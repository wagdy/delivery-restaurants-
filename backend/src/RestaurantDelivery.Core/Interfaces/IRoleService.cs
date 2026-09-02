using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Roles;

namespace RestaurantDelivery.Core.Interfaces;

public interface IRoleService
{
    Task<List<RoleResponse>> GetAllAsync();
    Task<ServiceResult<RoleResponse>> CreateAsync(RoleRequest request);
    Task<ServiceResult<RoleResponse>> UpdateAsync(int id, RoleRequest request);
    Task<ServiceResult<bool>> DeleteAsync(int id);
}
