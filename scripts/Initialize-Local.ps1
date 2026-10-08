#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Runtime.ps1')
$ldtRoot = Split-Path -Parent $PSScriptRoot
Set-LdtNodeRuntime | Out-Null
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 8 SDK or a newer SDK is required.' }
if (!(Get-Command pnpm -ErrorAction SilentlyContinue)) { throw 'pnpm 11 is required. Install pnpm@11.25.0 first.' }
if (!$env:ConnectionStrings__Default) {
    if (!(Get-Command SqlLocalDB -ErrorAction SilentlyContinue)) { throw 'Install SQL Server LocalDB, or set ConnectionStrings__Default to your development SQL Server.' }
    SqlLocalDB start MSSQLLocalDB
    Assert-LdtExit 'LocalDB startup'
}
$previousHusky = $env:HUSKY
try {
    $env:HUSKY = '0'
    Push-Location (Join-Path $ldtRoot 'frontend')
    try { pnpm install --frozen-lockfile; Assert-LdtExit 'Frontend dependency installation' } finally { Pop-Location }
} finally { $env:HUSKY = $previousHusky }
Enable-LdtDirectNuGet
dotnet restore (Join-Path $ldtRoot 'backend\Ldt.Host\Ldt.Host.csproj') --configfile (Join-Path $ldtRoot 'backend\NuGet.Config')
Assert-LdtExit 'Backend dependency restore'
dotnet build (Join-Path $ldtRoot 'backend\Ldt.Host\Ldt.Host.csproj') --no-restore -c Release
Assert-LdtExit 'Backend build'
Write-Host 'Initialization complete. Start scripts\Start-Backend.ps1 and scripts\Start-Frontend.ps1 in separate terminals.'
