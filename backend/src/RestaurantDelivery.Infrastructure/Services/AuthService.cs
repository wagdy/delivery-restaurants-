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

namespace RestaurantDelivery.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRoleRepository _roleRepository;
    private readonly IWhatsAppNotificationService _whatsAppNotificationService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppUser> userManager,
        ITokenService tokenService,
        IRoleRepository roleRepository,
        IWhatsAppNotificationService whatsAppNotificationService,
        ILoyaltyService loyaltyService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _roleRepository = roleRepository;
        _whatsAppNotificationService = whatsAppNotificationService;
        _loyaltyService = loyaltyService;
        _logger = logger;
    }

    public async Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        if (await IsPhoneTakenAsync(request.PhoneNumber))
        {
            return ServiceResult<AuthResponse>.Failure("An account with this phone number already exists.");
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

        await AwardWelcomeBonusAndNotifyAsync(user);

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

        return ServiceResult<AuthResponse>.Success(await BuildAuthResponseAsync(user));
    }

    // Phone number is the universal login identifier now (customers and staff alike), so
    // uniqueness is enforced globally rather than scoped to a role - two accounts sharing a
    // phone would otherwise make LoginByPhoneAsync's lookup ambiguous.
    //
    // Deliberately NOT excluding IsDeleted accounts here: RegisterAsync's synthetic email
    // (customer+{phone}@internal.otantik) is deterministic from the phone number alone, so
    // even if this check let a deleted account's number through, _userManager.CreateAsync
    // would still reject the new account on that email/username colliding with the
    // deleted row's still-normalized Identity columns - a confusing "Username already
    // taken" error in place of this method's own clear "account already exists" message.
    // Freeing a deleted customer's phone number for reuse would need the delete flow to
    // also rewrite that account's Email/UserName (and their normalized columns via
    // UserManager.SetEmailAsync/SetUserNameAsync) - a real but separate follow-up.
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

    public async Task<ServiceResult<FindOrCreateCustomerResult>> FindOrCreateCustomerByPhoneAsync(string fullName, string phoneNumber, string? address)
    {
        // Matches a soft-deleted account too, same reasoning as IsPhoneTakenAsync above:
        // treating a deleted row as "no match" would fall through to _userManager.CreateAsync
        // with the same deterministic synthetic email, which would fail on that stale
        // account's still-normalized Identity columns. Reattaching a new order to the
        // existing (deactivated) AppUser id is a narrow, pre-existing edge case - it costs
        // nothing (no data is lost or corrupted) and is far better than a broken order
        // creation flow.
        var existing = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        if (existing is not null)
        {
            return ServiceResult<FindOrCreateCustomerResult>.Success(
                new FindOrCreateCustomerResult { CustomerId = existing.Id, IsNewCustomer = false });
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
        // details into the POS, not the customer themselves. This value is never shown to
        // or used by anyone; it only satisfies Identity's requirement that every account
        // have one. A real login (password reset, or a future OTP flow) would be a
        // separate, explicit follow-up - this account exists for CRM/loyalty tracking and
        // phone-based recognition on a future visit, not for the customer to sign into yet.
        var generatedPassword = $"Aa1{Guid.NewGuid():N}{Guid.NewGuid():N}";

        var createResult = await _userManager.CreateAsync(user, generatedPassword);
        if (!createResult.Succeeded)
        {
            return ServiceResult<FindOrCreateCustomerResult>.Failure(createResult.Errors.Select(e => e.Description).ToArray());
        }

        // Same welcome treatment RegisterAsync's self-service path gives a brand-new
        // customer - a staff-registered customer (Scanner's New Customer tab, Create
        // Order's New Customer section) gets the 100-point bonus and welcome WhatsApp
        // message too, not just whoever happened to register themselves.
        await AwardWelcomeBonusAndNotifyAsync(user);

        return ServiceResult<FindOrCreateCustomerResult>.Success(
            new FindOrCreateCustomerResult { CustomerId = user.Id, IsNewCustomer = true });
    }

    // Shared by RegisterAsync and FindOrCreateCustomerByPhoneAsync's create branch, so a
    // brand-new customer's welcome bonus/notification can never drift out of sync between
    // the two paths that create one, the way FindOrCreateCustomerByPhoneAsync used to
    // silently skip both entirely before this method existed.
    private async Task AwardWelcomeBonusAndNotifyAsync(AppUser user)
    {
        // Never allowed to fail the caller: the account has already been committed by
        // _userManager.CreateAsync, so a hiccup awarding the signup bonus must not turn an
        // otherwise-successful registration into a 500 the client would retry against a
        // phone number that's now already taken.
        var bonusAwarded = true;
        try
        {
            await _loyaltyService.AwardWelcomeBonusAsync(user.Id);
        }
        catch (Exception ex)
        {
            bonusAwarded = false;
            _logger.LogError(ex, "Failed to award welcome bonus to new customer {UserId}.", user.Id);
        }

        // SendWelcomeMessageAsync's template states a specific "100 points" balance as
        // fact - only send it once that's actually true, rather than telling a customer
        // they have a bonus balance that a swallowed failure above just meant they don't.
        if (!bonusAwarded)
        {
            return;
        }

        // Never allowed to fail the caller either - see IWhatsAppNotificationService's contract.
        await _whatsAppNotificationService.SendWelcomeMessageAsync(user.PhoneNumber!, user.FullName);
    }

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

    private async Task<AuthResponse> BuildAuthResponseAsync(AppUser user)
    {
        var modules = await ResolveAdminModuleNamesAsync(user);
        var permissions = await ResolveGranularPermissionNamesAsync(user);
        var (token, expiresAtUtc) = _tokenService.CreateToken(user, modules, permissions);
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
