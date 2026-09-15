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
    private readonly IReadThroughCache _cache;

    public SettingsService(ApplicationDbContext context, IReadThroughCache cache)
    {
        _context = context;
        _cache = cache;
    }

    // Branding, tax rate and delivery fee - read by nearly every request (order creation
    // alone reads it twice) and written only from the admin settings screen.
    //
    // Only this mapped response is cached, never the tracked entity behind it:
    // GetOrCreateAsync is shared with UpdateAsync, which mutates what it returns, so
    // handing a cached entity to a second request would let two callers write through the
    // same instance.
    public async Task<RestaurantSettingsResponse> GetAsync()
    {
        return await _cache.GetOrCreateAsync(CacheGroup.Settings, "current", async () =>
        {
            var settings = await GetOrCreateAsync();
            return MapResponse(settings);
        });
    }

    public async Task<ServiceResult<RestaurantSettingsResponse>> UpdateAsync(UpdateRestaurantSettingsRequest request)
    {
        var settings = await GetOrCreateAsync();

        settings.RestaurantName = request.RestaurantName;
        settings.LogoUrl = request.LogoUrl;
        settings.PrimaryColor = request.PrimaryColor;
        settings.AccentColor = request.AccentColor;
        settings.HeaderColor = request.HeaderColor;
        settings.BodyColor = request.BodyColor;
        settings.IconColor = request.IconColor;
        settings.BackgroundImageUrl = request.BackgroundImageUrl;
        settings.CenterLogoUrl = request.CenterLogoUrl;
        settings.Address = request.Address;
        settings.Phone = request.Phone;
        settings.Email = request.Email;
        settings.FooterAbout = request.FooterAbout;
        settings.FaviconUrl = request.FaviconUrl;
        settings.TabTitle = request.TabTitle;
        settings.TaxPercentage = request.TaxPercentage;
        settings.IsCashEnabled = request.IsCashEnabled;
        settings.IsVisaEnabled = request.IsVisaEnabled;
        settings.VisaFawryUrl = request.VisaFawryUrl;
        settings.IsInstapayEnabled = request.IsInstapayEnabled;
        settings.InstapayAccount = request.InstapayAccount;
        settings.IsPickupEnabled = request.IsPickupEnabled;
        settings.PickupDuration = OptionalText.NullIfBlank(request.PickupDuration);
        settings.BaseDeliveryFee = request.BaseDeliveryFee;
        settings.EstimatedDeliveryMinMinutes = request.EstimatedDeliveryMinMinutes;
        settings.EstimatedDeliveryMaxMinutes = request.EstimatedDeliveryMaxMinutes;
        settings.ManagerWhatsApp1 = request.ManagerWhatsApp1;
        settings.ManagerWhatsApp2 = request.ManagerWhatsApp2;
        settings.ManagerWhatsApp3 = request.ManagerWhatsApp3;

        await _context.SaveChangesAsync();
        _cache.Invalidate(CacheGroup.Settings);

        return ServiceResult<RestaurantSettingsResponse>.Success(MapResponse(settings));
    }

    private async Task<RestaurantSettings> GetOrCreateAsync()
    {
        // OrderBy(Id) so "the settings row" resolves to the same row every time.
        // RestaurantSettings is a singleton by convention rather than by constraint - Id is
        // an identity column with nothing stopping a second row - and an unordered
        // FirstOrDefault against more than one row may return either, which is what EF
        // warns about here ("uses First/FirstOrDefault without OrderBy"). Today there is
        // exactly one row, so this changes nothing; the point is that if a duplicate ever
        // appeared (a hand-run insert, or two concurrent first-touches racing through the
        // create path below), every request would still agree on which row is authoritative
        // instead of branding and tax rates flickering between two answers.
        var settings = await _context.RestaurantSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (settings is not null)
        {
            return settings;
        }

        settings = new RestaurantSettings();
        _context.RestaurantSettings.Add(settings);
        await _context.SaveChangesAsync();
        _cache.Invalidate(CacheGroup.Settings);
        return settings;
    }

    private static RestaurantSettingsResponse MapResponse(RestaurantSettings settings) => new()
    {
        RestaurantName = settings.RestaurantName,
        LogoUrl = settings.LogoUrl,
        PrimaryColor = settings.PrimaryColor,
        AccentColor = settings.AccentColor,
        HeaderColor = settings.HeaderColor,
        BodyColor = settings.BodyColor,
        IconColor = settings.IconColor,
        BackgroundImageUrl = settings.BackgroundImageUrl,
        CenterLogoUrl = settings.CenterLogoUrl,
        Address = settings.Address,
        Phone = settings.Phone,
        Email = settings.Email,
        FooterAbout = settings.FooterAbout,
        FaviconUrl = settings.FaviconUrl,
        TabTitle = settings.TabTitle,
        TaxPercentage = settings.TaxPercentage,
        IsCashEnabled = settings.IsCashEnabled,
        IsVisaEnabled = settings.IsVisaEnabled,
        VisaFawryUrl = settings.VisaFawryUrl,
        IsInstapayEnabled = settings.IsInstapayEnabled,
        InstapayAccount = settings.InstapayAccount,
        IsPickupEnabled = settings.IsPickupEnabled,
        PickupDuration = settings.PickupDuration,
        BaseDeliveryFee = settings.BaseDeliveryFee,
        EstimatedDeliveryMinMinutes = settings.EstimatedDeliveryMinMinutes,
        EstimatedDeliveryMaxMinutes = settings.EstimatedDeliveryMaxMinutes,
        ManagerWhatsApp1 = settings.ManagerWhatsApp1,
        ManagerWhatsApp2 = settings.ManagerWhatsApp2,
        ManagerWhatsApp3 = settings.ManagerWhatsApp3
    };
}
