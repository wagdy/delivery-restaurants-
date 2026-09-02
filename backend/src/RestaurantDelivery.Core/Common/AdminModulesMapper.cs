using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Common;

// Converts between the [Flags] AdminModules enum and a List<string> of module names -
// used at every boundary (JSON DTOs, JWT claims) instead of relying on the enum's own
// ToString()/Parse round-trip, which produces awkward comma-joined strings for a
// multi-flag value and serializes 0/None ambiguously.
public static class AdminModulesMapper
{
    public static List<string> ToNames(AdminModules modules) =>
        Enum.GetValues<AdminModules>()
            .Where(m => m != AdminModules.None && modules.HasFlag(m))
            .Select(m => m.ToString())
            .ToList();

    public static ServiceResult<AdminModules> FromNames(IEnumerable<string> names)
    {
        var result = AdminModules.None;

        foreach (var name in names)
        {
            if (!Enum.TryParse<AdminModules>(name, ignoreCase: true, out var parsed) || parsed == AdminModules.None)
            {
                return ServiceResult<AdminModules>.Failure($"'{name}' is not a recognized admin module.");
            }

            result |= parsed;
        }

        return ServiceResult<AdminModules>.Success(result);
    }
}
