namespace InterfaceConfigurator.Main.Core.Interfaces;

public interface ILoggingService
{
    Task LogAsync(string level, string message, string? details = null, CancellationToken cancellationToken = default);
}

