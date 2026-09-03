using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Api.Services.Loyalty;

public class ApplePassKitService : IApplePassKitService
{
    private readonly ApplicationDbContext _context;

    public ApplePassKitService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool?> RegisterDeviceAsync(string appUserId, string deviceLibraryIdentifier, string passTypeIdentifier, string pushToken, CancellationToken ct)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == appUserId, ct);
        if (!userExists)
        {
            return null;
        }

        var existing = await _context.LoyaltyWalletPassRegistrations.FirstOrDefaultAsync(
            r => r.AppUserId == appUserId && r.DeviceLibraryIdentifier == deviceLibraryIdentifier && r.PassTypeIdentifier == passTypeIdentifier, ct);

        if (existing is not null)
        {
            existing.PushToken = pushToken;
            await _context.SaveChangesAsync(ct);
            return false;
        }

        _context.LoyaltyWalletPassRegistrations.Add(new LoyaltyWalletPassRegistration
        {
            AppUserId = appUserId,
            DeviceLibraryIdentifier = deviceLibraryIdentifier,
            PassTypeIdentifier = passTypeIdentifier,
            PushToken = pushToken
        });

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UnregisterDeviceAsync(string appUserId, string deviceLibraryIdentifier, string passTypeIdentifier, CancellationToken ct)
    {
        var existing = await _context.LoyaltyWalletPassRegistrations.FirstOrDefaultAsync(
            r => r.AppUserId == appUserId && r.DeviceLibraryIdentifier == deviceLibraryIdentifier && r.PassTypeIdentifier == passTypeIdentifier, ct);

        if (existing is null)
        {
            return false;
        }

        _context.LoyaltyWalletPassRegistrations.Remove(existing);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<UpdatablePassesResponse?> GetUpdatablePassesAsync(string deviceLibraryIdentifier, string passTypeIdentifier, DateTimeOffset? updatedSince, CancellationToken ct)
    {
        var registrations = await _context.LoyaltyWalletPassRegistrations
            .Where(r => r.DeviceLibraryIdentifier == deviceLibraryIdentifier && r.PassTypeIdentifier == passTypeIdentifier)
            .Select(r => new { r.AppUserId, LastActivityDate = _context.LoyaltyProfiles.Where(p => p.AppUserId == r.AppUserId).Select(p => p.LastActivityDate).FirstOrDefault() })
            .ToListAsync(ct);

        if (updatedSince is { } since)
        {
            registrations = registrations.Where(r => r.LastActivityDate > since.UtcDateTime).ToList();
        }

        if (registrations.Count == 0)
        {
            return null;
        }

        var serials = registrations.Select(r => r.AppUserId).ToList();
        var lastUpdated = registrations.Max(r => r.LastActivityDate).ToString("O");

        return new UpdatablePassesResponse(serials, lastUpdated);
    }
}
