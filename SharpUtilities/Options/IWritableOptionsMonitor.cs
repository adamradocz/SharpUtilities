using Microsoft.Extensions.Options;

namespace SharpUtilities.Options;

public interface IWritableOptionsMonitor<TOptions> : IOptionsMonitor<TOptions> where TOptions : class
{
    /// <summary>
    /// The file path of the JSON file what will be updated by <see cref="Update(Action{TOptions}, ConfigurationProvider)"/>.
    /// </summary>
    string JsonFilePhysicalPath { get; }

    /// <summary>
    /// Update the configuration.
    /// </summary>
    /// <param name="applyChanges">Action to make the modification in the configuration section.</param>
    /// <param name="providerFlags">Which configuration provider should be updated.</param>
    void Update(Action<TOptions> applyChanges, ConfigurationProvider providerFlags);

    /// <inheritdoc cref="Update(Action{TOptions}, ConfigurationProvider)"/>
    Task UpdateAsync(Action<TOptions> applyChanges, ConfigurationProvider providerFlags);
}
