using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Hosting;
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
    private readonly JsonDocumentOptions _jsonDocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };
    private readonly JsonWriterOptions _jsonWriterOptions = new()
    {
        Indented = true,
        SkipValidation = false
    };

    /// <inheritdoc/>
    public string JsonFilePhysicalPath { get; }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="factory">The factory to use to create options.</param>
    /// <param name="sources">The sources used to listen for changes to the options instance.</param>
    /// <param name="cache">The cache used to store options.</param>
    /// <param name="hostEnvironment">Hosting environment.</param>
    /// <param name="configuration">IConfiguration root.</param>
    /// <param name="configurationSection">Configuration section.</param>
    public WritableOptionsMonitor(
        IOptionsFactory<TOptions> factory,
        IEnumerable<IOptionsChangeTokenSource<TOptions>> sources,
        IOptionsMonitorCache<TOptions> cache,
        IOptions<WritableOptionsMonitorOption> options,
        IHostEnvironment hostEnvironment,
        IConfigurationRoot configuration,
        IConfigurationSection configurationSection) : base(factory, sources, cache)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(options);
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configurationSection = configurationSection ?? throw new ArgumentNullException(nameof(configurationSection));

        JsonFilePhysicalPath = GetAppSettingsPhysicalPath(options.Value.JsonBaseFile, hostEnvironment);
    }

    /// <inheritdoc/>
    public void Update(Action<TOptions> applyChanges, ConfigurationProvider providerFlags)
    {
        var optionObject = CurrentValue;
        applyChanges(optionObject);
        
        if ((providerFlags & ConfigurationProvider.Json) == ConfigurationProvider.Json)
        {
            UpdateJsonConfiguration(optionObject);
        }

        if ((providerFlags & ConfigurationProvider.Memory) == ConfigurationProvider.Memory)
        {
            UpdateMemoryConfiguration(optionObject);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Action<TOptions> applyChanges, ConfigurationProvider providerFlags)
    {
        var optionObject = CurrentValue;
        applyChanges(optionObject);

        if ((providerFlags & ConfigurationProvider.Json) == ConfigurationProvider.Json)
        {
            await UpdateJsonConfigurationAsync(optionObject);
        }

        if ((providerFlags & ConfigurationProvider.Memory) == ConfigurationProvider.Memory)
        {
            UpdateMemoryConfiguration(optionObject);
        }
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

    private void UpdateJsonConfiguration(TOptions updatedOption)
    {
        ReadOnlyMemory<byte> appsettingsMemory = File.ReadAllBytes(JsonFilePhysicalPath);

        using var appsettingsJsonDocument = JsonDocument.Parse(appsettingsMemory, _jsonDocumentOptions);
        var appsettingsRootElement = appsettingsJsonDocument.RootElement;
        var updatedOptionJsonElement = JsonSerializer.SerializeToElement(updatedOption);

        using var fileStream = new FileStream(JsonFilePhysicalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        using var utf8JsonWriter = new Utf8JsonWriter(fileStream, options: _jsonWriterOptions);

        WriteAppsSettingsJson(appsettingsRootElement, utf8JsonWriter, updatedOptionJsonElement);

        utf8JsonWriter.Flush();
        _configuration.Reload();
    }

    private async Task UpdateJsonConfigurationAsync(TOptions updatedOption)
    {
        ReadOnlyMemory<byte> appsettingsMemory = await File.ReadAllBytesAsync(JsonFilePhysicalPath);

        using var appsettingsJsonDocument = JsonDocument.Parse(appsettingsMemory, _jsonDocumentOptions);
        var appsettingsRootElement = appsettingsJsonDocument.RootElement;
        var updatedOptionJsonElement = JsonSerializer.SerializeToElement(updatedOption);

        await using var fileStream = new FileStream(JsonFilePhysicalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        await using var utf8JsonWriter = new Utf8JsonWriter(fileStream, options: _jsonWriterOptions);

        WriteAppsSettingsJson(appsettingsRootElement, utf8JsonWriter, updatedOptionJsonElement);

        await utf8JsonWriter.FlushAsync();
        _configuration.Reload();
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
    /// <summary></summary>
    /// <param name="updatedOption"></param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no <see cref="MemoryConfigurationProvider"/> is added.
    /// </exception>
    private void UpdateMemoryConfiguration(TOptions updatedOption)
    {
        var memoryConfigurationProvider = (MemoryConfigurationProvider)_configuration.Providers.First(configurationProvider => configurationProvider.GetType() == typeof(MemoryConfigurationProvider));
        SetMemoryKeyValuePairs(memoryConfigurationProvider, updatedOption);
        _configuration.Reload();
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
