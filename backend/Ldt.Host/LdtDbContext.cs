using BaseService.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.IdentityServer.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace Ldt;

[ConnectionStringName("Default")]
public class LdtDbContext(DbContextOptions<LdtDbContext> options) : AbpDbContext<LdtDbContext>(options)
{
    public DbSet<LdtBootstrapState> BootstrapStates => Set<LdtBootstrapState>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureIdentity();
        builder.ConfigureIdentityServer();
        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureTenantManagement();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigureBaseService();
        builder.Entity<LdtBootstrapState>().ToTable("LdtBootstrapStates").HasKey(state => state.Id);
    }
}

public class LdtBootstrapState
{
    public int Id { get; set; }
    public DateTime InitializedAt { get; set; }
}

public class LdtDbContextFactory : IDesignTimeDbContextFactory<LdtDbContext>
{
    public LdtDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables().Build();
        var options = new DbContextOptionsBuilder<LdtDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default")).Options;
        return new LdtDbContext(options);
    }
}
