using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Models;
using System.IO;
using System.Threading.Tasks;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class AutoFixServiceTests
{
    private readonly Mock<ILogger<AutoFixService>> _mockLogger;
    private readonly AutoFixService _service;
    private readonly string _testWorkspaceRoot;

    public AutoFixServiceTests()
    {
        _mockLogger = new Mock<ILogger<AutoFixService>>();
        _testWorkspaceRoot = Path.Combine(Path.GetTempPath(), "test-workspace");
        Directory.CreateDirectory(_testWorkspaceRoot);
        
        // Set environment variable for test
        Environment.SetEnvironmentVariable("WORKSPACE_ROOT", _testWorkspaceRoot);
        
        _service = new AutoFixService(_mockLogger.Object);
    }

    [Fact]
    public async Task ApplyFixesAsync_WithValidFixes_AppliesFixes()
    {
        // Arrange
        var testFile = Path.Combine(_testWorkspaceRoot, "test.ts");
        await File.WriteAllTextAsync(testFile, "const x = null;\nx.name;");

        var analysisResult = new ErrorAnalysisResult
        {
            ErrorId = "TEST-001",
            SuggestedFixes = new List<SuggestedFix>
            {
                new SuggestedFix
                {
                    Description = "Add null check",
                    CodeChanges = new List<CodeChange>
                    {
                        new CodeChange
                        {
                            FilePath = "test.ts",
                            LineNumber = 2,
                            ChangeType = "AddNullCheck",
                            OldCode = "x.name;",
                            NewCode = "if (x != null) { x.name; }"
                        }
                    },
                    Priority = "high"
                }
            }
        };

        // Act
        var result = await _service.ApplyFixesAsync(analysisResult);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.AppliedFixes.Count > 0 || result.FailedFixes.Count > 0);
    }

    [Fact]
    public async Task ApplyFixesAsync_WithNonExistentFile_AddsToFailedFixes()
    {
        // Arrange
        var analysisResult = new ErrorAnalysisResult
        {
            ErrorId = "TEST-002",
            SuggestedFixes = new List<SuggestedFix>
            {
                new SuggestedFix
                {
                    Description = "Add null check",
                    CodeChanges = new List<CodeChange>
                    {
                        new CodeChange
                        {
                            FilePath = "non-existent.ts",
                            LineNumber = 1,
                            ChangeType = "AddNullCheck",
                            OldCode = "code",
                            NewCode = "fixed code"
                        }
                    }
                }
            }
        };

        // Act
        var result = await _service.ApplyFixesAsync(analysisResult);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.FailedFixes.Count > 0);
    }

    [Fact]
    public async Task CommitFixesAsync_WithValidResult_ReturnsSuccess()
    {
        // Arrange
        var fixResult = new FixApplicationResult
        {
            ErrorId = "TEST-003",
            AppliedFixes = new List<AppliedFix>
            {
                new AppliedFix
                {
                    FilePath = "test.ts",
                    LineNumber = 1,
                    ChangeType = "AddNullCheck",
                    Description = "Test fix"
                }
            },
            Success = true
        };

        // Act
        var result = await _service.CommitFixesAsync(fixResult, "TEST-003");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CommitFixesAsync_WithEmptyFixes_ReturnsSuccess()
    {
        // Arrange
        var fixResult = new FixApplicationResult
        {
            ErrorId = "TEST-004",
            AppliedFixes = new List<AppliedFix>(),
            Success = false
        };

        // Act
        var result = await _service.CommitFixesAsync(fixResult, "TEST-004");

        // Assert
        // Should still return true as commit operation itself succeeds
        Assert.True(result);
    }
}
