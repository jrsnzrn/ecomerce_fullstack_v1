using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models;

public class Product
{
    public int Id { get; set; }

    [Required, MinLength(2)]
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    [Range(0, 999999999)]
    public decimal Price { get; set; }

    [Range(0, 999999999)]
    public int StockQty { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    // FK
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}
