namespace BackendAPI.Models;

public class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";


    public string Role { get; set; } = "User"; // default
}
