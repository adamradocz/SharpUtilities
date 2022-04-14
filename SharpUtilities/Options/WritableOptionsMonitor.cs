using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

/**
 * Based on: https://docs.microsoft.com/en-us/answers/questions/299791/saving-to-appsettingsjson.html
 */
namespace SharpUtilities.Options;

/// <summary>
/// Writable implementation of IOptionsMonitor.
/// </summary>
/// <typeparam name="TOptions">Options model.</typeparam>
public class WritableOptionsMonitor<TOptions> : OptionsMonitor<TOptions>, IWritableOptionsMonitor<TOptions> where TOptions : class
{
    private const string BaseFile = "appsettings.json";

    private readonly IConfigurationRoot _configuration;
    private readonly IConfigurationSection _configurationSection;
    private readonly ILogger<TOptions> _logger;
    private readonly string _appsettingsPhysicalPath;
    private readonly JsonDocumentOptions _jsonDocumentOptions;
    private readonly JsonWriterOptions _jsonWriterOptions;

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
        in IOptionsFactory<TOptions> factory,
        in IEnumerable<IOptionsChangeTokenSource<TOptions>> sources,
        in IOptionsMonitorCache<TOptions> cache,
        in IHostEnvironment hostEnvironment,
        in IConfigurationRoot configuration,
        in IConfigurationSection configurationSection,
        in ILogger<TOptions> logger) : base(factory, sources, cache)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (cache is null)
        {
            throw new ArgumentNullException(nameof(cache));
        }

        if (hostEnvironment is null)
        {
            throw new ArgumentNullException(nameof(hostEnvironment));
        }

        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configurationSection = configurationSection ?? throw new ArgumentNullException(nameof(configurationSection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _appsettingsPhysicalPath = GetAppSettingsPhysicalPath(BaseFile, hostEnvironment);

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
    /// Get the physical path of the appsettings.json.
    /// If the appsettings.{Environment}.json exists, than it retuns that one.
    /// </summary>
    /// <param name="baseFile">The base settings file, not the environment specific one.</param>
    /// <param name="hostEnvironment">Host environment</param>
    /// <returns>Environment specific physical path of the settings file if exists, otherwise the physical path of the base settings file.</returns>
    private static string GetAppSettingsPhysicalPath(in string baseFile, in IHostEnvironment hostEnvironment)
    {
        string environmentSpecificFileName = $"{Path.GetFileNameWithoutExtension(baseFile)}.{hostEnvironment.EnvironmentName}{Path.GetExtension(baseFile)}";
        string appsettingsPhysicalPath = Path.Combine(hostEnvironment.ContentRootPath, environmentSpecificFileName);

        if (!File.Exists(appsettingsPhysicalPath))
        {
            appsettingsPhysicalPath = Path.Combine(hostEnvironment.ContentRootPath, baseFile);
        }

        return appsettingsPhysicalPath;
    }

    /// <summary>
    /// Update the application settings file.
    /// </summary>
    /// <param name="applyChanges">Action to make the modification in the configuration section.</param>
    /// <returns><c>true</c> if success, otherwise <c>false</c></returns>
    public bool Update(Action<TOptions> applyChanges)
    {
        ReadOnlyMemory<byte> appsettingsMemory = File.ReadAllBytes(_appsettingsPhysicalPath);

        JsonElement appsettingsRootElement;
        using var appsettingsJsonDocument = JsonDocument.Parse(appsettingsMemory, _jsonDocumentOptions);
        appsettingsRootElement = appsettingsJsonDocument.RootElement;

        var optionObject = CurrentValue;
        applyChanges(optionObject);
        var updatedOptionJsonElement = JsonSerializer.SerializeToElement<TOptions>(optionObject);

        try
        {
            using var fileStream = new FileStream(_appsettingsPhysicalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            using var utf8JsonWriter = new Utf8JsonWriter(fileStream, options: _jsonWriterOptions);
            WriteAppsSettingsJson(appsettingsRootElement, utf8JsonWriter, updatedOptionJsonElement);
            utf8JsonWriter.Flush();
        }
        catch (Exception ex)
        {
            _logger.LogError("Couldn't write the settings. File path: {AppsettingsPhysicalPath}. Exception: {Exception}", _appsettingsPhysicalPath, ex);
            return false;
        }

        _configuration.Reload();
        return true;
    }

    /// <inheritdoc cref="Update(Action{TOptions})"/>
    /// <returns><c>Task<true></c> if success, otherwise <c>Task<false></c></returns>
    public async Task<bool> UpdateAsync(Action<TOptions> applyChanges)
    {
        ReadOnlyMemory<byte> appsettingsMemory = await File.ReadAllBytesAsync(_appsettingsPhysicalPath);
        using var appsettingsJsonDocument = JsonDocument.Parse(appsettingsMemory, _jsonDocumentOptions);
        var appsettingsRootElement = appsettingsJsonDocument.RootElement;

        var optionObject = CurrentValue;
        applyChanges(optionObject);
        var updatedOptionJsonElement = JsonSerializer.SerializeToElement<TOptions>(optionObject);

        try
        {
            await using var fileStream = new FileStream(_appsettingsPhysicalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            await using var utf8JsonWriter = new Utf8JsonWriter(fileStream, options: _jsonWriterOptions);

            WriteAppsSettingsJson(appsettingsRootElement, utf8JsonWriter, updatedOptionJsonElement);

            await utf8JsonWriter.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("Couldn't write the settings. File path: {AppsettingsPhysicalPath}. Exception: {Exception}", _appsettingsPhysicalPath, ex);
            return false;
        }

        _configuration.Reload();
        return true;
    }

    private void WriteAppsSettingsJson(in JsonElement appsettingsRootElement, in Utf8JsonWriter utf8JsonWriter, in JsonElement updatedOptionJsonElement)
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
}