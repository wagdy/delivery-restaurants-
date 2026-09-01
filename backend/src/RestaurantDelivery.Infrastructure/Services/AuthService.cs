using Microsoft.AspNetCore.Identity;
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

    public AuthService(UserManager<AppUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return ServiceResult<AuthResponse>.Failure("An account with this email already exists.");
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
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

        return ServiceResult<AuthResponse>.Success(BuildAuthResponse(user));
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return ServiceResult<AuthResponse>.Failure("Invalid email or password.");
        }

        return ServiceResult<AuthResponse>.Success(BuildAuthResponse(user));
    }

    public async Task<ServiceResult<UserProfileResponse>> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult<UserProfileResponse>.Failure("User not found.");
        }

        return ServiceResult<UserProfileResponse>.Success(MapProfile(user));
    }

    public async Task<ServiceResult<UserProfileResponse>> CreateStaffUserAsync(CreateStaffUserRequest request)
    {
        if (request.Role is not (UserRole.Admin or UserRole.CaptainOrder))
        {
            return ServiceResult<UserProfileResponse>.Failure(
                "Only Admin or CaptainOrder accounts can be created here — customers self-register.");
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return ServiceResult<UserProfileResponse>.Failure("An account with this email already exists.");
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = request.Role
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return ServiceResult<UserProfileResponse>.Failure(createResult.Errors.Select(e => e.Description).ToArray());
        }

        return ServiceResult<UserProfileResponse>.Success(MapProfile(user));
    }

    private AuthResponse BuildAuthResponse(AppUser user)
    {
        var (token, expiresAtUtc) = _tokenService.CreateToken(user);
        return new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            User = MapProfile(user)
        };
    }

    private static UserProfileResponse MapProfile(AppUser user) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        FullName = user.FullName,
        PhoneNumber = user.PhoneNumber,
        Address = user.Address,
        Role = user.Role
    };
}
