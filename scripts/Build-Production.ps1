#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Runtime.ps1')
$ldtRoot = Split-Path -Parent $PSScriptRoot
Set-LdtNodeRuntime | Out-Null
Push-Location (Join-Path $ldtRoot 'frontend')
try { pnpm run build:prod; Assert-LdtExit 'Production frontend build' } finally { Pop-Location }
Enable-LdtDirectNuGet
dotnet restore (Join-Path $ldtRoot 'backend\Ldt.Host\Ldt.Host.csproj') --configfile (Join-Path $ldtRoot 'backend\NuGet.Config')
Assert-LdtExit 'Backend restore'
dotnet publish (Join-Path $ldtRoot 'backend\Ldt.Host\Ldt.Host.csproj') -c Release --no-restore -o (Join-Path $ldtRoot 'artifacts\backend')
Assert-LdtExit 'Backend publish'
Write-Host 'Frontend output: frontend\dist; Backend output: artifacts\backend.'
