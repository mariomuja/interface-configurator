using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for managing features and feature flags
/// Now uses MessageBoxDbContext (moved from ApplicationDbContext)
/// </summary>
public class FeatureService
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<FeatureService> _logger;

    public FeatureService(MessageBoxDbContext context, ILogger<FeatureService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all features ordered by implementation date (newest first)
    /// </summary>
    public async Task<List<FeatureDto>> GetAllFeaturesAsync(string? currentUserRole = null)
    {
        var features = await _context.Features
            .OrderByDescending(f => f.ImplementedDate)
            .ToListAsync();

        return features.Select(f => new FeatureDto
        {
            Id = f.Id,
            FeatureNumber = f.FeatureNumber,
            Title = f.Title,
            Description = f.Description,
            DetailedDescription = f.DetailedDescription,
            TechnicalDetails = f.TechnicalDetails,
            TestInstructions = f.TestInstructions,
            KnownIssues = f.KnownIssues,
            Dependencies = f.Dependencies,
            BreakingChanges = f.BreakingChanges,
            Screenshots = f.Screenshots,
            Category = f.Category,
            Priority = f.Priority,
            IsEnabled = f.IsEnabled,
            ImplementedDate = f.ImplementedDate,
            EnabledDate = f.EnabledDate,
            EnabledBy = f.EnabledBy,
            TestComment = f.TestComment,
            TestCommentBy = f.TestCommentBy,
            TestCommentDate = f.TestCommentDate,
            CanToggle = currentUserRole == "admin"
        }).ToList();
    }

    /// <summary>
    /// Gets a feature by ID
    /// </summary>
    public async Task<Feature?> GetFeatureByIdAsync(int id)
    {
        return await _context.Features.FindAsync(id);
    }

    /// <summary>
    /// Creates a new feature with detailed information
    /// </summary>
    public async Task<Feature> CreateFeatureAsync(
        string title,
        string description,
        string detailedDescription,
        string? technicalDetails = null,
        string? testInstructions = null,
        string? knownIssues = null,
        string? dependencies = null,
        string? breakingChanges = null,
        string? screenshots = null,
        string category = "General",
        string priority = "Medium",
        string? implementationDetails = null)
    {
        // Get next feature number
        var maxFeatureNumber = await _context.Features
            .Select(f => f.FeatureNumber)
            .DefaultIfEmpty(0)
            .MaxAsync();

        var feature = new Feature
        {
            FeatureNumber = maxFeatureNumber + 1,
            Title = title,
            Description = description,
            DetailedDescription = detailedDescription,
            TechnicalDetails = technicalDetails,
            TestInstructions = testInstructions,
            KnownIssues = knownIssues,
            Dependencies = dependencies,
            BreakingChanges = breakingChanges,
            Screenshots = screenshots,
            Category = category,
            Priority = priority,
            IsEnabled = false, // Default: disabled
            ImplementedDate = DateTime.UtcNow,
            ImplementationDetails = implementationDetails
        };

        _context.Features.Add(feature);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created feature #{FeatureNumber}: {Title}", feature.FeatureNumber, feature.Title);
        return feature;
    }

    /// <summary>
    /// Toggles feature enabled state (admin only)
    /// </summary>
    public async Task<bool> ToggleFeatureAsync(int featureId, string enabledBy)
    {
        var feature = await _context.Features.FindAsync(featureId);
        if (feature == null)
        {
            return false;
        }

        feature.IsEnabled = !feature.IsEnabled;
        feature.EnabledDate = feature.IsEnabled ? DateTime.UtcNow : null;
        feature.EnabledBy = feature.IsEnabled ? enabledBy : null;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Feature #{FeatureNumber} {State} by {User}",
            feature.FeatureNumber,
            feature.IsEnabled ? "enabled" : "disabled",
            enabledBy);

        return true;
    }

    /// <summary>
    /// Checks if a feature is enabled for all users
    /// </summary>
    public async Task<bool> IsFeatureEnabledAsync(int featureNumber)
    {
        var feature = await _context.Features
            .FirstOrDefaultAsync(f => f.FeatureNumber == featureNumber);

        return feature?.IsEnabled ?? false;
    }

    /// <summary>
    /// Updates the test comment for a feature (all users can add comments)
    /// </summary>
    public async Task<bool> UpdateTestCommentAsync(int featureId, string testComment, string commentedBy)
    {
        var feature = await _context.Features.FindAsync(featureId);
        if (feature == null)
        {
            return false;
        }

        feature.TestComment = testComment;
        feature.TestCommentBy = commentedBy;
        feature.TestCommentDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Test comment updated for feature #{FeatureNumber} by {User}",
            feature.FeatureNumber,
            commentedBy);

        return true;
    }
}

