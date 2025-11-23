namespace InterfaceConfigurator.Main.Models;

// Error report model matching frontend structure
public class ErrorReport
{
    public string ErrorId { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public string UserAgent { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<FunctionCall> FunctionCallHistory { get; set; } = new();
    public CurrentErrorInfo? CurrentError { get; set; }
    public Dictionary<string, object> ApplicationState { get; set; } = new();
    public EnvironmentInfo? Environment { get; set; }
}

public class FunctionCall
{
    public string FunctionName { get; set; } = string.Empty;
    public string? Component { get; set; }
    public long Timestamp { get; set; }
    public object? Parameters { get; set; }
    public object? ReturnValue { get; set; }
    public double? Duration { get; set; }
    public bool Success { get; set; }
    public ErrorInfo? Error { get; set; }
}

public class CurrentErrorInfo
{
    public string FunctionName { get; set; } = string.Empty;
    public string? Component { get; set; }
    public ErrorInfo? Error { get; set; }
    public string Stack { get; set; } = string.Empty;
    public object? Context { get; set; }
}

public class ErrorInfo
{
    public string Message { get; set; } = string.Empty;
    public string? Stack { get; set; }
    public string? Name { get; set; }
    public object? Details { get; set; }
}

public class EnvironmentInfo
{
    public string ApiUrl { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}



