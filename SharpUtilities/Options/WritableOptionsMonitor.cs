using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;

/**
 * Based on: https://docs.microsoft.com/en-us/answers/questions/299791/saving-to-appsettingsjson.html
 */
namespace SharpUtilities.Options;

/// <summary>
/// Writable implementation of IOptionsMonitor.
/// </summary>
/// <typeparam name="TOptions">Options model.</typeparam>
public partial class WritableOptionsMonitor<TOptions> : OptionsMonitor<TOptions>, IWritableOptionsMonitor<TOptions> where TOptions : class
{
    private readonly IConfigurationRoot _configuration;
    private readonly IConfigurationSection _configurationSection;
    private readonly ILogger<TOptions> _logger;
    private readonly JsonDocumentOptions _jsonDocumentOptions;
    private readonly JsonWriterOptions _jsonWriterOptions;

    /// <inheritdoc/>
    public string JsonFilePhysicalPath { get; }

    #region Log
    [LoggerMessage(0, LogLevel.Warning, "Couldn't write the settings. File path: {AppsettingsPhysicalPath}.")]
    partial void LogWriteError(string appsettingsPhysicalPath, Exception exception);
    #endregion

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="factory">The factory to use to create options.</param>
    /// <param name="sources">The sources used to listen for changes to the options instance.</param>
    /// <param name="cache">The cache used to store options.</param>
    /// <param name="hostEnvironment">Hosting environment.</param>
    /// <param name="configuration">IConfiguration root.</param>
    /// <param name="configurationSection">Configuration section.</param>
    /// <param name="logger">Logger.</param>
    public WritableOptionsMonitor(
        IOptionsFactory<TOptions> factory,
        IEnumerable<IOptionsChangeTokenSource<TOptions>> sources,
        IOptionsMonitorCache<TOptions> cache,
        IOptions<WritableOptionsMonitorOption> options,
        IHostEnvironment hostEnvironment,
        IConfigurationRoot configuration,
        IConfigurationSection configurationSection,
        ILogger<TOptions> logger) : base(factory, sources, cache)
    {
        Guard.IsNotNull(hostEnvironment, nameof(hostEnvironment));
        Guard.IsNotNull(options, nameof(options));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configurationSection = configurationSection ?? throw new ArgumentNullException(nameof(configurationSection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        JsonFilePhysicalPath = GetAppSettingsPhysicalPath(options.Value.JsonBaseFile, hostEnvironment);

        _jsonDocumentOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        _jsonWriterOptions = new JsonWriterOptions
        {
            Indented = true,
            SkipValidation = false
        };
    }

    /// <inheritdoc/>
    public bool Update(Action<TOptions> applyChanges, ConfigurationProvider providerFlags)
    {
        bool? isUpdatedSuccessfully = null;
        var optionObject = CurrentValue;
        applyChanges(optionObject);
        
        if ((providerFlags & ConfigurationProvider.Json) == ConfigurationProvider.Json)
        {
            isUpdatedSuccessfully = EvaluateSuccess(isUpdatedSuccessfully, UpdateJsonConfiguration(optionObject));
        }

        if ((providerFlags & ConfigurationProvider.Memory) == ConfigurationProvider.Memory)
        {
            isUpdatedSuccessfully = EvaluateSuccess(isUpdatedSuccessfully, UpdateMemoryConfiguration(optionObject));
        }

        return isUpdatedSuccessfully ?? false;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAsync(Action<TOptions> applyChanges, ConfigurationProvider providerFlags)
    {
        bool? isUpdatedSuccessfully = null;
        var optionObject = CurrentValue;
        applyChanges(optionObject);

        if ((providerFlags & ConfigurationProvider.Json) == ConfigurationProvider.Json)
        {
            isUpdatedSuccessfully = EvaluateSuccess(isUpdatedSuccessfully, await UpdateJsonConfigurationAsync(optionObject));
        }

        if ((providerFlags & ConfigurationProvider.Memory) == ConfigurationProvider.Memory)
        {
            isUpdatedSuccessfully = EvaluateSuccess(isUpdatedSuccessfully, UpdateMemoryConfiguration(optionObject));
        }

        return isUpdatedSuccessfully ?? false;
    }

    private static bool EvaluateSuccess(bool? isUpdatedSuccessfully, bool currentReturnValue)
    {
        // The first evaluation.
        if (isUpdatedSuccessfully is null)
        {
            return currentReturnValue;
        }

        // There was an unsuccessful update.
        if (isUpdatedSuccessfully == false)
        {
            return false;
        }

        return currentReturnValue;
    }

    #region JSON Configuration
    /// <summary>
    /// Get the physical path of the appsettings.json.
    /// If the appsettings.{Environment}.json exists, than it retuns that one.
    /// </summary>
    /// <param name="baseFile">The base settings file, not the environment specific one.</param>
    /// <param name="hostEnvironment">Host environment</param>
    /// <returns>Environment specific physical path of the settings file if exists, otherwise the physical path of the base settings file.</returns>
    private static string GetAppSettingsPhysicalPath(string baseFile, IHostEnvironment hostEnvironment)
    {
        var environmentSpecificFileName = $"{Path.GetFileNameWithoutExtension(baseFile)}.{hostEnvironment.EnvironmentName}{Path.GetExtension(baseFile)}";
        var appsettingsPhysicalPath = Path.Combine(hostEnvironment.ContentRootPath, environmentSpecificFileName);

        if (!File.Exists(appsettingsPhysicalPath))
        {
            appsettingsPhysicalPath = Path.Combine(hostEnvironment.ContentRootPath, baseFile);
        }

        return appsettingsPhysicalPath;
    }

    private bool UpdateJsonConfiguration(TOptions updatedOption)
    {
        ReadOnlyMemory<byte> appsettingsMemory = File.ReadAllBytes(JsonFilePhysicalPath);

        JsonElement appsettingsRootElement;
        using var appsettingsJsonDocument = JsonDocument.Parse(appsettingsMemory, _jsonDocumentOptions);
        appsettingsRootElement = appsettingsJsonDocument.RootElement;

        var updatedOptionJsonElement = JsonSerializer.SerializeToElement(updatedOption);

        try
        {
            using var fileStream = new FileStream(JsonFilePhysicalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            using var utf8JsonWriter = new Utf8JsonWriter(fileStream, options: _jsonWriterOptions);
            WriteAppsSettingsJson(appsettingsRootElement, utf8JsonWriter, updatedOptionJsonElement);
            utf8JsonWriter.Flush();
        }
        catch (Exception exception)
        {
            LogWriteError(JsonFilePhysicalPath, exception);
            return false;
        }

        _configuration.Reload();
        return true;
    }

    private async Task<bool> UpdateJsonConfigurationAsync(TOptions updatedOption)
    {
        ReadOnlyMemory<byte> appsettingsMemory = await File.ReadAllBytesAsync(JsonFilePhysicalPath);
        using var appsettingsJsonDocument = JsonDocument.Parse(appsettingsMemory, _jsonDocumentOptions);
        var appsettingsRootElement = appsettingsJsonDocument.RootElement;

        var updatedOptionJsonElement = JsonSerializer.SerializeToElement(updatedOption);

        try
        {
            await using var fileStream = new FileStream(JsonFilePhysicalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            await using var utf8JsonWriter = new Utf8JsonWriter(fileStream, options: _jsonWriterOptions);

            WriteAppsSettingsJson(appsettingsRootElement, utf8JsonWriter, updatedOptionJsonElement);

            await utf8JsonWriter.FlushAsync();
        }
        catch (Exception exception)
        {
            LogWriteError(JsonFilePhysicalPath, exception);
            return false;
        }

        _configuration.Reload();
        return true;
    }

    private void WriteAppsSettingsJson(in JsonElement appsettingsRootElement, Utf8JsonWriter utf8JsonWriter, in JsonElement updatedOptionJsonElement)
    {
        utf8JsonWriter.WriteStartObject();

        var propertyFound = false;
        foreach (var property in appsettingsRootElement.EnumerateObject())
        {
            if (_configurationSection.Key.Equals(property.Name, StringComparison.Ordinal))
            {
                propertyFound = true;
                utf8JsonWriter.WritePropertyName(_configurationSection.Key);
                updatedOptionJsonElement.WriteTo(utf8JsonWriter);
            }
            else
            {
                property.WriteTo(utf8JsonWriter);
            }
        }

        if (!propertyFound)
        {
            utf8JsonWriter.WritePropertyName(_configurationSection.Key);
            updatedOptionJsonElement.WriteTo(utf8JsonWriter);
        }

        utf8JsonWriter.WriteEndObject();
    }
    #endregion

    #region Memory Configuration
    private bool UpdateMemoryConfiguration(TOptions updatedOption)
    {
        if (_configuration.Providers.FirstOrDefault(configurationProvider => configurationProvider.GetType() == typeof(MemoryConfigurationProvider)) is not MemoryConfigurationProvider memoryConfigurationProvider)
        {
            return  false;
        }

        SetMemoryKeyValuePairs(memoryConfigurationProvider, updatedOption);

        _configuration.Reload();
        return true;
    }

    private void SetMemoryKeyValuePairs(MemoryConfigurationProvider memoryConfigurationProvider, object option, string optionKeyPrefix = "")
    {
        var optionObjectType = option.GetType();
        var bindingFlags = BindingFlags.Public | BindingFlags.Instance;
        var properties = optionObjectType.GetProperties(bindingFlags);
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(option, null);
            var optionKey = string.IsNullOrEmpty(optionKeyPrefix) ? $"{optionObjectType.Name}:{property.Name}" : $"{optionKeyPrefix}:{property.Name}";

            if (propertyValue is null)
            {
                memoryConfigurationProvider.Set(optionKey, string.Empty);
            }
            else
            {
                var propertyType = propertyValue.GetType();
                if (propertyType != typeof(string) && propertyType.IsClass)
                {
                    SetMemoryKeyValuePairs(memoryConfigurationProvider, propertyValue, optionKey);
                }
                else
                {
                    memoryConfigurationProvider.Set(optionKey, propertyValue.ToString());
                }
            }            
        }
    }
    #endregion
}
