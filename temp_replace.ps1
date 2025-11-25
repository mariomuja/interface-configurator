 = @('scripts/migrate-to-west-europe.ps1','scripts/migrate-to-west-europe-safe.ps1','scripts/migrate-to-west-europe-complete.ps1','scripts/check-messagebox-data.sql','scripts/diagnose-messagebox.ps1','terraform/outputs.tf','terraform/check-messagebox-data.sql','terraform/init-messagebox-database.sql','terraform/main.tf','terraform/update-messagebox-database.sql','terraform/check-messagebox-database.ps1','api/start-transport.js','api/GetMessageBoxMessages.js','api/initialize-messagebox.js')
foreach ( in ) {
     = Join-Path (Get-Location) 
    if (Test-Path ) {
         = Get-Content -LiteralPath  -Raw
         =  -replace 'MessageBox','InterfaceConfigDb'
         =  -replace 'messagebox','interfaceconfigdb'
        Set-Content -LiteralPath  -Value 
    }
}
