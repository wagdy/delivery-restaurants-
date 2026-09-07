using System.Text.Json;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Roles;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _repository;

    public RoleService(IRoleRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<RoleResponse>> GetAllAsync()
    {
        var roles = await _repository.GetAllOrderedAsync();
        return roles.Select(MapResponse).ToList();
    }

    public async Task<ServiceResult<RoleResponse>> CreateAsync(RoleRequest request)
    {
        var name = request.Name.Trim();

        if (await _repository.GetByNameAsync(name) is not null)
        {
            return ServiceResult<RoleResponse>.Failure("A role with this name already exists.");
        }

        var modulesResult = AdminModulesMapper.FromNames(request.Modules);
        if (!modulesResult.Succeeded)
        {
            return ServiceResult<RoleResponse>.Failure(modulesResult.Errors.ToArray());
        }

        var permissionsResult = ValidateGranularPermissions(request.GranularPermissions, modulesResult.Data);
        if (!permissionsResult.Succeeded)
        {
            return ServiceResult<RoleResponse>.Failure(permissionsResult.Errors.ToArray());
        }

        var role = new Role
        {
            Name = name,
            Modules = modulesResult.Data,
            GranularPermissionsJson = SerializePermissions(permissionsResult.Data)
        };
        await _repository.AddAsync(role);
        await _repository.SaveChangesAsync();

        return ServiceResult<RoleResponse>.Success(MapResponse(role));
    }

    public async Task<ServiceResult<RoleResponse>> UpdateAsync(int id, RoleRequest request)
    {
        var role = await _repository.GetByIdAsync(id);
        if (role is null)
        {
            return ServiceResult<RoleResponse>.Failure("Role not found.");
        }

        var name = request.Name.Trim();

        var existing = await _repository.GetByNameAsync(name);
        if (existing is not null && existing.Id != id)
        {
            return ServiceResult<RoleResponse>.Failure("A role with this name already exists.");
        }

        var modulesResult = AdminModulesMapper.FromNames(request.Modules);
        if (!modulesResult.Succeeded)
        {
            return ServiceResult<RoleResponse>.Failure(modulesResult.Errors.ToArray());
        }

        var permissionsResult = ValidateGranularPermissions(request.GranularPermissions, modulesResult.Data);
        if (!permissionsResult.Succeeded)
        {
            return ServiceResult<RoleResponse>.Failure(permissionsResult.Errors.ToArray());
        }

        role.Name = name;
        role.Modules = modulesResult.Data;
        role.GranularPermissionsJson = SerializePermissions(permissionsResult.Data);
        await _repository.SaveChangesAsync();

        return ServiceResult<RoleResponse>.Success(MapResponse(role));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var role = await _repository.GetByIdAsync(id);
        if (role is null)
        {
            return ServiceResult<bool>.Failure("Role not found.");
        }

        var staffCount = await _repository.CountAssignedStaffAsync(id);
        if (staffCount > 0)
        {
            return ServiceResult<bool>.Failure(
                $"Cannot delete this role because {staffCount} staff account(s) are assigned to it. Reassign them first.");
        }

        _repository.Remove(role);
        await _repository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    // Rejects unknown permission strings outright, and rejects a permission whose parent
    // module isn't in the role's own module list - a role can't be granted "Orders.Create"
    // without also having the "Orders" module, since the sub-permission is meaningless
    // without the parent tab it narrows.
    private static ServiceResult<List<string>> ValidateGranularPermissions(List<string> permissions, AdminModules modules)
    {
        var distinct = permissions.Distinct().ToList();

        foreach (var permission in distinct)
        {
            if (!GranularPermissions.IsValid(permission))
            {
                return ServiceResult<List<string>>.Failure($"'{permission}' is not a recognized permission.");
            }

            var parentModule = GranularPermissions.ModuleFor(permission)!.Value;
            if (!modules.HasFlag(parentModule))
            {
                return ServiceResult<List<string>>.Failure(
                    $"Cannot grant '{permission}' without also granting the '{parentModule}' module.");
            }
        }

        return ServiceResult<List<string>>.Success(distinct);
    }

    private static string? SerializePermissions(List<string> permissions) =>
        permissions.Count == 0 ? null : JsonSerializer.Serialize(permissions);

    private static List<string> DeserializePermissions(string? json) =>
        string.IsNullOrEmpty(json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

    private static RoleResponse MapResponse(Role role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Modules = AdminModulesMapper.ToNames(role.Modules),
        GranularPermissions = DeserializePermissions(role.GranularPermissionsJson)
    };
}
