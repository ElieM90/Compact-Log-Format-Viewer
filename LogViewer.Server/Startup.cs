using System.Text.Json;
using LogViewer.Server.Hubs;
using LogViewer.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;

namespace LogViewer.Server;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            // Remove model validation to avoid .NET 9 model metadata issues
            options.ModelValidatorProviders.Clear();
        })
        .AddNewtonsoftJson(options =>
        {
            options.SerializerSettings.ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };
        })
        .ConfigureApiBehaviorOptions(options =>
        {
            // Disable automatic inference of binding sources to avoid .NET 9 model metadata issue
            options.SuppressInferBindingSourcesForParameters = true;
            // Disable automatic model state validation to avoid .NET 9 model metadata issue
            options.SuppressModelStateInvalidFilter = true;
        });

        services.AddSingleton<ILogParser, LogParser>();
        services.AddSignalR();

        // Add Data Protection for encrypting FTP passwords
        services.AddDataProtection();

        // Add FTP services
        services.AddSingleton<IFtpService, FtpService>();
        services.AddSingleton<IConnectionStorageService, ConnectionStorageService>();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseDefaultFiles(); // index.html etc
        app.UseStaticFiles(); // serve assets from wwwroot

        app.UseDeveloperExceptionPage();

        app.UseRouting();
        app.UseEndpoints(routes =>
        {
            routes.MapControllers();
            routes.MapHub<LogHub>("log");
        });
    }
}
