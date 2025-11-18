namespace ProcessCsvBlobTrigger.Core.Models;

/// <summary>
/// Represents a single adapter setting (for JSON serialization)
/// </summary>
public class AdapterSetting
{
    public string AdapterName { get; set; } = string.Empty;
    public string AdapterType { get; set; } = string.Empty; // "Source" or "Destination"
    public string SettingKey { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
    public string? Description { get; set; }
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;
    public DateTime? datetime_updated { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Container for all adapter settings (for JSON serialization)
/// </summary>
public class AdapterSettingsContainer
{
    public List<AdapterSetting> Settings { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}






