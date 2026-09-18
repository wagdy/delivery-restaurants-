namespace RestaurantDelivery.Core.DTOs.Common;

// What a bulk operation actually did. Requested and Affected differ whenever an id in
// the selection no longer exists - deleted by someone else between the page loading and
// the button being pressed - and the admin should be told that rather than shown a
// count that quietly includes rows nothing happened to.
public class BulkActionResult
{
    public int Requested { get; set; }
    public int Affected { get; set; }
}

public class BulkIdsRequest
{
    public List<int> Ids { get; set; } = new();
}

public class BulkAvailabilityRequest : BulkIdsRequest
{
    public bool IsAvailable { get; set; }
}
