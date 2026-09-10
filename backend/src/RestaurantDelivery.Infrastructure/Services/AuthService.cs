using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Auth;
using RestaurantDelivery.Core.DTOs.Customers;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Services;

public class AuthService : IAuthService
{
    // A 6-digit OTP's expiry window (ForgotPasswordAsync) - matches the exact wording of
    // SendPasswordResetOtpAsync's WhatsApp template ("صالح لمدة 10 دقائق").
    private const int OtpExpiryMinutes = 10;

    // Brute-force guard on ResetPasswordAsync - a 6-digit code only has 1,000,000
    // possibilities, too few to leave unlimited guesses against within the expiry window.
    private const int MaxOtpAttempts = 5;

    private readonly UserManager<AppUser> _userManager;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRoleRepository _roleRepository;
    private readonly IWhatsAppNotificationService _whatsAppNotificationService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppUser> userManager,
        IPasswordHasher<AppUser> passwordHasher,
        ITokenService tokenService,
        IRoleRepository roleRepository,
        IWhatsAppNotificationService whatsAppNotificationService,
        ILoyaltyService loyaltyService,
        ApplicationDbContext context,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _roleRepository = roleRepository;
        _whatsAppNotificationService = whatsAppNotificationService;
        _loyaltyService = loyaltyService;
        _context = context;
        _logger = logger;
    }

    public async Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        // A direct entity lookup, not IsPhoneTakenAsync's plain bool - reactivating a
        // soft-deleted match needs the actual row, and IsPhoneTakenAsync stays reserved
        // for CreateStaffUserAsync (staff/admin accounts are never soft-deleted, so it has
        // no reactivation branch to consider).
        var existing = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (existing is not null)
        {
            if (!existing.IsDeleted)
            {
                return ServiceResult<AuthResponse>.Failure("An account with this phone number already exists.");
            }

            // Reactivation: a previously soft-deleted customer signing themselves back up
            // under the same phone number - same treatment as
            // FindOrCreateCustomerByPhoneAsync's reactivation branch (the Scanner/Create
            // Order staff path): name/address updated from the form, points reset to the
            // flat welcome bonus, welcome WhatsApp resent, every historical Order/
            // OrderReview/LoyaltyPointTransaction row left untouched. Unlike that path,
            // the customer IS present and just typed their own new password - it goes
            // through RemovePasswordAsync/AddPasswordAsync (Identity's fully validated
            // password-change flow, same length/complexity policy CreateAsync(user,
            // password) would enforce for a fresh signup) rather than the direct
            // IPasswordHasher bypass used for a staff-generated 6-digit password that
            // could never pass that policy in the first place. Role is force-reset to
            // Customer too, not just IsDeleted - the phone-number lookup above has no Role
            // scope, so this could in principle be reactivating a row whose Role drifted
            // away from Customer for some other reason. Without this, the row comes back
            // non-deleted but still invisible to CrmController's Role == Customer filter
            // (Customer Insights), while still matching a Role-unscoped lookup elsewhere
            // (the Scanner's phone search) - exactly the split-visibility bug this fixes.
            existing.IsDeleted = false;
            existing.Role = UserRole.Customer;
            existing.FullName = request.FullName;
            existing.Address = request.Address;

            var reactivateResult = await _userManager.UpdateAsync(existing);
            if (!reactivateResult.Succeeded)
            {
                return ServiceResult<AuthResponse>.Failure(reactivateResult.Errors.Select(e => e.Description).ToArray());
            }

            var removePasswordResult = await _userManager.RemovePasswordAsync(existing);
            if (!removePasswordResult.Succeeded)
            {
                return ServiceResult<AuthResponse>.Failure(removePasswordResult.Errors.Select(e => e.Description).ToArray());
            }

            var addPasswordResult = await _userManager.AddPasswordAsync(existing, request.Password);
            if (!addPasswordResult.Succeeded)
            {
                return ServiceResult<AuthResponse>.Failure(addPasswordResult.Errors.Select(e => e.Description).ToArray());
            }

            await AwardWelcomeBonusAndNotifyAsync(existing, request.Password, isReactivation: true);

            return ServiceResult<AuthResponse>.Success(await BuildAuthResponseAsync(existing));
        }

        // Identity requires a unique Email/UserName (RequireUniqueEmail=true) even though
        // customers log in by phone - this placeholder is never shown to or used by the
        // customer, it only satisfies Identity's internal bookkeeping. Unique because phone
        // number is enforced-unique above.
        var syntheticEmail = $"customer+{request.PhoneNumber}@internal.otantik";

        var user = new AppUser
        {
            UserName = syntheticEmail,
            Email = syntheticEmail,
            EmailConfirmed = true,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            Role = UserRole.Customer
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return ServiceResult<AuthResponse>.Failure(createResult.Errors.Select(e => e.Description).ToArray());
        }

        // The customer's own just-chosen password, recited back in the welcome WhatsApp
        // message as a login receipt - see AwardWelcomeBonusAndNotifyAsync/
        // SendWelcomeMessageAsync's template, which now always shows phone+password.
        await AwardWelcomeBonusAndNotifyAsync(user, request.Password, isReactivation: false);

        return ServiceResult<AuthResponse>.Success(await BuildAuthResponseAsync(user));
    }

    // Single sign-in entry point for every account - customers and staff alike. Detects
    // whether Identifier is an email (contains '@') or a phone number and looks the
    // account up accordingly, so the client never needs to ask which kind it's sending.
    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var identifier = request.Identifier.Trim();
        var isEmail = identifier.Contains('@');

        var user = isEmail
            ? await _userManager.FindByEmailAsync(identifier)
            : await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == identifier);

        // A soft-deleted account fails the same generic message as a wrong password - not
        // a distinct "this account was deleted" error, to avoid leaking account status to
        // whoever's typing (see AppUser.IsDeleted).
        if (user is null || user.IsDeleted || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return ServiceResult<AuthResponse>.Failure(
                isEmail ? "Invalid email or password." : "Invalid phone number or password.");
        }

        // RememberMe is nullable and opt-in: an older/other client that never sends it
        // (e.g. EmailLoginComponent's staff sign-in) falls through to null, which keeps
        // JwtTokenService's own Jwt:ExpiryMinutes config default completely unchanged -
        // only a client that explicitly sends true/false gets the new 30-day/1-day session.
        TimeSpan? expiryOverride = request.RememberMe switch
        {
            true => TimeSpan.FromDays(30),
            false => TimeSpan.FromDays(1),
            null => null
        };

        return ServiceResult<AuthResponse>.Success(await BuildAuthResponseAsync(user, expiryOverride));
    }

    // Used by CreateStaffUserAsync only - RegisterAsync now does its own phone lookup
    // above so it can reactivate a soft-deleted match instead of just rejecting it. Phone
    // number is the universal login identifier now (customers and staff alike), so
    // uniqueness is enforced globally rather than scoped to a role - two accounts sharing a
    // phone would otherwise make LoginByPhoneAsync's lookup ambiguous. Matches a
    // soft-deleted customer's number too, which is fine here: staff/admin accounts are
    // never soft-deleted (see AppUser.IsDeleted), so there's no reactivation concept for
    // this method to consider - leaving a deleted customer's old number blocked for a
    // brand-new STAFF account is just the conservative, harmless default.
    private Task<bool> IsPhoneTakenAsync(string phoneNumber) =>
        _userManager.Users.AnyAsync(u => u.PhoneNumber == phoneNumber);

    public async Task<ServiceResult<UserProfileResponse>> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        // A soft-deleted customer with a still-valid JWT loses access on their next profile
        // refresh, rather than staying fully functional until the token naturally expires.
        if (user is null || user.IsDeleted)
        {
            return ServiceResult<UserProfileResponse>.Failure("User not found.");
        }

        var modules = await ResolveAdminModuleNamesAsync(user);
        var permissions = await ResolveGranularPermissionNamesAsync(user);
        return ServiceResult<UserProfileResponse>.Success(MapProfile(user, modules, permissions));
    }

    public async Task<ServiceResult<UserProfileResponse>> CreateStaffUserAsync(CreateStaffUserRequest request)
    {
        if (request.Role is not (UserRole.Admin or UserRole.CaptainOrder))
        {
            return ServiceResult<UserProfileResponse>.Failure(
                "Only Admin or CaptainOrder accounts can be created here — customers self-register.");
        }

        if (request.Role == UserRole.Admin)
        {
            if (request.RoleId is null)
            {
                return ServiceResult<UserProfileResponse>.Failure("A role must be selected for an Admin account.");
            }

            if (await _roleRepository.GetByIdAsync(request.RoleId.Value) is null)
            {
                return ServiceResult<UserProfileResponse>.Failure("The selected role was not found.");
            }
        }
        else if (request.RoleId is not null)
        {
            return ServiceResult<UserProfileResponse>.Failure("A role cannot be selected for a Captain Order account.");
        }

        if (await IsPhoneTakenAsync(request.PhoneNumber))
        {
            return ServiceResult<UserProfileResponse>.Failure("An account with this phone number already exists.");
        }

        // Identity requires a unique Email/UserName (RequireUniqueEmail=true) even though
        // staff log in by phone - this placeholder is never shown to or used by the staff
        // member, it only satisfies Identity's internal bookkeeping. Unique because phone
        // number is enforced-unique above.
        var syntheticEmail = $"staff+{request.PhoneNumber}@internal.otantik";

        var user = new AppUser
        {
            UserName = syntheticEmail,
            Email = syntheticEmail,
            EmailConfirmed = true,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = request.Role,
            CustomRoleId = request.Role == UserRole.Admin ? request.RoleId : null
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return ServiceResult<UserProfileResponse>.Failure(createResult.Errors.Select(e => e.Description).ToArray());
        }

        var modules = await ResolveAdminModuleNamesAsync(user);
        var permissions = await ResolveGranularPermissionNamesAsync(user);
        return ServiceResult<UserProfileResponse>.Success(MapProfile(user, modules, permissions));
    }

    public async Task<ServiceResult<FindOrCreateCustomerResult>> FindOrCreateCustomerByPhoneAsync(string fullName, string phoneNumber, string? address, bool isPastCustomer = false)
    {
        // Matches a soft-deleted account too, same reasoning as IsPhoneTakenAsync above:
        // treating a deleted row as "no match" would fall through to _userManager.CreateAsync
        // with the same deterministic synthetic email, which would fail on that stale
        // account's still-normalized Identity columns.
        var existing = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        if (existing is not null)
        {
            if (!existing.IsDeleted)
            {
                return ServiceResult<FindOrCreateCustomerResult>.Success(
                    new FindOrCreateCustomerResult { CustomerId = existing.Id, IsNewCustomer = false });
            }

            // Reactivation: re-registering a previously soft-deleted customer's phone
            // number brings the same account back rather than silently handing back a
            // still-hidden id (which would then 404 out of every Scanner/Registered-
            // Customer lookup that filters on IsDeleted) or colliding on the deterministic
            // synthetic email if this fell through to CreateAsync below. Treated exactly
            // like a brand-new signup for welcome purposes - name updates, points reset to
            // the flat welcome bonus, welcome WhatsApp resent - while every historical
            // Order/OrderReview/LoyaltyPointTransaction row tied to this AppUser id, from
            // both before AND after this reactivation, stays intact throughout. Role is
            // force-reset to Customer for the same reason IsDeleted is: the phone lookup
            // above has no Role scope, so without this the row can come back non-deleted
            // yet still excluded from CrmController's Role == Customer filter (Customer
            // Insights) while a Role-unscoped lookup (the Scanner's phone search) still
            // finds it - the exact split-visibility bug this fixes.
            existing.IsDeleted = false;
            existing.Role = UserRole.Customer;
            existing.FullName = fullName;
            if (address is not null)
            {
                existing.Address = address;
            }

            // A fresh default password too, same as a brand-new signup below - whatever
            // they logged in with before is gone the moment they're soft-deleted, so
            // reactivation needs to hand them a new, working one rather than leave
            // PasswordHash pointing at a password nobody (customer or staff) still knows.
            var reactivationPassword = GenerateSixDigitCode();
            existing.PasswordHash = _passwordHasher.HashPassword(existing, reactivationPassword);

            var reactivateResult = await _userManager.UpdateAsync(existing);
            if (!reactivateResult.Succeeded)
            {
                return ServiceResult<FindOrCreateCustomerResult>.Failure(reactivateResult.Errors.Select(e => e.Description).ToArray());
            }

            await AwardWelcomeBonusAndNotifyAsync(existing, reactivationPassword, isReactivation: true, isPastCustomer: isPastCustomer);

            return ServiceResult<FindOrCreateCustomerResult>.Success(
                new FindOrCreateCustomerResult { CustomerId = existing.Id, IsNewCustomer = true });
        }

        // Identity requires a unique Email/UserName even though customers log in by phone -
        // same placeholder convention as RegisterAsync/CreateStaffUserAsync. Unique because
        // the phone lookup above already confirmed this number isn't taken.
        var syntheticEmail = $"customer+{phoneNumber}@internal.otantik";

        var user = new AppUser
        {
            UserName = syntheticEmail,
            Email = syntheticEmail,
            EmailConfirmed = true,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            Address = address,
            Role = UserRole.Customer
        };

        // The customer isn't present to choose a password - a cashier is typing their
        // details into the POS, not the customer themselves. A real, working default
        // password (sent to them via the welcome WhatsApp message below) so they can log
        // in and manage their own account afterward, rather than the old permanently-
        // unusable placeholder this used to generate.
        var defaultPassword = GenerateSixDigitCode();

        // CreateAsync(user) - the no-password overload - deliberately skips Identity's
        // PasswordValidator pipeline (Program.cs's Password.RequiredLength = 8, plus the
        // still-default RequireUppercase/RequireLowercase) which a plain 6-digit numeric
        // password would fail outright. IUserValidator (the Email/UserName uniqueness
        // checks) still runs. The hash is set directly via IPasswordHasher afterward,
        // bypassing only the password-complexity policy for this one system-generated
        // value - every customer- or staff-chosen password elsewhere in the app still
        // goes through CreateAsync(user, password)/AddPasswordAsync and is fully validated.
        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            return ServiceResult<FindOrCreateCustomerResult>.Failure(createResult.Errors.Select(e => e.Description).ToArray());
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, defaultPassword);
        var setPasswordResult = await _userManager.UpdateAsync(user);
        if (!setPasswordResult.Succeeded)
        {
            return ServiceResult<FindOrCreateCustomerResult>.Failure(setPasswordResult.Errors.Select(e => e.Description).ToArray());
        }

        // Same welcome treatment RegisterAsync's self-service path gives a brand-new
        // customer - a staff-registered customer (Scanner's New Customer tab, Create
        // Order's New Customer section) gets the 100-point bonus and welcome WhatsApp
        // message too, not just whoever happened to register themselves.
        await AwardWelcomeBonusAndNotifyAsync(user, defaultPassword, isReactivation: false, isPastCustomer: isPastCustomer);

        return ServiceResult<FindOrCreateCustomerResult>.Success(
            new FindOrCreateCustomerResult { CustomerId = user.Id, IsNewCustomer = true });
    }

    // Always succeeds from the caller's perspective, regardless of whether this phone
    // number is actually registered - same anti-enumeration reasoning as LoginAsync's
    // generic "Invalid phone number or password" message, just applied at the "does an
    // account exist" question instead of "is this the right password". Only a real,
    // non-deleted Customer account actually gets an OTP generated and a WhatsApp sent.
    public async Task<ServiceResult<bool>> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(
            u => u.PhoneNumber == request.PhoneNumber && u.Role == UserRole.Customer);

        if (user is not null && !user.IsDeleted)
        {
            // Any still-active token from an earlier request is superseded - otherwise
            // two independently-valid codes could exist for the same account at once,
            // which is confusing (which one is "the" real code?) even though it isn't
            // itself a security hole.
            var supersededTokens = await _context.PasswordResetTokens
                .Where(t => t.AppUserId == user.Id && !t.IsUsed)
                .ToListAsync();
            foreach (var superseded in supersededTokens)
            {
                superseded.IsUsed = true;
            }

            var otp = GenerateSixDigitCode();
            _context.PasswordResetTokens.Add(new PasswordResetToken
            {
                AppUserId = user.Id,
                OtpHash = _passwordHasher.HashPassword(user, otp),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes)
            });
            await _context.SaveChangesAsync();

            // Never allowed to fail the caller - see IWhatsAppNotificationService's contract.
            await _whatsAppNotificationService.SendPasswordResetOtpAsync(user.PhoneNumber!, otp);
        }

        return ServiceResult<bool>.Success(true);
    }

    // Every rejection reason - no such account, no active token, expired, already used,
    // wrong code, too many attempts - returns the exact same generic message. Specific
    // reasons are deliberately never distinguished in the response, so this endpoint can't
    // be used to enumerate registered phone numbers or probe which OTPs are still valid.
    // A weak NewPassword is a different concern (real, actionable feedback the customer
    // needs to fix their own input) and still returns Identity's actual validation errors.
    public async Task<ServiceResult<bool>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        const string genericError = "Invalid or expired code. Please request a new one.";

        var user = await _userManager.Users.FirstOrDefaultAsync(
            u => u.PhoneNumber == request.PhoneNumber && u.Role == UserRole.Customer);

        if (user is null || user.IsDeleted)
        {
            return ServiceResult<bool>.Failure(genericError);
        }

        var token = await _context.PasswordResetTokens
            .Where(t => t.AppUserId == user.Id && !t.IsUsed && t.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (token is null || token.FailedAttempts >= MaxOtpAttempts)
        {
            return ServiceResult<bool>.Failure(genericError);
        }

        if (_passwordHasher.VerifyHashedPassword(user, token.OtpHash, request.Otp) == PasswordVerificationResult.Failed)
        {
            token.FailedAttempts++;
            await _context.SaveChangesAsync();
            return ServiceResult<bool>.Failure(genericError);
        }

        // The customer's own real, chosen password - fully validated (length/complexity),
        // same as self-service registration's reactivation branch, not the direct
        // IPasswordHasher bypass used for a staff-generated code that could never pass
        // that policy in the first place.
        var removePasswordResult = await _userManager.RemovePasswordAsync(user);
        if (!removePasswordResult.Succeeded)
        {
            return ServiceResult<bool>.Failure(removePasswordResult.Errors.Select(e => e.Description).ToArray());
        }

        var addPasswordResult = await _userManager.AddPasswordAsync(user, request.NewPassword);
        if (!addPasswordResult.Succeeded)
        {
            return ServiceResult<bool>.Failure(addPasswordResult.Errors.Select(e => e.Description).ToArray());
        }

        token.IsUsed = true;
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    // Shared by RegisterAsync, FindOrCreateCustomerByPhoneAsync's create branch, and its
    // reactivation branch, so a customer's welcome bonus/notification can never drift out
    // of sync between the paths that grant it. isReactivation picks
    // ResetToWelcomeBonusAsync (a hard reset to the flat welcome amount, since a
    // reactivated customer is explicitly treated as a brand-new welcome) over
    // AwardWelcomeBonusAsync (a plain += on a fresh, always-zero profile) - using the
    // latter for a reactivation would incorrectly stack on top of whatever balance the
    // account still had from before it was soft-deleted.
    private async Task AwardWelcomeBonusAndNotifyAsync(AppUser user, string password, bool isReactivation, bool isPastCustomer = false)
    {
        // Never allowed to fail the caller: the account has already been committed (or, for
        // a reactivation, already saved undeleted) above, so a hiccup awarding the bonus
        // must not turn an otherwise-successful registration into a 500 the client would
        // retry against a phone number that's now already taken/reactivated.
        var bonusAwarded = true;
        try
        {
            if (isReactivation)
            {
                await _loyaltyService.ResetToWelcomeBonusAsync(user.Id);
            }
            else
            {
                await _loyaltyService.AwardWelcomeBonusAsync(user.Id);
            }
        }
        catch (Exception ex)
        {
            bonusAwarded = false;
            _logger.LogError(ex, "Failed to award welcome bonus to {Context} customer {UserId}.",
                isReactivation ? "reactivated" : "new", user.Id);
        }

        // SendWelcomeMessageAsync's template states a specific "100 points" balance as
        // fact - only send it once that's actually true, rather than telling a customer
        // they have a bonus balance that a swallowed failure above just meant they don't.
        if (!bonusAwarded)
        {
            return;
        }

        // Never allowed to fail the caller either - see IWhatsAppNotificationService's
        // contract. isPastCustomer picks the no-review-link variant, since a past-visit
        // customer registered from Customer Insights has no delivery order to review.
        if (isPastCustomer)
        {
            await _whatsAppNotificationService.SendPastCustomerWelcomeAsync(user.PhoneNumber!, user.FullName, password);
        }
        else
        {
            await _whatsAppNotificationService.SendWelcomeMessageAsync(user.PhoneNumber!, user.FullName, password);
        }
    }

    // A plain 6-digit numeric string (e.g. "482915") - simple enough for a customer to
    // read off WhatsApp and type on a phone keypad. Shared by every staff-generated
    // default password (never passed through Identity's own password validators - see
    // those call sites - since it would fail this app's configured length/complexity
    // policy outright) and by ForgotPasswordAsync's OTP generation below (hashed and
    // verified separately, never validated as a password at all).
    private static string GenerateSixDigitCode() => Random.Shared.Next(100000, 1000000).ToString();

    // Empty for Customer/CaptainOrder. For Admin: every module when no custom Role is
    // assigned (the default, backward-compatible "full access" superuser behavior), else
    // the assigned Role's modules - falling back to full access if that Role has somehow
    // gone missing, rather than silently locking the admin out.
    private async Task<List<string>> ResolveAdminModuleNamesAsync(AppUser user)
    {
        if (user.Role != UserRole.Admin)
        {
            return new List<string>();
        }

        if (user.CustomRoleId is null)
        {
            return AdminModulesMapper.ToNames(
                AdminModules.Orders | AdminModules.MenuItems | AdminModules.Settings | AdminModules.Staff | AdminModules.Customers |
                AdminModules.Crm | AdminModules.Campaigns | AdminModules.Scanner | AdminModules.PromoCodes | AdminModules.Reviews);
        }

        var role = await _roleRepository.GetByIdAsync(user.CustomRoleId.Value);
        return role is null
            ? AdminModulesMapper.ToNames(
                AdminModules.Orders | AdminModules.MenuItems | AdminModules.Settings | AdminModules.Staff | AdminModules.Customers)
            : AdminModulesMapper.ToNames(role.Modules);
    }

    // Empty for Customer/CaptainOrder, and empty for the no-CustomRole superuser default
    // (mirrors ResolveAdminModuleNamesAsync's own "full access" default - a superuser has
    // no recorded restrictions on any module). Otherwise the assigned Role's own granular
    // permissions - empty if that Role recorded none, meaning "full access within whatever
    // modules it has", the same "absence = full access" rule AdminModuleClaimsHelper
    // already applies at the module level.
    private async Task<List<string>> ResolveGranularPermissionNamesAsync(AppUser user)
    {
        if (user.Role != UserRole.Admin || user.CustomRoleId is null)
        {
            return new List<string>();
        }

        var role = await _roleRepository.GetByIdAsync(user.CustomRoleId.Value);
        if (role is null || string.IsNullOrEmpty(role.GranularPermissionsJson))
        {
            return new List<string>();
        }

        return JsonSerializer.Deserialize<List<string>>(role.GranularPermissionsJson) ?? new List<string>();
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(AppUser user, TimeSpan? expiryOverride = null)
    {
        var modules = await ResolveAdminModuleNamesAsync(user);
        var permissions = await ResolveGranularPermissionNamesAsync(user);
        var (token, expiresAtUtc) = _tokenService.CreateToken(user, modules, permissions, expiryOverride);
        return new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            User = MapProfile(user, modules, permissions)
        };
    }

    private static UserProfileResponse MapProfile(AppUser user, List<string> modules, List<string> permissions) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        FullName = user.FullName,
        PhoneNumber = user.PhoneNumber,
        Address = user.Address,
        Role = user.Role,
        Modules = user.Role == UserRole.Admin ? modules : null,
        GranularPermissions = user.Role == UserRole.Admin ? permissions : null
    };
}
