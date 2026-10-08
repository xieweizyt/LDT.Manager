param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$AdminPassword = 'Ldt.Local2026!',
    [string]$ReportPath = ''
)
$ErrorActionPreference = 'Stop'
$uri = [Uri]$BaseUrl
if (!$uri.IsLoopback -or $uri.Port -ne 5000) { throw 'This test only permits the local development API on port 5000.' }
$ldtRoot = Split-Path -Parent $PSScriptRoot
if (!$ReportPath) { $ReportPath = Join-Path $ldtRoot 'docs\api-verification.json' }
$checks = [Collections.Generic.List[object]]::new()
$createdUser = $null; $createdRole = $null; $adminHeaders = $null
$suiteCompleted = $false
function Check([string]$Name, [bool]$Passed) {
    $checks.Add([PSCustomObject]@{ Name=$Name; Passed=$Passed })
    if (!$Passed) { throw "Check failed: $Name" }
    Write-Host "PASS $Name"
}
function Call([string]$Method, [string]$Path, [object]$Data = $null, [hashtable]$Headers = @{}) {
    $arguments = @{ Uri=($BaseUrl.TrimEnd('/') + $Path); Method=$Method; Headers=$Headers; NoProxy=$true; TimeoutSec=20; SkipHttpErrorCheck=$true }
    if ($null -ne $Data) { $arguments.Body = ConvertTo-Json -InputObject $Data -Depth 20 -Compress; $arguments.ContentType='application/json' }
    $response = Invoke-WebRequest @arguments
    $body = if ($response.Content -and $response.Headers['Content-Type'] -match 'json') { $response.Content | ConvertFrom-Json } else { $null }
    return [PSCustomObject]@{Status=[int]$response.StatusCode;Data=$body}
}
function Tokens([string]$UserName, [string]$Password, [string]$Refresh = '') {
    $body = if ($Refresh) { @{ grant_type='refresh_token'; client_id='ldt-spa'; refresh_token=$Refresh } }
        else { @{ grant_type='password'; client_id='ldt-spa'; username=$UserName; password=$Password; scope='openid profile email role BaseService offline_access' } }
    return Invoke-RestMethod -Uri "$BaseUrl/connect/token" -Method Post -Body $body -NoProxy -TimeoutSec 20
}
function Set-Grants([string]$Provider, [string]$Key, [bool]$Granted) {
    return Call 'PUT' "/api/permission-management/permissions?providerName=$Provider&providerKey=$([Uri]::EscapeDataString($Key))" @{
        permissions=@(@{name='AbpIdentity.Users';isGranted=$Granted}, @{name='AbpIdentity.Roles';isGranted=$Granted})
    } $adminHeaders
}
try {
    $health = Call 'GET' '/api/ldt/health'
    Check 'Development service is running' ($health.Status -eq 200 -and $health.Data.environment -eq 'Development')
    Check 'Anonymous user list is rejected' ((Call 'GET' '/api/identity/users').Status -eq 401)
    $definition = Call 'GET' '/swagger/v1/swagger.json'
    Check 'Unrelated upstream APIs are not exposed' ($definition.Status -eq 200 -and !($definition.Data.paths.PSObject.Properties.Name | Where-Object { $_ -match '^/api/(base|business|tenant-management|account)' }))
    $adminTokens = Tokens 'admin' $AdminPassword
    $adminHeaders = @{ Authorization=('Bearer ' + $adminTokens.access_token) }
    Check 'OAuth access and refresh tokens are issued' (!!$adminTokens.access_token -and !!$adminTokens.refresh_token)
    Check 'Administrator can read users and roles' ((Call 'GET' '/api/identity/users' $null $adminHeaders).Status -eq 200 -and (Call 'GET' '/api/identity/roles' $null $adminHeaders).Status -eq 200)
    $suffix = [Guid]::NewGuid().ToString('N').Substring(0,10)
    $userName = "qa_$suffix"; $roleName = "qa_role_$suffix"; $testPassword = 'Ldt.Test2026!'
    $role = Call 'POST' '/api/identity/roles' @{name=$roleName;isDefault=$false;isPublic=$true} $adminHeaders
    Check 'Role creation succeeds' ($role.Status -eq 200 -and !!$role.Data.id)
    $createdRole = $role.Data.id
    $editedRole = Call 'PUT' "/api/identity/roles/$createdRole" @{name=$roleName;isDefault=$false;isPublic=$false;concurrencyStamp=$role.Data.concurrencyStamp} $adminHeaders
    Check 'Role editing is persisted' ($editedRole.Status -eq 200 -and $editedRole.Data.isPublic -eq $false)
    $user = Call 'POST' '/api/identity/users' @{userName=$userName;name='接口验证';email="$userName@ldt.local";password=$testPassword;isActive=$true;lockoutEnabled=$true;roleNames=@()} $adminHeaders
    Check 'User creation succeeds' ($user.Status -eq 200 -and !!$user.Data.id)
    $createdUser = $user.Data.id
    $userTokens = Tokens $userName $testPassword
    $userHeaders = @{Authorization=('Bearer ' + $userTokens.access_token)}
    Check 'User without grants receives 403' ((Call 'GET' '/api/identity/users' $null $userHeaders).Status -eq 403)
    Check 'User cannot grant themselves permissions' ((Call 'PUT' "/api/permission-management/permissions?providerName=U&providerKey=$createdUser" @{permissions=@(@{name='AbpIdentity.Users';isGranted=$true})} $userHeaders).Status -eq 403)
    Check 'User cannot read inherited permission details without authorization' ((Call 'GET' "/api/ldt/users/$createdUser/inherited-permissions" $null $userHeaders).Status -eq 403)
    Check 'Direct user permission can be saved' ((Set-Grants 'U' $createdUser $true).Status -eq 204)
    $saved = Call 'GET' "/api/permission-management/permissions?providerName=U&providerKey=$createdUser" $null $adminHeaders
    $readGrant = $saved.Data.groups.permissions | Where-Object name -eq 'AbpIdentity.Users'
    Check 'Direct user permission is persisted' ($readGrant.isGranted -eq $true)
    Check 'Direct grant takes effect with the existing token' ((Call 'GET' '/api/identity/users' $null $userHeaders).Status -eq 200)
    Check 'Read permission cannot create a user' ((Call 'POST' '/api/identity/users' @{userName="forbidden_$suffix";email="forbidden_$suffix@ldt.local";password=$testPassword;isActive=$true} $userHeaders).Status -eq 403)
    Check 'Direct user grant can be revoked' ((Set-Grants 'U' $createdUser $false).Status -eq 204)
    Check 'Revocation takes effect with the existing token' ((Call 'GET' '/api/identity/users' $null $userHeaders).Status -eq 403)
    Check 'Role permissions can be saved' ((Set-Grants 'R' $roleName $true).Status -eq 204)
    Check 'User role assignment can be saved' ((Call 'PUT' "/api/ldt/users/$createdUser/roles" @{roleNames=@($roleName)} $adminHeaders).Status -eq 204)
    $assigned = Call 'GET' "/api/identity/users/$createdUser/roles" $null $adminHeaders
    Check 'Role membership is persisted' ($assigned.Data.items.name -contains $roleName)
    $inherited = Call 'GET' "/api/ldt/users/$createdUser/inherited-permissions" $null $adminHeaders
    Check 'Inherited permissions identify their granting role' ($inherited.Status -eq 200 -and $inherited.Data.'AbpIdentity.Users' -contains $roleName)
    Check 'New role membership takes effect with the existing token' ((Call 'GET' '/api/identity/users' $null $userHeaders).Status -eq 200 -and (Call 'GET' '/api/identity/roles' $null $userHeaders).Status -eq 200)
    Check 'Role grants can be revoked' ((Set-Grants 'R' $roleName $false).Status -eq 204)
    Check 'Role revocation takes effect with the existing token' ((Call 'GET' '/api/identity/users' $null $userHeaders).Status -eq 403)
    $inheritedAfterRevocation = Call 'GET' "/api/ldt/users/$createdUser/inherited-permissions" $null $adminHeaders
    Check 'Inherited permission details update after revocation' (!$inheritedAfterRevocation.Data.'AbpIdentity.Users')
    $editGrant = Call 'PUT' "/api/permission-management/permissions?providerName=U&providerKey=$createdUser" @{permissions=@(@{name='AbpIdentity.Users';isGranted=$true},@{name='AbpIdentity.Users.Update';isGranted=$true})} $adminHeaders
    Check 'User-edit permission can be delegated' ($editGrant.Status -eq 204)
    Check 'Editing users does not grant role assignment access' ((Call 'PUT' "/api/ldt/users/$createdUser/roles" @{roleNames=@($roleName)} $userHeaders).Status -eq 403)
    Check 'Legacy role assignment bypass is unavailable' ((Call 'PUT' "/api/identity/users/$createdUser/roles" @{roleNames=@($roleName)} $userHeaders).Status -eq 405)
    $delegateGrant = Call 'PUT' "/api/permission-management/permissions?providerName=U&providerKey=$createdUser" @{permissions=@(@{name='AbpIdentity.Users.Update';isGranted=$false},@{name='AbpIdentity.Users.Update.ManageRoles';isGranted=$true})} $adminHeaders
    Check 'Role assignment permission can be delegated separately' ($delegateGrant.Status -eq 204)
    Check 'Role assignment works without user-edit permission' ((Call 'PUT' "/api/ldt/users/$createdUser/roles" @{roleNames=@($roleName)} $userHeaders).Status -eq 204)
    $delegatedMenus = Call 'GET' '/api/ldt/menus' $null $userHeaders
    Check 'Delegated role administrator can access the authorization page' ($delegatedMenus.Data.children.path -contains 'user-authorization')
    Check 'Role assignment does not grant permission-editor access' ((Call 'GET' "/api/permission-management/permissions?providerName=U&providerKey=$createdUser" $null $userHeaders).Status -eq 403)
    $currentUser = (Call 'GET' "/api/identity/users/$createdUser" $null $adminHeaders).Data
    $updated = Call 'PUT' "/api/identity/users/$createdUser" @{userName=$userName;name='修改后';email=$currentUser.email;isActive=$true;lockoutEnabled=$true;concurrencyStamp=$currentUser.concurrencyStamp} $adminHeaders
    Check 'User editing succeeds' ($updated.Status -eq 200 -and $updated.Data.name -eq '修改后')
    $disabled = Call 'PUT' "/api/identity/users/$createdUser" @{userName=$userName;name='修改后';email=$currentUser.email;isActive=$false;lockoutEnabled=$true;concurrencyStamp=$updated.Data.concurrencyStamp} $adminHeaders
    Check 'Account can be disabled' ($disabled.Status -eq 200)
    Check 'Disabling an account rejects its existing token' ((Call 'GET' '/api/ldt/session' $null $userHeaders).Status -eq 401)
    $refreshed = Tokens '' '' $adminTokens.refresh_token
    Check 'Refresh rotates the token' (!!$refreshed.access_token -and !!$refreshed.refresh_token -and $refreshed.refresh_token -ne $adminTokens.refresh_token)
    $revoked = Invoke-WebRequest -Uri "$BaseUrl/connect/revocation" -Method Post -Body @{client_id='ldt-spa';token=$refreshed.refresh_token;token_type_hint='refresh_token'} -NoProxy -TimeoutSec 20 -SkipHttpErrorCheck
    Check 'Logout revocation succeeds' ($revoked.StatusCode -eq 200)
    $afterRevocation = Invoke-WebRequest -Uri "$BaseUrl/connect/token" -Method Post -Body @{client_id='ldt-spa';grant_type='refresh_token';refresh_token=$refreshed.refresh_token} -NoProxy -TimeoutSec 20 -SkipHttpErrorCheck
    Check 'Revoked refresh token cannot issue another token' ($afterRevocation.StatusCode -eq 400)
    $suiteCompleted = $true
} finally {
    if ($createdUser -and $adminHeaders) { Check 'Temporary user is removed' ((Call 'DELETE' "/api/identity/users/$createdUser" $null $adminHeaders).Status -eq 204) }
    if ($createdRole -and $adminHeaders) { Check 'Temporary role is removed' ((Call 'DELETE' "/api/identity/roles/$createdRole" $null $adminHeaders).Status -eq 204) }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($ReportPath)) | Out-Null
    [PSCustomObject]@{VerifiedAt=(Get-Date).ToString('o');Environment='Development';Passed=($suiteCompleted -and $checks.Where({!$_.Passed}).Count -eq 0);Checks=$checks} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding utf8NoBOM
}
