namespace InterfaceConfigurator.Main.Models;

/// <summary>
/// Represents a user with role-based access control
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // BCrypt hash
    public string Role { get; set; } = "user"; // "admin" or "user"
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }
    public bool IsActive { get; set; } = true;
}


