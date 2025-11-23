namespace InterfaceConfigurator.Main.Models;

public class ErrorAnalysisResult
{
    public string ErrorId { get; set; } = string.Empty;
    public DateTime AnalysisTimestamp { get; set; }
    public List<AffectedFile> AffectedFiles { get; set; } = new();
    public RootCauseAnalysis RootCause { get; set; } = new();
    public List<SuggestedFix> SuggestedFixes { get; set; } = new();
    public double ConfidenceScore { get; set; }
}

public class AffectedFile
{
    public string FilePath { get; set; } = string.Empty;
    public int? LineNumber { get; set; }
    public int? ColumnNumber { get; set; }
    public string? FunctionName { get; set; }
    public string Severity { get; set; } = "medium";
}

public class RootCauseAnalysis
{
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = "Unknown";
    public List<string> LikelyCauses { get; set; } = new();
    public string ErrorPattern { get; set; } = string.Empty;
}

public class SuggestedFix
{
    public string Description { get; set; } = string.Empty;
    public List<CodeChange> CodeChanges { get; set; } = new();
    public string Priority { get; set; } = "medium";
    public string EstimatedImpact { get; set; } = string.Empty;
}

public class CodeChange
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string OldCode { get; set; } = string.Empty;
    public string NewCode { get; set; } = string.Empty;
}

public class AutoFixResult
{
    public string ErrorId { get; set; } = string.Empty;
    public DateTime FixTimestamp { get; set; }
    public bool Success { get; set; }
    public List<AppliedFix> AppliedFixes { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? CommitHash { get; set; }
    public bool TestsPassed { get; set; }
}

public class AppliedFix
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
}

public class FailedFix
{
    public CodeChange CodeChange { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

public class FixApplicationResult
{
    public string ErrorId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<AppliedFix> AppliedFixes { get; set; } = new();
    public List<FailedFix> FailedFixes { get; set; } = new();
    public bool Success { get; set; }
}

public class TestResult
{
    public string ErrorId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<TestRunResult> TestResults { get; set; } = new();
    public bool OverallSuccess { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class TestRunResult
{
    public string TestSuite { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string ErrorOutput { get; set; } = string.Empty;
    public int ExitCode { get; set; }
}

