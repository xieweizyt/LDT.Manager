using IdentityServer4.Models;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.IdentityServer.ApiResources;
using Volo.Abp.IdentityServer.ApiScopes;
using Volo.Abp.IdentityServer.Clients;
using Volo.Abp.IdentityServer.IdentityResources;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using ApiResource = Volo.Abp.IdentityServer.ApiResources.ApiResource;
using ApiScope = Volo.Abp.IdentityServer.ApiScopes.ApiScope;
using Client = Volo.Abp.IdentityServer.Clients.Client;

namespace Ldt;

public class LdtDatabaseSeeder(IDataSeeder dataSeeder, IApiResourceRepository resources,
    IApiScopeRepository scopes, IClientRepository clients, IIdentityResourceDataSeeder identityResources,
    IGuidGenerator guids, IPermissionDefinitionManager definitions, IPermissionDataSeeder permissions,
    IConfiguration configuration, IUnitOfWorkManager units, LdtDbContext database) : ITransientDependency
{
    public async Task SeedAsync()
    {
        var initialized = await database.BootstrapStates.AnyAsync();
        var password = configuration["Seed:AdminPassword"];
        if (!initialized && string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set Seed__AdminPassword before initializing the database.");
        using var unit = units.Begin(requiresNew: true, isTransactional: true);
        if (!initialized)
            await dataSeeder.SeedAsync(new DataSeedContext()
                .WithProperty("AdminEmail", configuration["Seed:AdminEmail"] ?? "admin@ldt.local")
                .WithProperty("AdminPassword", password));
        await identityResources.CreateStandardResourcesAsync();
        const string apiName = "BaseService";
        if (await scopes.FindByNameAsync(apiName) is null)
            await scopes.InsertAsync(new ApiScope(guids.Create(), apiName, "LDT 管理 API"));
        if (await resources.FindByNameAsync(apiName) is null)
        {
            var resource = new ApiResource(guids.Create(), apiName, "LDT 管理 API");
            foreach (var claim in new[] { "sub", "name", "email", "role" }) resource.AddUserClaim(claim);
            await resources.InsertAsync(resource);
        }
        var clientId = configuration["AuthServer:ClientId"] ?? "ldt-spa";
        var client = await clients.FindByClientIdAsync(clientId);
        if (client is null)
        {
            client = new Client(guids.Create(), clientId)
            {
                ClientName = "LDT Art Frontend", ProtocolType = "oidc", RequireClientSecret = false,
                RequirePkce = true, AllowOfflineAccess = true, AccessTokenLifetime = 900,
                AbsoluteRefreshTokenLifetime = 28800, SlidingRefreshTokenLifetime = 28800,
                RefreshTokenUsage = (int)TokenUsage.OneTimeOnly,
                RefreshTokenExpiration = (int)TokenExpiration.Absolute,
                UpdateAccessTokenClaimsOnRefresh = true
            };
            // Preserve the upstream public SPA password grant, without a secret in JavaScript.
            client.AddGrantType("password");
            client.AddGrantType("refresh_token");
            foreach (var name in new[] { "openid", "profile", "email", "role", apiName }) client.AddScope(name);
            await clients.InsertAsync(client);
        }
        var origins = (configuration["App:CorsOrigins"] ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        client.AllowedCorsOrigins.RemoveAll(origin => !origins.Contains(origin.Origin));
        foreach (var origin in origins)
            if (client.FindCorsOrigin(origin) is null) client.AddCorsOrigin(origin);
        await clients.UpdateAsync(client);
        if (!initialized)
        {
            var grants = (await definitions.GetPermissionsAsync()).Where(permission => permission.IsEnabled)
                .Select(permission => permission.Name).ToArray();
            await permissions.SeedAsync(RolePermissionValueProvider.ProviderName, "admin", grants);
            database.BootstrapStates.Add(new LdtBootstrapState { InitializedAt = DateTime.UtcNow });
            await database.SaveChangesAsync();
        }
        await unit.CompleteAsync();
    }
}
