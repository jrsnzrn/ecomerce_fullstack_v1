using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models;

public class Category
{
    public int Id { get; set; }

    [Required, MinLength(2)]
    public string Name { get; set; } = "";

    public List<Product> Products { get; set; } = new();
}
