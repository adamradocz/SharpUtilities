using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpUtilities.Options;

namespace SharpUtilities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureWritable<TOptions>(this IServiceCollection services, IConfigurationSection section) where TOptions : class
    {
        return services.AddSingleton<IWritableOptionsMonitor<TOptions>>(serviceProvider =>
        {
            var optionsFactory = serviceProvider.GetRequiredService<IOptionsFactory<TOptions>>();
            var optionsChangeTokenSources = serviceProvider.GetRequiredService<IEnumerable<IOptionsChangeTokenSource<TOptions>>>();
            var optionsMonitorCache = serviceProvider.GetRequiredService<IOptionsMonitorCache<TOptions>>();
            var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
            var configurationRoot = (IConfigurationRoot)serviceProvider.GetRequiredService<IConfiguration>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<TOptions>();
            return new WritableOptionsMonitor<TOptions>(optionsFactory, optionsChangeTokenSources, optionsMonitorCache, hostEnvironment, configurationRoot, section, logger);
        });
    }
}
