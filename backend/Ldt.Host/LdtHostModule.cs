using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using System.Threading.RateLimiting;
using BaseService;
using BaseService.EntityFrameworkCore;
using BaseService.Permissions;
using IdentityServer4;
using IdentityServer4.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Net;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity.AspNetCore;
using Volo.Abp.Identity;
using Volo.Abp.IdentityServer;
using Volo.Abp.IdentityServer.EntityFrameworkCore;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.HttpApi;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;

namespace Ldt;

[DependsOn(typeof(AbpAutofacModule), typeof(BaseServiceApplicationModule),
    typeof(BaseServiceEntityFrameworkCoreModule), typeof(AbpIdentityHttpApiModule),
    typeof(AbpPermissionManagementHttpApiModule), typeof(AbpIdentityServerEntityFrameworkCoreModule),
    typeof(AbpIdentityAspNetCoreModule))]
public class LdtHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var environment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();
        Directory.CreateDirectory("App_Data");
        PreConfigure<IIdentityServerBuilder>(builder =>
        {
            if (environment.IsDevelopment())
            {
                builder.AddDeveloperSigningCredential(persistKey: true, filename: "App_Data/signing-key.jwk");
            }
            else
            {
                var path = configuration["AuthServer:SigningCertificatePath"];
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    throw new InvalidOperationException("Production requires AuthServer__SigningCertificatePath and a private signing certificate.");
                builder.AddSigningCredential(new X509Certificate2(path,
                    configuration["AuthServer:SigningCertificatePassword"], X509KeyStorageFlags.EphemeralKeySet));
            }
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var environment = context.Services.GetHostingEnvironment();
        Directory.CreateDirectory("App_Data");
        context.Services.AddDataProtection().SetApplicationName("Ldt")
            .PersistKeysToFileSystem(new DirectoryInfo("App_Data/data-protection"));
        context.Services.AddAbpDbContext<LdtDbContext>();
        Configure<MvcOptions>(options => options.Conventions.Add(new LdtIdentityApiConvention()));
        if (!environment.IsDevelopment() && string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
            throw new InvalidOperationException("Production requires ConnectionStrings__Default.");
        context.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var address in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? new[] { "127.0.0.1" })
                options.KnownProxies.Add(IPAddress.Parse(address));
        });
        Configure<AbpDbContextOptions>(options => options.UseSqlServer());
        Configure<AbpMultiTenancyOptions>(options => options.IsEnabled = false);
        Configure<AbpAuditingOptions>(options => options.IsEnabledForGetRequests = true);
        Configure<AbpAntiForgeryOptions>(options => options.AutoValidate = false);
        Configure<PermissionManagementOptions>(options => options.IsDynamicPermissionStoreEnabled = false);
        PostConfigure<AbpPermissionOptions>(options =>
        {
            options.DefinitionProviders.Remove(typeof(BaseServicePermissionDefinitionProvider));
            foreach (var provider in options.DefinitionProviders.Where(type => type.Namespace?.StartsWith("Volo.Abp.TenantManagement", StringComparison.Ordinal) == true).ToArray())
                options.DefinitionProviders.Remove(provider);
        });
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
            options.Languages.Add(new LanguageInfo("en", "en", "English"));
        });
        Configure<IdentityServerOptions>(options =>
        {
            options.IssuerUri = configuration["AuthServer:Authority"];
            options.Endpoints.EnableAuthorizeEndpoint = false;
            options.Endpoints.EnableEndSessionEndpoint = false;
            options.Endpoints.EnableCheckSessionEndpoint = false;
        });
        context.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.Authority = configuration["AuthServer:Authority"];
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.Audience = "BaseService";
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = AbpClaimTypes.UserName,
                RoleClaimType = AbpClaimTypes.Role,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async tokenContext =>
                {
                    var subject = tokenContext.Principal?.FindFirst("sub")?.Value;
                    var manager = tokenContext.HttpContext.RequestServices.GetRequiredService<IdentityUserManager>();
                    var user = subject is null ? null : await manager.FindByIdAsync(subject);
                    if (user is null || !user.IsActive || (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow))
                    {
                        tokenContext.Fail("Account is unavailable.");
                        return;
                    }
                    var identity = (ClaimsIdentity)tokenContext.Principal!.Identity!;
                    foreach (var claim in identity.Claims.Where(claim => claim.Type == "role" || claim.Type == AbpClaimTypes.Role).ToArray())
                        identity.RemoveClaim(claim);
                    foreach (var role in await manager.GetRolesAsync(user)) identity.AddClaim(new Claim(AbpClaimTypes.Role, role));
                    if (!identity.HasClaim(claim => claim.Type == AbpClaimTypes.UserId))
                        identity.AddClaim(new Claim(AbpClaimTypes.UserId, user.Id.ToString()));
                }
            };
        });
        var origins = configuration["App:CorsOrigins"]?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            ?? throw new InvalidOperationException("App:CorsOrigins is required.");
        if (!environment.IsDevelopment() && origins.Any(origin => !origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Production frontend origins must use HTTPS.");
        context.Services.AddCors(options => options.AddPolicy("Ldt", policy =>
            policy.WithOrigins(origins).WithAbpExposedHeaders().AllowAnyHeader().AllowAnyMethod()));
        context.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                http.Request.Path.StartsWithSegments("/connect/token")
                    ? RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
                        new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("api"));
        });
        context.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "LDT 管理 API", Version = "v1" });
            options.DocInclusionPredicate((_, _) => true);
            options.CustomSchemaIds(type => type.FullName);
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
            });
        });
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var environment = context.GetEnvironment();
        app.UseForwardedHeaders();
        if (environment.IsDevelopment()) app.UseDeveloperExceptionPage();
        app.UseCorrelationId();
        app.UseRouting();
        app.UseCors("Ldt");
        app.UseRateLimiter();
        app.UseAbpRequestLocalization();
        app.UseAuthentication();
        app.UseIdentityServer();
        app.UseAuthorization();
        app.UseUnitOfWork();
        app.UseAuditing();
        if (environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        app.UseConfiguredEndpoints();
        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
        if (configuration.GetValue<bool>("Database:AutoMigrate"))
        {
            await using var scope = context.ServiceProvider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<LdtDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<LdtDatabaseSeeder>().SeedAsync();
        }
    }
}
