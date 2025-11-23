using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Models;
using System.Security.Cryptography;
using System.Text;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for user authentication and authorization
/// Now uses MessageBoxDbContext (moved from ApplicationDbContext)
/// </summary>
public class AuthService
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<AuthService> _logger;

    public AuthService(MessageBoxDbContext context, ILogger<AuthService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates a user and returns user info
    /// </summary>
    public async Task<UserInfo?> AuthenticateAsync(string username, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
        {
            _logger.LogWarning("Authentication failed: User not found: {Username}", username);
            return null;
        }

        if (!VerifyPassword(password, user.PasswordHash))
        {
            _logger.LogWarning("Authentication failed: Invalid password for user: {Username}", username);
            return null;
        }

        // Update last login
        user.LastLoginDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User authenticated: {Username}, Role: {Role}", username, user.Role);

        return new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role
        };
    }

    /// <summary>
    /// Creates a new user
    /// </summary>
    public async Task<UserInfo?> CreateUserAsync(string username, string password, string role = "user")
    {
        if (await _context.Users.AnyAsync(u => u.Username == username))
        {
            _logger.LogWarning("User creation failed: Username already exists: {Username}", username);
            return null;
        }

        var user = new User
        {
            Username = username,
            PasswordHash = HashPassword(password),
            Role = role,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User created: {Username}, Role: {Role}", username, role);

        return new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role
        };
    }

    /// <summary>
    /// Gets user by username (for demo user login without password)
    /// </summary>
    public async Task<UserInfo?> GetUserAsync(string username)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
            return null;

        // Update last login
        user.LastLoginDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Demo user logged in: {Username}, Role: {Role}", username, user.Role);

        return new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role
        };
    }

    /// <summary>
    /// Hashes a password using BCrypt (simplified - using SHA256 for now)
    /// In production, use BCrypt.Net or similar
    /// </summary>
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    /// <summary>
    /// Verifies a password against a hash
    /// </summary>
    private bool VerifyPassword(string password, string hash)
    {
        var passwordHash = HashPassword(password);
        return passwordHash == hash;
    }

    /// <summary>
    /// Initializes default users (admin and test user)
    /// </summary>
    public async Task InitializeDefaultUsersAsync()
    {
        // Create admin user if it doesn't exist
        if (!await _context.Users.AnyAsync(u => u.Username == "admin"))
        {
            await CreateUserAsync("admin", "admin123", "admin");
            _logger.LogInformation("Default admin user created");
        }

        // Create test user if it doesn't exist
        if (!await _context.Users.AnyAsync(u => u.Username == "test"))
        {
            await CreateUserAsync("test", "test123", "user");
            _logger.LogInformation("Default test user created");
        }
    }
}

