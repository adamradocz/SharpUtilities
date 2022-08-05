using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SharpUtilities.Factories;
using SharpUtilities.Options;

namespace SharpUtilities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureWritable<TOptions>(this IServiceCollection services, IConfigurationSection section, Action<WritableOptionsMonitorOption> options) where TOptions : class
    {
        return services.Configure(options)
            .ConfigureWritable<TOptions>(section);
    }

    public static IServiceCollection ConfigureWritable<TOptions>(this IServiceCollection services, IConfigurationSection section) where TOptions : class
    {
        return services.Configure<TOptions>(section)
            .AddSingleton<IWritableOptionsMonitor<TOptions>>(serviceProvider =>
            {
                var optionsFactory = serviceProvider.GetRequiredService<IOptionsFactory<TOptions>>();
                var optionsChangeTokenSources = serviceProvider.GetRequiredService<IEnumerable<IOptionsChangeTokenSource<TOptions>>>();
                var optionsMonitorCache = serviceProvider.GetRequiredService<IOptionsMonitorCache<TOptions>>();
                var options = serviceProvider.GetRequiredService<IOptions<WritableOptionsMonitorOption>>();
                var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
                var configurationRoot = (IConfigurationRoot)serviceProvider.GetRequiredService<IConfiguration>();
                return new WritableOptionsMonitor<TOptions>(optionsFactory, optionsChangeTokenSources, optionsMonitorCache, options, hostEnvironment, configurationRoot, section);
            });
    }

    public static IServiceCollection AddGenericServices(this IServiceCollection services)
    {
        services.TryAddSingleton(typeof(IFactory<>), typeof(Factory<>));
        services.TryAddTransient(typeof(ILazyFactory<>), typeof(LazyFactory<>));

        return services;
    }
}
