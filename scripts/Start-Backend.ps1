#Requires -Version 7.0
param([ValidateSet('Development','Production')][string]$Environment='Development', [string]$EnvFile='', [switch]$MigrateOnly)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Runtime.ps1')
if ($EnvFile) { Import-LdtEnvironment $EnvFile }
$ldtRoot = Split-Path -Parent $PSScriptRoot
$env:ASPNETCORE_ENVIRONMENT=$Environment
Push-Location (Join-Path $ldtRoot 'backend\Ldt.Host')
try {
    if ($MigrateOnly) { dotnet run -c Release --no-restore --no-launch-profile -- --migrate }
    else { dotnet run -c Release --no-restore --no-launch-profile }
    Assert-LdtExit 'Backend execution'
} finally { Pop-Location }
