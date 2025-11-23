namespace InterfaceConfigurator.Main.Models;

/// <summary>
/// Login request model
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Login response model
/// </summary>
public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public UserInfo? User { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// User information (without password)
/// </summary>
public class UserInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Feature DTO for frontend
/// </summary>
public class FeatureDto
{
    public int Id { get; set; }
    public int FeatureNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DetailedDescription { get; set; } = string.Empty;
    public string? TechnicalDetails { get; set; }
    public string? TestInstructions { get; set; }
    public string? KnownIssues { get; set; }
    public string? Dependencies { get; set; }
    public string? BreakingChanges { get; set; }
    public string? Screenshots { get; set; }
    public string Category { get; set; } = "General";
    public string Priority { get; set; } = "Medium";
    public bool IsEnabled { get; set; }
    public DateTime ImplementedDate { get; set; }
    public DateTime? EnabledDate { get; set; }
    public string? EnabledBy { get; set; }
    public string? TestComment { get; set; } // Test result comment from tester - visible to all users
    public string? TestCommentBy { get; set; } // Username of the tester who wrote the comment
    public DateTime? TestCommentDate { get; set; } // Date when the test comment was last updated
    public bool CanToggle { get; set; } // Whether current user can toggle this feature
}

