# PowerShell script to fix SqlServerAdapter constructor calls in test files
# Adds missing tableName and statisticsService parameters

$testFiles = @(
    "Adapters\SqlServerAdapterEdgeCasesTests.cs",
    "Integration\SqlServerSourceAdapterTests.cs",
    "Integration\MultipleDestinationAdaptersTests.cs",
    "Integration\LargeCsvFilePerformanceTests.cs",
    "Adapters\SqlServerAdapterMessageBoxTests.cs"
)

$basePath = Split-Path -Parent $MyInvocation.MyCommand.Path

foreach ($file in $testFiles) {
    $fullPath = Join-Path $basePath $file
    if (Test-Path $fullPath) {
        Write-Host "Fixing $file..."
        $content = Get-Content $fullPath -Raw
        
        # Pattern 1: Fix constructors missing tableName (before useTransaction)
        # Look for: null, // useTransaction or null, // pollingInterval followed by null, // useTransaction
        $content = $content -replace '(\s+null,\s*//\s*pollingInterval\s*\n\s*)(null,\s*//\s*useTransaction)', '$1null, // tableName`n            $2'
        $content = $content -replace '(\s+null,\s*//\s*tableName\s*\n\s*)(null,\s*//\s*useTransaction)', '$1$2'
        
        # Pattern 2: Fix constructors missing statisticsService at the end
        # Look for: _mockLogger.Object); or _mockSqlLogger.Object); at end of constructor
        $content = $content -replace '(_mockLogger\.Object\));', '$1,`n            null); // statisticsService'
        $content = $content -replace '(_mockSqlLogger\.Object\));', '$1,`n            null); // statisticsService'
        
        # Pattern 3: Fix named parameter calls that need tableName
        $content = $content -replace '(tableName:\s*TableName,\s*\n\s*)(useTransaction:)', '$1$2'
        $content = $content -replace '(pollingInterval:\s*\d+,\s*\n\s*)(tableName:\s*TableName,)', '$1$2'
        
        Set-Content -Path $fullPath -Value $content -NoNewline
    }
}

Write-Host "Done fixing test files."




















