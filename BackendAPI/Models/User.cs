using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [MinLength(2)]
    public string Name { get; set; } = "";
}
