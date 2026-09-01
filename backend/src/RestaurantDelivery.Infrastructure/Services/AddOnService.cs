using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.AddOns;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class AddOnService : IAddOnService
{
    private readonly IAddOnRepository _repository;

    public AddOnService(IAddOnRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<AddOnResponse>> GetAllAsync()
    {
        var addOns = await _repository.GetAllOrderedAsync();
        return addOns.Select(MapResponse).ToList();
    }

    public async Task<ServiceResult<AddOnResponse>> CreateAsync(AddOnRequest request)
    {
        var name = request.Name.Trim();

        if (await _repository.GetByNameAsync(name) is not null)
        {
            return ServiceResult<AddOnResponse>.Failure("An add-on with this name already exists.");
        }

        var addOn = new AddOn { Name = name, Price = request.Price };
        await _repository.AddAsync(addOn);
        await _repository.SaveChangesAsync();

        return ServiceResult<AddOnResponse>.Success(MapResponse(addOn));
    }

    public async Task<ServiceResult<AddOnResponse>> UpdateAsync(int id, AddOnRequest request)
    {
        var addOn = await _repository.GetByIdAsync(id);
        if (addOn is null)
        {
            return ServiceResult<AddOnResponse>.Failure("Add-on not found.");
        }

        var name = request.Name.Trim();

        var existing = await _repository.GetByNameAsync(name);
        if (existing is not null && existing.Id != id)
        {
            return ServiceResult<AddOnResponse>.Failure("An add-on with this name already exists.");
        }

        addOn.Name = name;
        addOn.Price = request.Price;

        await _repository.SaveChangesAsync();

        return ServiceResult<AddOnResponse>.Success(MapResponse(addOn));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var addOn = await _repository.GetByIdAsync(id);
        if (addOn is null)
        {
            return ServiceResult<bool>.Failure("Add-on not found.");
        }

        var usageCount = await _repository.CountOrderUsageAsync(id);
        if (usageCount > 0)
        {
            return ServiceResult<bool>.Failure(
                $"Cannot delete this add-on because it appears on {usageCount} past order item(s).");
        }

        _repository.Remove(addOn);
        await _repository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private static AddOnResponse MapResponse(AddOn addOn) => new()
    {
        Id = addOn.Id,
        Name = addOn.Name,
        Price = addOn.Price
    };
}
