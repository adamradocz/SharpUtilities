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
    private const string _baseFile = "appsettings.json";

    private readonly IConfigurationRoot _configuration;
    private readonly IConfigurationSection _configurationSection;
    private readonly ILogger<TOptions> _logger;
    private readonly string _appsettingsPhysicalPath;
    private readonly JsonDocumentOptions _jsonDocumentOptions;
    private readonly JsonWriterOptions _jsonWriterOptions;

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
        IHostEnvironment hostEnvironment,
        IConfigurationRoot configuration,
        IConfigurationSection configurationSection,
        ILogger<TOptions> logger) : base(factory, sources, cache)
    {
        Guard.IsNotNull(hostEnvironment, nameof(hostEnvironment));
        
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configurationSection = configurationSection ?? throw new ArgumentNullException(nameof(configurationSection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _appsettingsPhysicalPath = GetAppSettingsPhysicalPath(_baseFile, hostEnvironment);

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

    /// <summary>
    /// Update the application settings file.
    /// </summary>
    /// <param name="applyChanges">Action to make the modification in the configuration section.</param>
    /// <returns><c>true</c> if success, otherwise <c>false</c></returns>
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

    /// <inheritdoc cref="Update(Action{TOptions})"/>
    /// <returns><c>Task<true></c> if success, otherwise <c>Task<false></c></returns>
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
        string environmentSpecificFileName = $"{Path.GetFileNameWithoutExtension(baseFile)}.{hostEnvironment.EnvironmentName}{Path.GetExtension(baseFile)}";
        string appsettingsPhysicalPath = Path.Combine(hostEnvironment.ContentRootPath, environmentSpecificFileName);

        if (!File.Exists(appsettingsPhysicalPath))
        {
            appsettingsPhysicalPath = Path.Combine(hostEnvironment.ContentRootPath, baseFile);
        }

        return appsettingsPhysicalPath;
    }

    private bool UpdateJsonConfiguration(TOptions updatedOption)
    {
        ReadOnlyMemory<byte> appsettingsMemory = File.ReadAllBytes(_appsettingsPhysicalPath);

        JsonElement appsettingsRootElement;
        using var appsettingsJsonDocument = JsonDocument.Parse(appsettingsMemory, _jsonDocumentOptions);
        appsettingsRootElement = appsettingsJsonDocument.RootElement;

        var updatedOptionJsonElement = JsonSerializer.SerializeToElement(updatedOption);

        try
        {
            using var fileStream = new FileStream(_appsettingsPhysicalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            using var utf8JsonWriter = new Utf8JsonWriter(fileStream, options: _jsonWriterOptions);
            WriteAppsSettingsJson(appsettingsRootElement, utf8JsonWriter, updatedOptionJsonElement);
            utf8JsonWriter.Flush();
        }
        catch (Exception exception)
        {
            LogWriteError(_appsettingsPhysicalPath, exception);
            return false;
        }

        _configuration.Reload();
        return true;
    }

    private async Task<bool> UpdateJsonConfigurationAsync(TOptions updatedOption)
    {
        ReadOnlyMemory<byte> appsettingsMemory = await File.ReadAllBytesAsync(_appsettingsPhysicalPath);
        using var appsettingsJsonDocument = JsonDocument.Parse(appsettingsMemory, _jsonDocumentOptions);
        var appsettingsRootElement = appsettingsJsonDocument.RootElement;

        var updatedOptionJsonElement = JsonSerializer.SerializeToElement(updatedOption);

        try
        {
            await using var fileStream = new FileStream(_appsettingsPhysicalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            await using var utf8JsonWriter = new Utf8JsonWriter(fileStream, options: _jsonWriterOptions);

            WriteAppsSettingsJson(appsettingsRootElement, utf8JsonWriter, updatedOptionJsonElement);

            await utf8JsonWriter.FlushAsync();
        }
        catch (Exception exception)
        {
            LogWriteError(_appsettingsPhysicalPath, exception);
            return false;
        }

        _configuration.Reload();
        return true;
    }

    private void WriteAppsSettingsJson(in JsonElement appsettingsRootElement, Utf8JsonWriter utf8JsonWriter, in JsonElement updatedOptionJsonElement)
    {
        utf8JsonWriter.WriteStartObject();

        bool propertyFound = false;
        foreach (var property in appsettingsRootElement.EnumerateObject())
        {
            if (_configurationSection.Key.Equals(property.Name))
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

        var optionObjectType = updatedOption.GetType();
        var bindingFlags = BindingFlags.Public | BindingFlags.Instance;
        var properties = optionObjectType.GetProperties(bindingFlags);
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(updatedOption, null);
            var optionKey = $"{optionObjectType.Name}:{property.Name}";
            if (propertyValue is null)
            {
                memoryConfigurationProvider.Set(optionKey, string.Empty);
            }
            else
            {
                memoryConfigurationProvider.Set(optionKey, propertyValue.ToString());
            }
        }
        
        _configuration.Reload();
        return true;
    }
    #endregion
}
