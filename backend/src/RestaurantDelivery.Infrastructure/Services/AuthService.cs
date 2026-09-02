using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Auth;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRoleRepository _roleRepository;

    public AuthService(UserManager<AppUser> userManager, ITokenService tokenService, IRoleRepository roleRepository)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _roleRepository = roleRepository;
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

        return ServiceResult<AuthResponse>.Success(await BuildAuthResponseAsync(user));
    }

    // Email-based login, kept for the two pre-existing seeded staff accounts
    // (admin@restaurant.com / captain@restaurant.com) and any legacy account that
    // predates the switch to phone-based login - neither has a PhoneNumber on file.
    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return ServiceResult<AuthResponse>.Failure("Invalid email or password.");
        }

        return ServiceResult<AuthResponse>.Success(await BuildAuthResponseAsync(user));
    }

    public async Task<ServiceResult<AuthResponse>> LoginByPhoneAsync(PhoneLoginRequest request)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return ServiceResult<AuthResponse>.Failure("Invalid phone number or password.");
        }

        return ServiceResult<AuthResponse>.Success(await BuildAuthResponseAsync(user));
    }

    // Phone number is the universal login identifier now (customers and staff alike), so
    // uniqueness is enforced globally rather than scoped to a role - two accounts sharing a
    // phone would otherwise make LoginByPhoneAsync's lookup ambiguous.
    private Task<bool> IsPhoneTakenAsync(string phoneNumber) =>
        _userManager.Users.AnyAsync(u => u.PhoneNumber == phoneNumber);

    public async Task<ServiceResult<UserProfileResponse>> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult<UserProfileResponse>.Failure("User not found.");
        }

        var modules = await ResolveAdminModuleNamesAsync(user);
        return ServiceResult<UserProfileResponse>.Success(MapProfile(user, modules));
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
        return ServiceResult<UserProfileResponse>.Success(MapProfile(user, modules));
    }

    // Empty for Customer/CaptainOrder. For Admin: all 5 modules when no custom Role is
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
                AdminModules.Orders | AdminModules.MenuItems | AdminModules.Settings | AdminModules.Staff | AdminModules.Customers);
        }

        var role = await _roleRepository.GetByIdAsync(user.CustomRoleId.Value);
        return role is null
            ? AdminModulesMapper.ToNames(
                AdminModules.Orders | AdminModules.MenuItems | AdminModules.Settings | AdminModules.Staff | AdminModules.Customers)
            : AdminModulesMapper.ToNames(role.Modules);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(AppUser user)
    {
        var modules = await ResolveAdminModuleNamesAsync(user);
        var (token, expiresAtUtc) = _tokenService.CreateToken(user, modules);
        return new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            User = MapProfile(user, modules)
        };
    }

    private static UserProfileResponse MapProfile(AppUser user, List<string> modules) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        FullName = user.FullName,
        PhoneNumber = user.PhoneNumber,
        Address = user.Address,
        Role = user.Role,
        Modules = user.Role == UserRole.Admin ? modules : null
    };
}
