using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for transforming JSON using jq scripts
/// Uses node-jq to execute jq transformations
/// </summary>
public class JQTransformationService
{
    private readonly ILogger<JQTransformationService>? _logger;
    private readonly string _nodePath;
    private readonly string _jqScriptPath;

    public JQTransformationService(ILogger<JQTransformationService>? logger = null)
    {
        _logger = logger;
        
        // Try to find Node.js in common locations
        _nodePath = FindNodePath();
        
        // Path to the jq transformation script (will be created)
        _jqScriptPath = Path.Combine(Path.GetTempPath(), "jq-transform.js");
        
        EnsureJQScriptExists();
    }

    private string FindNodePath()
    {
        // Try common Node.js locations
        var possiblePaths = new[]
        {
            "node", // In PATH
            @"C:\Program Files\nodejs\node.exe",
            @"C:\Program Files (x86)\nodejs\node.exe",
            Environment.GetEnvironmentVariable("NODE_PATH") ?? string.Empty
        };

        foreach (var path in possiblePaths)
        {
            if (string.IsNullOrEmpty(path))
                continue;

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0)
                    {
                        _logger?.LogInformation("Found Node.js at: {NodePath}", path);
                        return path;
                    }
                }
            }
            catch
            {
                // Continue searching
            }
        }

        _logger?.LogWarning("Node.js not found. jq transformations will not work. Please install Node.js and ensure it's in PATH.");
        return "node"; // Fallback - will fail if not available
    }

    private void EnsureJQScriptExists()
    {
        if (File.Exists(_jqScriptPath))
            return;

        // Create a Node.js script that uses node-jq
        var scriptContent = @"
const jq = require('node-jq');
const fs = require('fs');

// Read input from command line arguments
const inputJson = process.argv[2];
const jqFilter = process.argv[3];
const jqScriptFile = process.argv[4];

let filter = jqFilter;

// If jqScriptFile is provided, read the filter from file
if (jqScriptFile && jqScriptFile !== '') {
    try {
        filter = fs.readFileSync(jqScriptFile, 'utf8');
    } catch (error) {
        console.error('Error reading jq script file:', error.message);
        process.exit(1);
    }
}

// Parse input JSON
let input;
try {
    input = JSON.parse(inputJson);
} catch (error) {
    console.error('Error parsing input JSON:', error.message);
    process.exit(1);
}

// Apply jq filter
jq.run(filter, input, { input: 'json', output: 'json' })
    .then((output) => {
        console.log(output);
    })
    .catch((error) => {
        console.error('jq transformation error:', error.message);
        process.exit(1);
    });
";

        try
        {
            File.WriteAllText(_jqScriptPath, scriptContent);
            _logger?.LogInformation("Created jq transformation script at: {ScriptPath}", _jqScriptPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating jq transformation script");
        }
    }

    /// <summary>
    /// Transforms JSON using a jq script file
    /// </summary>
    /// <param name="inputJson">Input JSON as string</param>
    /// <param name="jqScriptFile">Path or URI to jq script file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transformed JSON as string</returns>
    public async Task<string> TransformJsonAsync(
        string inputJson,
        string jqScriptFile,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jqScriptFile))
        {
            throw new ArgumentException("jq script file path cannot be empty", nameof(jqScriptFile));
        }

        if (string.IsNullOrWhiteSpace(inputJson))
        {
            return inputJson; // Return empty if input is empty
        }

        try
        {
            // Resolve script file path (handle URIs and local paths)
            var resolvedScriptPath = ResolveScriptPath(jqScriptFile);

            if (!File.Exists(resolvedScriptPath))
            {
                throw new FileNotFoundException($"jq script file not found: {resolvedScriptPath}");
            }

            // Read the jq filter from the file
            var jqFilter = await File.ReadAllTextAsync(resolvedScriptPath, cancellationToken);

            if (string.IsNullOrWhiteSpace(jqFilter))
            {
                throw new InvalidOperationException($"jq script file is empty: {resolvedScriptPath}");
            }

            // Execute jq transformation using Node.js
            var processInfo = new ProcessStartInfo
            {
                FileName = _nodePath,
                Arguments = $"\"{_jqScriptPath}\" \"{inputJson.Replace("\"", "\\\"")}\" \"{jqFilter.Replace("\"", "\\\"")}\" \"{resolvedScriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Node.js process for jq transformation");
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to complete with timeout
            var completed = await Task.Run(() => process.WaitForExit(30000), cancellationToken);

            if (!completed)
            {
                process.Kill();
                throw new TimeoutException("jq transformation timed out after 30 seconds");
            }

            if (process.ExitCode != 0)
            {
                var errorMessage = errorBuilder.ToString();
                _logger?.LogError("jq transformation failed with exit code {ExitCode}: {Error}", process.ExitCode, errorMessage);
                throw new InvalidOperationException($"jq transformation failed: {errorMessage}");
            }

            var result = outputBuilder.ToString().Trim();
            
            // Validate that result is valid JSON
            try
            {
                JsonDocument.Parse(result);
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "jq transformation returned invalid JSON: {Result}", result);
                throw new InvalidOperationException($"jq transformation returned invalid JSON: {ex.Message}");
            }

            _logger?.LogInformation("Successfully transformed JSON using jq script: {ScriptFile}", jqScriptFile);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error transforming JSON with jq: {ScriptFile}", jqScriptFile);
            throw;
        }
    }

    /// <summary>
    /// Resolves script file path from URI or local path
    /// </summary>
    private string ResolveScriptPath(string scriptPathOrUri)
    {
        // If it's a URI, try to download it or resolve it
        if (Uri.TryCreate(scriptPathOrUri, UriKind.Absolute, out var uri))
        {
            // For file:// URIs, convert to local path
            if (uri.Scheme == "file")
            {
                return uri.LocalPath;
            }

            // For HTTP/HTTPS URIs, download to temp file (could be enhanced)
            // For now, throw an exception - could be enhanced to download
            throw new NotSupportedException($"HTTP/HTTPS URIs for jq scripts are not yet supported. Please use a local file path or file:// URI. Provided: {scriptPathOrUri}");
        }

        // Assume it's a local path
        if (Path.IsPathRooted(scriptPathOrUri))
        {
            return scriptPathOrUri;
        }

        // Relative path - resolve relative to current directory or temp
        return Path.GetFullPath(scriptPathOrUri);
    }
}


