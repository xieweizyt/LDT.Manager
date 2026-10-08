$ErrorActionPreference = 'Stop'
function Set-LdtNodeRuntime {
    $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
    $candidates = @($nodeCommand.Source, (Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe')) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
    foreach ($candidate in $candidates) {
        $version = [Version]((& $candidate --version).TrimStart('v'))
        if (($version.Major -eq 20 -and $version -ge [Version]'20.19.0') -or $version -ge [Version]'22.12.0') {
            $env:PATH = [IO.Path]::GetDirectoryName($candidate) + [IO.Path]::PathSeparator + $env:PATH
            return $candidate
        }
    }
    throw 'Art Design Pro requires Node 20.19+ or 22.12+. Install a supported Node version first.'
}
function Enable-LdtDirectNuGet {
    $source = [Uri]'https://api.nuget.org'
    $proxy = [Net.Http.HttpClient]::DefaultProxy.GetProxy($source)
    # Make .NET apply an explicit bypass list when Windows has a system proxy.
    if ($proxy -and $proxy.Host -ne $source.Host) {
        $env:HTTP_PROXY = $proxy.AbsoluteUri
        $env:HTTPS_PROXY = $proxy.AbsoluteUri
    }
    $env:NO_PROXY = (@($env:NO_PROXY, 'api.nuget.org', 'globalcdn.nuget.org', 'nuget.org', 'localhost', '127.0.0.1') | Where-Object { $_ }) -join ','
}
function Assert-LdtExit([string]$Operation) {
    if ($LASTEXITCODE -ne 0) { throw "$Operation failed with exit code $LASTEXITCODE." }
}
function Import-LdtEnvironment([string]$Path) {
    foreach ($line in Get-Content -LiteralPath $Path) {
        $text = $line.Trim()
        if (!$text -or $text.StartsWith('#')) { continue }
        if ($text -notmatch '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { throw 'Environment file contains an invalid entry.' }
        $key = $Matches[1]; $value = $Matches[2]
        if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) { $value = $value.Substring(1, $value.Length - 2) }
        [Environment]::SetEnvironmentVariable($key, $value, 'Process')
    }
}
