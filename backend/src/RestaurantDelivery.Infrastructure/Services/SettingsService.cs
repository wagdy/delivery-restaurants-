using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Settings;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly ApplicationDbContext _context;

    public SettingsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RestaurantSettingsResponse> GetAsync()
    {
        var settings = await GetOrCreateAsync();
        return MapResponse(settings);
    }

    public async Task<ServiceResult<RestaurantSettingsResponse>> UpdateAsync(UpdateRestaurantSettingsRequest request)
    {
        var settings = await GetOrCreateAsync();

        settings.RestaurantName = request.RestaurantName;
        settings.LogoUrl = request.LogoUrl;
        settings.PrimaryColor = request.PrimaryColor;
        settings.AccentColor = request.AccentColor;
        settings.Address = request.Address;
        settings.Phone = request.Phone;
        settings.Email = request.Email;
        settings.FooterAbout = request.FooterAbout;

        await _context.SaveChangesAsync();

        return ServiceResult<RestaurantSettingsResponse>.Success(MapResponse(settings));
    }

    private async Task<RestaurantSettings> GetOrCreateAsync()
    {
        var settings = await _context.RestaurantSettings.FirstOrDefaultAsync();
        if (settings is not null)
        {
            return settings;
        }

        settings = new RestaurantSettings();
        _context.RestaurantSettings.Add(settings);
        await _context.SaveChangesAsync();
        return settings;
    }

    private static RestaurantSettingsResponse MapResponse(RestaurantSettings settings) => new()
    {
        RestaurantName = settings.RestaurantName,
        LogoUrl = settings.LogoUrl,
        PrimaryColor = settings.PrimaryColor,
        AccentColor = settings.AccentColor,
        Address = settings.Address,
        Phone = settings.Phone,
        Email = settings.Email,
        FooterAbout = settings.FooterAbout
    };
}
