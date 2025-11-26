namespace InterfaceConfigurator.Main.Models;

/// <summary>
/// Represents a feature that can be enabled or disabled for testers/users.
/// </summary>
public class Feature
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
    public bool IsEnabled { get; set; } = false;
    public DateTime ImplementedDate { get; set; } = DateTime.UtcNow;
    public DateTime? EnabledDate { get; set; }
    public string? EnabledBy { get; set; }
    public string? ImplementationDetails { get; set; }
    public string? TestComment { get; set; }
    public string? TestCommentBy { get; set; }
    public DateTime? TestCommentDate { get; set; }
}

