namespace InterfaceConfigurator.Main.Models;

/// <summary>
/// Represents a feature that can be enabled/disabled for users
/// </summary>
public class Feature
{
    public int Id { get; set; }
    public int FeatureNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description for testers to understand what the feature does
    /// This should be comprehensive and help testers make informed decisions
    /// </summary>
    public string DetailedDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Technical implementation details (for developers/testers)
    /// </summary>
    public string? TechnicalDetails { get; set; }
    
    /// <summary>
    /// Step-by-step instructions on how to test this feature
    /// Should include test scenarios, expected results, and edge cases
    /// </summary>
    public string? TestInstructions { get; set; }
    
    /// <summary>
    /// Known issues, limitations, or workarounds
    /// Important information for testers before enabling
    /// </summary>
    public string? KnownIssues { get; set; }
    
    /// <summary>
    /// Dependencies on other features, systems, or components
    /// </summary>
    public string? Dependencies { get; set; }
    
    /// <summary>
    /// Breaking changes or migration notes
    /// Critical information if this feature changes existing behavior
    /// </summary>
    public string? BreakingChanges { get; set; }
    
    /// <summary>
    /// JSON array of screenshot URLs or paths
    /// </summary>
    public string? Screenshots { get; set; }
    
    /// <summary>
    /// Feature category (UI, Backend, Integration, Security, etc.)
    /// </summary>
    public string Category { get; set; } = "General";
    
    /// <summary>
    /// Priority level: Low, Medium, High, Critical
    /// </summary>
    public string Priority { get; set; } = "Medium";
    
    public bool IsEnabled { get; set; } = false; // Default: disabled for all users
    public DateTime ImplementedDate { get; set; } = DateTime.UtcNow;
    public DateTime? EnabledDate { get; set; }
    public string? EnabledBy { get; set; }
    public string? ImplementationDetails { get; set; }
    
    /// <summary>
    /// Test result comment from tester - visible to all users
    /// Explains why a feature is not yet released
    /// </summary>
    public string? TestComment { get; set; }
    
    /// <summary>
    /// Username of the tester who wrote the comment
    /// </summary>
    public string? TestCommentBy { get; set; }
    
    /// <summary>
    /// Date when the test comment was last updated
    /// </summary>
    public DateTime? TestCommentDate { get; set; }
}

