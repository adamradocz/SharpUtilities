using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpUtilities.Options;

namespace SharpUtilities.Benchmarks.Options;

[MemoryDiagnoser, DisassemblyDiagnoser(printInstructionAddresses: true, printSource: true, exportDiff: true)]
public class WritableOptionsMonitorBenchmarks
{
    public class TestOption
    {
        public DateTime LastLaunchedAt { get; set; }
        public string[] StringSettings { get; set; } = Array.Empty<string>();
        public int[] IntSettings { get; set; } = Array.Empty<int>();
    }

    private readonly string _jsonFilePath;
    private readonly TestOption _option;
    private readonly IWritableOptionsMonitor<TestOption> _writableOptionsMonitor;

    public WritableOptionsMonitorBenchmarks()
    {
        _jsonFilePath = Path.GetTempFileName();
        File.WriteAllText(_jsonFilePath, "{}");

        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration => configuration.Sources.Clear())
            .ConfigureServices((hostBuilderContext, services) => services.ConfigureWritable<TestOption>(hostBuilderContext.Configuration.GetSection(nameof(TestOption)), options => options.JsonBaseFile = _jsonFilePath))
            .Build();

        _option = new TestOption()
        {
            LastLaunchedAt = DateTime.Now,
            StringSettings = Enumerable.Range(1, 100).Select((_) => Guid.NewGuid().ToString()).ToArray(),
            IntSettings = Enumerable.Range(1, 100).ToArray()
        };

        _writableOptionsMonitor = host.Services.GetRequiredService<IWritableOptionsMonitor<TestOption>>();
    }

    [Benchmark]
    public void Update() => _writableOptionsMonitor.Update(option =>
    {
        option.LastLaunchedAt = _option.LastLaunchedAt;
        option.StringSettings = _option.StringSettings;
        option.IntSettings = _option.IntSettings;
    }, ConfigurationProvider.Json);

    [GlobalCleanup]
    public void Teardown()
    {
        if (File.Exists(_jsonFilePath))
        {
            File.Delete(_jsonFilePath);
        }
    }
}
