using Microsoft.Extensions.Logging;
using System.Linq;
using InterfaceConfigurator.Main.Core.Services;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Service for data quality validation and scoring
/// </summary>
public class DataQualityService
{
    private readonly ILogger<DataQualityService>? _logger;
    private readonly SchemaRegistryService? _schemaRegistry;

    public DataQualityService(
        ILogger<DataQualityService>? logger = null,
        SchemaRegistryService? schemaRegistry = null)
    {
        _logger = logger;
        _schemaRegistry = schemaRegistry;
    }

    /// <summary>
    /// Validate record and generate quality report
    /// </summary>
    public async Task<DataQualityReport> ValidateRecordAsync(
        Dictionary<string, string> record,
        DataQualityRules rules,
        string? interfaceName = null,
        CancellationToken cancellationToken = default)
    {
        var report = new DataQualityReport
        {
            Record = record,
            Timestamp = DateTime.UtcNow,
            Issues = new List<DataQualityIssue>()
        };

        // Schema validation if schema registry is available
        if (_schemaRegistry != null && !string.IsNullOrWhiteSpace(interfaceName))
        {
            var schemaValidation = await _schemaRegistry.ValidateRecordAsync(record, interfaceName, cancellationToken);
            if (!schemaValidation.IsValid)
            {
                foreach (var error in schemaValidation.Errors)
                {
                    report.Issues.Add(new DataQualityIssue
                    {
                        Severity = "Error",
                        Field = "Schema",
                        Message = error
                    });
                }
            }
        }

        // Completeness check
        if (rules.CheckCompleteness)
        {
            var completeness = CalculateCompleteness(record, rules.RequiredFields);
            report.CompletenessScore = completeness;
            
            if (completeness < rules.MinCompletenessScore)
            {
                report.Issues.Add(new DataQualityIssue
                {
                    Severity = "Warning",
                    Field = "Completeness",
                    Message = $"Completeness score {completeness:P} is below minimum {rules.MinCompletenessScore:P}"
                });
            }
        }

        // Format compliance check
        if (rules.CheckFormatCompliance)
        {
            foreach (var field in record.Keys)
            {
                var formatIssues = CheckFormatCompliance(field, record[field], rules);
                report.Issues.AddRange(formatIssues);
            }
        }

        // Business rules check
        if (rules.BusinessRules != null && rules.BusinessRules.Count > 0)
        {
            foreach (var rule in rules.BusinessRules)
            {
                var ruleIssues = CheckBusinessRule(record, rule);
                report.Issues.AddRange(ruleIssues);
            }
        }

        // Calculate overall quality score
        report.QualityScore = CalculateQualityScore(report, rules);
        report.IsValid = report.QualityScore >= rules.MinQualityScore && 
                        report.Issues.Count(i => i.Severity == "Error") == 0;

        return report;
    }

    private double CalculateCompleteness(
        Dictionary<string, string> record,
        List<string>? requiredFields)
    {
        if (requiredFields == null || requiredFields.Count == 0)
            return 1.0; // No required fields = 100% complete

        var presentFields = requiredFields.Count(field => 
            record.ContainsKey(field) && !string.IsNullOrWhiteSpace(record[field]));

        return (double)presentFields / requiredFields.Count;
    }

    private List<DataQualityIssue> CheckFormatCompliance(
        string fieldName,
        string value,
        DataQualityRules rules)
    {
        var issues = new List<DataQualityIssue>();

        if (string.IsNullOrWhiteSpace(value))
            return issues;

        // Check email format
        if (rules.EmailFields?.Contains(fieldName) == true)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, 
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                issues.Add(new DataQualityIssue
                {
                    Severity = "Error",
                    Field = fieldName,
                    Message = $"Invalid email format: {value}"
                });
            }
        }

        // Check phone format
        if (rules.PhoneFields?.Contains(fieldName) == true)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, 
                @"^[\d\s\-\+\(\)]+$"))
            {
                issues.Add(new DataQualityIssue
                {
                    Severity = "Warning",
                    Field = fieldName,
                    Message = $"Invalid phone format: {value}"
                });
            }
        }

        // Check date format
        if (rules.DateFields?.Contains(fieldName) == true)
        {
            if (!DateTime.TryParse(value, out _))
            {
                issues.Add(new DataQualityIssue
                {
                    Severity = "Error",
                    Field = fieldName,
                    Message = $"Invalid date format: {value}"
                });
            }
        }

        return issues;
    }

    private List<DataQualityIssue> CheckBusinessRule(
        Dictionary<string, string> record,
        BusinessRule rule)
    {
        var issues = new List<DataQualityIssue>();

        // Simple rule evaluation - can be extended
        if (rule.Type == "RequiredIf")
        {
            var conditionField = rule.ConditionField;
            var conditionValue = rule.ConditionValue;
            
            if (record.TryGetValue(conditionField, out var actualValue) && 
                actualValue == conditionValue)
            {
                if (!record.ContainsKey(rule.TargetField) || 
                    string.IsNullOrWhiteSpace(record[rule.TargetField]))
                {
                    issues.Add(new DataQualityIssue
                    {
                        Severity = "Error",
                        Field = rule.TargetField,
                        Message = rule.Description ?? $"Field '{rule.TargetField}' is required when '{conditionField}' = '{conditionValue}'"
                    });
                }
            }
        }

        return issues;
    }

    private double CalculateQualityScore(DataQualityReport report, DataQualityRules rules)
    {
        var score = 100.0;

        // Deduct points for issues
        foreach (var issue in report.Issues)
        {
            score -= issue.Severity switch
            {
                "Error" => 10.0,
                "Warning" => 5.0,
                "Info" => 1.0,
                _ => 0.0
            };
        }

        // Apply completeness score
        if (rules.CheckCompleteness)
        {
            score = (score + report.CompletenessScore * 100) / 2;
        }

        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// Detect anomalies in a dataset
    /// </summary>
    public DataQualityReport DetectAnomalies(
        List<Dictionary<string, string>> records,
        string? fieldName = null)
    {
        if (records == null || records.Count == 0)
        {
            return new DataQualityReport
            {
                QualityScore = 0,
                Issues = new List<DataQualityIssue> { new DataQualityIssue
                {
                    Severity = "Warning",
                    Message = "No records provided for anomaly detection"
                }}
            };
        }

        var report = new DataQualityReport
        {
            Timestamp = DateTime.UtcNow,
            Issues = new List<DataQualityIssue>()
        };

        // Simple anomaly detection: check for outliers in numeric fields
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            var numericValues = records
                .Where(r => r.ContainsKey(fieldName))
                .Select(r => r[fieldName])
                .Where(v => double.TryParse(v, out _))
                .Select(v => double.Parse(v))
                .ToList();

            if (numericValues.Count > 0)
            {
                var mean = numericValues.Average();
                var stdDev = Math.Sqrt(numericValues.Select(x => Math.Pow(x - mean, 2)).Average());
                var threshold = mean + (3 * stdDev); // 3-sigma rule

                var outliers = numericValues.Where(v => Math.Abs(v - mean) > threshold).ToList();
                if (outliers.Count > 0)
                {
                    report.Issues.Add(new DataQualityIssue
                    {
                        Severity = "Warning",
                        Field = fieldName,
                        Message = $"Detected {outliers.Count} outliers in field '{fieldName}'"
                    });
                }
            }
        }

        return report;
    }
}

public class DataQualityRules
{
    public bool CheckCompleteness { get; set; } = true;
    public bool CheckFormatCompliance { get; set; } = true;
    public double MinCompletenessScore { get; set; } = 0.8; // 80%
    public double MinQualityScore { get; set; } = 70.0; // 70/100
    public List<string>? RequiredFields { get; set; }
    public List<string>? EmailFields { get; set; }
    public List<string>? PhoneFields { get; set; }
    public List<string>? DateFields { get; set; }
    public List<BusinessRule>? BusinessRules { get; set; }
}

public class BusinessRule
{
    public string Type { get; set; } = string.Empty; // RequiredIf, Range, Pattern, etc.
    public string ConditionField { get; set; } = string.Empty;
    public string? ConditionValue { get; set; }
    public string TargetField { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class DataQualityReport
{
    public Dictionary<string, string>? Record { get; set; }
    public DateTime Timestamp { get; set; }
    public double QualityScore { get; set; }
    public double CompletenessScore { get; set; }
    public bool IsValid { get; set; }
    public List<DataQualityIssue> Issues { get; set; } = new();
}

public class DataQualityIssue
{
    public string Severity { get; set; } = "Info"; // Error, Warning, Info
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
