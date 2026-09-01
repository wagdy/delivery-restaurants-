using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Push;

public class UnsubscribeRequest
{
    [Required]
    public string Endpoint { get; set; } = string.Empty;
}
