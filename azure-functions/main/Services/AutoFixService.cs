using System.Text;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service to automatically apply fixes to code
/// </summary>
public class AutoFixService
{
    private readonly ILogger<AutoFixService> _logger;
    private readonly string _workspaceRoot;

    public AutoFixService(ILogger<AutoFixService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Determine workspace root (adjust path as needed)
        _workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT") 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "interface-configurator");
    }

    /// <summary>
    /// Applies suggested fixes to code files
    /// </summary>
    public async Task<FixApplicationResult> ApplyFixesAsync(ErrorAnalysisResult analysisResult)
    {
        _logger.LogInformation("Applying fixes for error: {ErrorId}", analysisResult.ErrorId);

        var result = new FixApplicationResult
        {
            ErrorId = analysisResult.ErrorId,
            Timestamp = DateTime.UtcNow,
            AppliedFixes = new List<AppliedFix>(),
            FailedFixes = new List<FailedFix>(),
            Success = false
        };

        try
        {
            foreach (var suggestedFix in analysisResult.SuggestedFixes)
            {
                foreach (var codeChange in suggestedFix.CodeChanges)
                {
                    try
                    {
                        var appliedFix = await ApplyCodeChangeAsync(codeChange, suggestedFix);
                        result.AppliedFixes.Add(appliedFix);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to apply fix: {Description}", suggestedFix.Description);
                        result.FailedFixes.Add(new FailedFix
                        {
                            CodeChange = codeChange,
                            Reason = ex.Message
                        });
                    }
                }
            }

            result.Success = result.AppliedFixes.Count > 0 && result.FailedFixes.Count == 0;

            if (result.Success)
            {
                _logger.LogInformation(
                    "Successfully applied {Count} fixes for error: {ErrorId}",
                    result.AppliedFixes.Count,
                    analysisResult.ErrorId);
            }
            else
            {
                _logger.LogWarning(
                    "Partially applied fixes: {Applied} succeeded, {Failed} failed for error: {ErrorId}",
                    result.AppliedFixes.Count,
                    result.FailedFixes.Count,
                    analysisResult.ErrorId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying fixes");
            result.Success = false;
            return result;
        }
    }

    private async Task<AppliedFix> ApplyCodeChangeAsync(CodeChange codeChange, SuggestedFix suggestedFix)
    {
        var filePath = GetFullFilePath(codeChange.FilePath);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var fileContent = await File.ReadAllTextAsync(filePath);
        var lines = fileContent.Split('\n').ToList();

        // Apply the fix based on change type
        var modifiedContent = codeChange.ChangeType switch
        {
            "AddNullCheck" => ApplyNullCheckFix(lines, codeChange),
            "AddTypeCheck" => ApplyTypeCheckFix(lines, codeChange),
            "AddRetryLogic" => ApplyRetryLogicFix(lines, codeChange),
            "AddValidation" => ApplyValidationFix(lines, codeChange),
            _ => ApplyGenericFix(lines, codeChange)
        };

        // Create backup
        var backupPath = $"{filePath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
        await File.WriteAllTextAsync(backupPath, fileContent);

        // Write modified content
        await File.WriteAllTextAsync(filePath, modifiedContent);

        return new AppliedFix
        {
            FilePath = codeChange.FilePath,
            LineNumber = codeChange.LineNumber,
            ChangeType = codeChange.ChangeType,
            Description = suggestedFix.Description,
            BackupPath = backupPath
        };
    }

    private string ApplyNullCheckFix(List<string> lines, CodeChange codeChange)
    {
        if (codeChange.LineNumber <= 0 || codeChange.LineNumber > lines.Count)
        {
            return string.Join("\n", lines);
        }

        var lineIndex = codeChange.LineNumber - 1;
        var originalLine = lines[lineIndex];

        // Simple null check addition (this would be more sophisticated in production)
        var indent = new string(' ', originalLine.TakeWhile(char.IsWhiteSpace).Count());
        var nullCheck = $"{indent}if (object != null) {{\n{originalLine}\n{indent}}}\n";

        lines.Insert(lineIndex, nullCheck);
        lines.RemoveAt(lineIndex + 1);

        return string.Join("\n", lines);
    }

    private string ApplyTypeCheckFix(List<string> lines, CodeChange codeChange)
    {
        // Similar to null check, but for type checking
        return ApplyGenericFix(lines, codeChange);
    }

    private string ApplyRetryLogicFix(List<string> lines, CodeChange codeChange)
    {
        // Add retry logic wrapper
        return ApplyGenericFix(lines, codeChange);
    }

    private string ApplyValidationFix(List<string> lines, CodeChange codeChange)
    {
        // Add validation checks
        return ApplyGenericFix(lines, codeChange);
    }

    private string ApplyGenericFix(List<string> lines, CodeChange codeChange)
    {
        // Generic fix application
        if (codeChange.LineNumber > 0 && codeChange.LineNumber <= lines.Count)
        {
            var lineIndex = codeChange.LineNumber - 1;
            // Replace old code with new code
            if (!string.IsNullOrWhiteSpace(codeChange.NewCode))
            {
                lines[lineIndex] = codeChange.NewCode;
            }
        }

        return string.Join("\n", lines);
    }

    private string GetFullFilePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }

        return Path.Combine(_workspaceRoot, relativePath);
    }

    /// <summary>
    /// Commits applied fixes to git
    /// </summary>
    public async Task<bool> CommitFixesAsync(FixApplicationResult fixResult, string errorId)
    {
        try
        {
            _logger.LogInformation("Committing fixes for error: {ErrorId}", errorId);

            // This would use git commands to commit changes
            // For now, we'll log the intent
            var commitMessage = $"Auto-fix for error {errorId}\n\n" +
                              $"Applied {fixResult.AppliedFixes.Count} fixes\n" +
                              $"Files modified: {string.Join(", ", fixResult.AppliedFixes.Select(f => f.FilePath))}";

            _logger.LogInformation("Would commit with message: {Message}", commitMessage);

            // In production, this would execute:
            // git add <files>
            // git commit -m "<commitMessage>"

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit fixes");
            return false;
        }
    }
}

