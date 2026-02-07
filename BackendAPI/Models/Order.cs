using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models;

public class Order
{
    public int Id { get; set; }

    public int AppUserId { get; set; }
    public AppUser? AppUser { get; set; }

    [Required]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal TotalAmount { get; set; }

    [Required]
    public string ShippingName { get; set; } = "";

    [Required]
    public string ShippingAddress { get; set; } = "";

    [Required]
    public string Phone { get; set; } = "";

    public List<OrderItem> Items { get; set; } = new();

    public string OrderNumber { get; set; } = "";

}
