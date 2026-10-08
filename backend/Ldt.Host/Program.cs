using Ldt;

var migrateOnly = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--migrate").ToArray());
if (migrateOnly) builder.Configuration["Database:AutoMigrate"] = "true";
builder.WebHost.UseUrls("http://0.0.0.0:5000");
builder.Host.UseAutofac();
await builder.AddApplicationAsync<LdtHostModule>();
var app = builder.Build();
await app.InitializeApplicationAsync();
if (migrateOnly) return;
await app.RunAsync();
