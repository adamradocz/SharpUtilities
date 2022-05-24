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
        return services.AddSingleton<IWritableOptionsMonitor<TOptions>>(provider =>
        {
            var optionsFactory = provider.GetRequiredService<IOptionsFactory<TOptions>>();
            var optionsChangeTokenSources = provider.GetRequiredService<IEnumerable<IOptionsChangeTokenSource<TOptions>>>();
            var optionsMonitorCache = provider.GetRequiredService<IOptionsMonitorCache<TOptions>>();
            var hostEnvironment = provider.GetRequiredService<IHostEnvironment>();
            var configurationRoot = (IConfigurationRoot)provider.GetRequiredService<IConfiguration>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<TOptions>();
            return new WritableOptionsMonitor<TOptions>(optionsFactory, optionsChangeTokenSources, optionsMonitorCache, hostEnvironment, configurationRoot, section, logger);
        });
    }
}
