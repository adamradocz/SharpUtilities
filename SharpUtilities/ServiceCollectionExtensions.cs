using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpUtilities.Options;

namespace SharpUtilities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureWritable<TOptions>(this IServiceCollection services, IConfigurationSection section, Action<WritableOptionsMonitorOption>? options = null) where TOptions : class
    {
        if (options is not null)
        {
            _ = services.Configure(options);
        }

        return services.AddSingleton<IWritableOptionsMonitor<TOptions>>(serviceProvider =>
            {
                var optionsFactory = serviceProvider.GetRequiredService<IOptionsFactory<TOptions>>();
                var optionsChangeTokenSources = serviceProvider.GetRequiredService<IEnumerable<IOptionsChangeTokenSource<TOptions>>>();
                var optionsMonitorCache = serviceProvider.GetRequiredService<IOptionsMonitorCache<TOptions>>();
                var options = serviceProvider.GetRequiredService<IOptions<WritableOptionsMonitorOption>>();
                var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
                var configurationRoot = (IConfigurationRoot)serviceProvider.GetRequiredService<IConfiguration>();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<TOptions>();
                return new WritableOptionsMonitor<TOptions>(optionsFactory, optionsChangeTokenSources, optionsMonitorCache, options, hostEnvironment, configurationRoot, section, logger);
            });
    }
}
