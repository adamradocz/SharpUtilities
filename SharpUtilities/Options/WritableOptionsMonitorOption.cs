namespace SharpUtilities.Options;

public class WritableOptionsMonitorOption
{
    /// <summary>
    /// The JSON base file with extension.
    /// </summary>
    /// <remarks>The default value is: <c>appsettings.json</c></remarks>
    public string JsonBaseFile { get; set; } = "appsettings.json";
}
