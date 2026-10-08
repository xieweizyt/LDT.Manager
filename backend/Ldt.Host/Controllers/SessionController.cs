using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Users;

namespace Ldt.Controllers;

[Authorize]
[Route("api/ldt")]
public class SessionController(ICurrentUser currentUser, IPermissionDefinitionManager definitions,
    IPermissionChecker checker, IdentityUserManager users, IHostEnvironment environment,
    IPermissionManager permissionManager) : AbpController
{
    [HttpGet("session")]
    public async Task<object> Session()
    {
        var user = await users.GetByIdAsync(currentUser.GetId());
        var granted = new List<string>();
        foreach (var permission in await definitions.GetPermissionsAsync())
            if (permission.IsEnabled && await checker.IsGrantedAsync(permission.Name)) granted.Add(permission.Name);
        return new { userId = user.Id, userName = user.UserName, email = user.Email,
            roles = await users.GetRolesAsync(user), buttons = granted, permissions = granted };
    }

    [HttpGet("menus")]
    public async Task<object> Menus()
    {
        var children = new List<object>
        {
            new { path = "overview", name = "Overview", component = "/system/overview",
                meta = new { title = "工作台", icon = "ri:dashboard-line" } }
        };
        var routes = new[]
        {
            ("user", "User", "用户列表", "ri:user-line", IdentityPermissions.Users.Default),
            ("role", "Role", "角色列表", "ri:group-line", IdentityPermissions.Roles.Default),
            ("user-authorization", "UserAuthorization", "用户授权", "ri:user-settings-line", IdentityPermissions.Users.Default),
            ("role-authorization", "RoleAuthorization", "角色授权", "ri:shield-keyhole-line", IdentityPermissions.Roles.Default)
        };
        foreach (var (path, name, title, icon, permission) in routes)
        {
            if (!await checker.IsGrantedAsync(permission)) continue;
            if (path == "user-authorization" && !await checker.IsGrantedAsync(IdentityPermissions.Users.ManagePermissions)
                && !await checker.IsGrantedAsync(IdentityPermissions.Users.ManageRoles)) continue;
            if (path == "role-authorization" && !await checker.IsGrantedAsync(IdentityPermissions.Roles.ManagePermissions)) continue;
            children.Add(new { path, name, component = "/system/" + path,
                meta = new { title, icon, keepAlive = false } });
        }
        return new[] { new { path = "/system", name = "System", component = "/index/index",
            meta = new { title = "系统管理", icon = "ri:settings-3-line" }, children } };
    }

    [AllowAnonymous]
    [HttpGet("health")]
    public object Health() => new { status = "ok", service = "Ldt.Host", environment = environment.EnvironmentName };

    [Authorize(IdentityPermissions.Users.ManageRoles)]
    [HttpPut("users/{id:guid}/roles")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] IdentityUserUpdateRolesDto input)
    {
        var user = await users.GetByIdAsync(id);
        (await users.SetRolesAsync(user, input.RoleNames)).CheckErrors();
        (await users.UpdateAsync(user)).CheckErrors();
        return NoContent();
    }

    [Authorize(IdentityPermissions.Users.ManagePermissions)]
    [HttpGet("users/{id:guid}/inherited-permissions")]
    public async Task<Dictionary<string, List<string>>> InheritedPermissions(Guid id)
    {
        var user = await users.GetByIdAsync(id);
        var result = new Dictionary<string, List<string>>();
        foreach (var role in await users.GetRolesAsync(user))
        {
            foreach (var permission in await permissionManager.GetAllAsync(RolePermissionValueProvider.ProviderName, role))
            {
                if (!permission.IsGranted) continue;
                if (!result.TryGetValue(permission.Name, out var roles)) result[permission.Name] = roles = new List<string>();
                roles.Add(role);
            }
        }
        return result;
    }
}
