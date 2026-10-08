#Requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Runtime.ps1')
Set-LdtNodeRuntime | Out-Null
$ldtRoot = Split-Path -Parent $PSScriptRoot
Push-Location (Join-Path $ldtRoot 'frontend')
try { pnpm run dev; Assert-LdtExit 'Frontend execution' } finally { Pop-Location }
