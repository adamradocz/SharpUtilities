using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SharpUtilities.Options;
using System.Globalization;

namespace SharpUtilities.Tests.Options;

public class WritableOptionsMonitorTests
{
    private const int _updateDelay = 100;

    [Fact]
    public void Update_MemoryNoProviderAdded_NotUpdated()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration => configuration.Sources.Clear())
            .ConfigureServices((hostBuilderContext, services) => services.ConfigureWritable<TestOption>(hostBuilderContext.Configuration.GetSection(nameof(TestOption))))
            .Build();

        var writableOptionsMonitor = host.Services.GetRequiredService<IWritableOptionsMonitor<TestOption>>();

        // Act
        var isUpdated = writableOptionsMonitor.Update(newTestOption =>
        {
            newTestOption.TestBool = true;
            newTestOption.TestByte = 2;
            newTestOption.TestShort = 2;
            newTestOption.TestInt = 2;
            newTestOption.TestLong = 2;
            newTestOption.TestFloat = 2.2f;
            newTestOption.TestDouble = 2.2d;
            newTestOption.TestDecimal = 2.2m;
            newTestOption.TestString = "a";
            newTestOption.TestChar = 'a';
        }, SharpUtilities.Options.ConfigurationProvider.Memory);

        // Assert
        Assert.False(isUpdated);
    }

    [Fact]
    public async Task Update_MemoryEmptyProviderAdded_AddedSuccessfully()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                _ = configuration.AddInMemoryCollection();
            })
            .ConfigureServices((hostBuilderContext, services) =>
            {
                _ = services.Configure<TestOption>(hostBuilderContext.Configuration.GetSection(nameof(TestOption)))
                .ConfigureWritable<TestOption>(hostBuilderContext.Configuration.GetSection(nameof(TestOption)));
            })
            .Build();

        var optionsMonitor = host.Services.GetRequiredService<IOptionsMonitor<TestOption>>();
        var writableOptionsMonitor = host.Services.GetRequiredService<IWritableOptionsMonitor<TestOption>>();

        // Act
        var isUpdated = writableOptionsMonitor.Update(newTestOption =>
        {
            newTestOption.TestBool = true;
            newTestOption.TestByte = 2;
            newTestOption.TestShort = 2;
            newTestOption.TestInt = 2;
            newTestOption.TestLong = 2;
            newTestOption.TestFloat = 2.2f;
            newTestOption.TestDouble = 2.2d;
            newTestOption.TestDecimal = 2.2m;
            newTestOption.TestString = "a";
            newTestOption.TestChar = 'a';
        }, SharpUtilities.Options.ConfigurationProvider.Memory);

        // Wait for the update thread to catch up.
        await Task.Delay(TimeSpan.FromMilliseconds(_updateDelay));

        // Assert
        Assert.True(isUpdated);
        Assert.True(optionsMonitor.CurrentValue.TestBool);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestByte);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestShort);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestInt);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestLong);
        Assert.Equal(2.2f, optionsMonitor.CurrentValue.TestFloat);
        Assert.Equal(2.2d, optionsMonitor.CurrentValue.TestDouble);
        Assert.Equal(2.2m, optionsMonitor.CurrentValue.TestDecimal);
        Assert.Equal("a", optionsMonitor.CurrentValue.TestString);
        Assert.Equal('a', optionsMonitor.CurrentValue.TestChar);
        Assert.Null(optionsMonitor.CurrentValue.TestSubClass);
    }

    [Fact]
    public async Task Update_MemoryProviderWithDefaultOptionIsAdd_UpdatedSuccessfully()
    {
        // Arrange
        TestOption testOption = new();

        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();

                var testOptions = new Dictionary<string, string>()
                {
                    { $"{nameof(TestOption)}:{nameof(testOption.TestBool)}", testOption.TestBool.ToString()},
                    { $"{nameof(TestOption)}:{nameof(testOption.TestByte)}", testOption.TestByte.ToString(CultureInfo.InvariantCulture)},
                    { $"{nameof(TestOption)}:{nameof(testOption.TestShort)}", testOption.TestShort.ToString(CultureInfo.InvariantCulture)},
                    { $"{nameof(TestOption)}:{nameof(testOption.TestInt)}", testOption.TestInt.ToString(CultureInfo.InvariantCulture)},
                    { $"{nameof(TestOption)}:{nameof(testOption.TestLong)}", testOption.TestLong.ToString(CultureInfo.InvariantCulture)},
                    { $"{nameof(TestOption)}:{nameof(testOption.TestFloat)}", testOption.TestFloat.ToString(CultureInfo.InvariantCulture)},
                    { $"{nameof(TestOption)}:{nameof(testOption.TestDouble)}", testOption.TestDouble.ToString(CultureInfo.InvariantCulture)},
                    { $"{nameof(TestOption)}:{nameof(testOption.TestDecimal)}", testOption.TestDecimal.ToString(CultureInfo.InvariantCulture)},
                    { $"{nameof(TestOption)}:{nameof(testOption.TestString)}", testOption.TestString},
                    { $"{nameof(TestOption)}:{nameof(testOption.TestChar)}", testOption.TestChar.ToString()},
                    { $"{nameof(TestOption)}:{nameof(testOption.TestSubClass)}", string.Empty}
                };

                _ = configuration.AddInMemoryCollection(testOptions);
            })
            .ConfigureServices((hostBuilderContext, services) =>
            {
                _ = services.Configure<TestOption>(hostBuilderContext.Configuration.GetSection(nameof(TestOption)))
                .ConfigureWritable<TestOption>(hostBuilderContext.Configuration.GetSection(nameof(TestOption)));
            })
            .Build();

        var optionsMonitor = host.Services.GetRequiredService<IOptionsMonitor<TestOption>>();
        var writableOptionsMonitor = host.Services.GetRequiredService<IWritableOptionsMonitor<TestOption>>();

        // Act
        var isUpdated = writableOptionsMonitor.Update(newTestOption =>
        {
            newTestOption.TestBool = true;
            newTestOption.TestByte = 2;
            newTestOption.TestShort = 2;
            newTestOption.TestInt = 2;
            newTestOption.TestLong = 2;
            newTestOption.TestFloat = 2.2f;
            newTestOption.TestDouble = 2.2d;
            newTestOption.TestDecimal = 2.2m;
            newTestOption.TestString = "a";
            newTestOption.TestChar = 'a';
        }, SharpUtilities.Options.ConfigurationProvider.Memory);

        // Wait for the update thread to catch up.
        await Task.Delay(TimeSpan.FromMilliseconds(_updateDelay));

        // Assert
        Assert.True(isUpdated);
        Assert.True(optionsMonitor.CurrentValue.TestBool);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestByte);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestShort);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestInt);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestLong);
        Assert.Equal(2.2f, optionsMonitor.CurrentValue.TestFloat);
        Assert.Equal(2.2d, optionsMonitor.CurrentValue.TestDouble);
        Assert.Equal(2.2m, optionsMonitor.CurrentValue.TestDecimal);
        Assert.Equal("a", optionsMonitor.CurrentValue.TestString);
        Assert.Equal('a', optionsMonitor.CurrentValue.TestChar);
        Assert.Null(optionsMonitor.CurrentValue.TestSubClass);
    }

    [Fact]
    public async Task Update_MemorySubClassUpdated_UpdatedSuccessfully()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                _ = configuration.AddInMemoryCollection();
            })
            .ConfigureServices((hostBuilderContext, services) =>
            {
                _ = services.Configure<TestOption>(hostBuilderContext.Configuration.GetSection(nameof(TestOption)))
                .ConfigureWritable<TestOption>(hostBuilderContext.Configuration.GetSection(nameof(TestOption)));
            })
            .Build();

        var optionsMonitor = host.Services.GetRequiredService<IOptionsMonitor<TestOption>>();
        var writableOptionsMonitor = host.Services.GetRequiredService<IWritableOptionsMonitor<TestOption>>();

        // Act
        var isUpdated = writableOptionsMonitor.Update(newTestOption =>
        {
            newTestOption.TestBool = true;
            newTestOption.TestByte = 2;
            newTestOption.TestShort = 2;
            newTestOption.TestInt = 2;
            newTestOption.TestLong = 2;
            newTestOption.TestFloat = 2.2f;
            newTestOption.TestDouble = 2.2d;
            newTestOption.TestDecimal = 2.2m;
            newTestOption.TestString = "a";
            newTestOption.TestChar = 'a';
            newTestOption.TestSubClass = new()
            {
                TestBool = true,
                TestByte = 3,
                TestShort = 3,
                TestInt = 3,
                TestLong = 3,
                TestFloat = 3.3f,
                TestDouble = 3.3d,
                TestDecimal = 3.3m,
                TestString = "b",
                TestChar = 'b'
            };
        }, SharpUtilities.Options.ConfigurationProvider.Memory);

        // Wait for the update thread to catch up.
        await Task.Delay(TimeSpan.FromMilliseconds(_updateDelay));

        // Assert
        Assert.True(isUpdated);
        Assert.True(optionsMonitor.CurrentValue.TestBool);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestByte);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestShort);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestInt);
        Assert.Equal(2, optionsMonitor.CurrentValue.TestLong);
        Assert.Equal(2.2f, optionsMonitor.CurrentValue.TestFloat);
        Assert.Equal(2.2d, optionsMonitor.CurrentValue.TestDouble);
        Assert.Equal(2.2m, optionsMonitor.CurrentValue.TestDecimal);
        Assert.Equal("a", optionsMonitor.CurrentValue.TestString);
        Assert.Equal('a', optionsMonitor.CurrentValue.TestChar);
        Assert.NotNull(optionsMonitor.CurrentValue.TestSubClass);

        Assert.True(optionsMonitor.CurrentValue.TestSubClass.TestBool);
        Assert.Equal(3, optionsMonitor.CurrentValue.TestSubClass.TestByte);
        Assert.Equal(3, optionsMonitor.CurrentValue.TestSubClass.TestShort);
        Assert.Equal(3, optionsMonitor.CurrentValue.TestSubClass.TestInt);
        Assert.Equal(3, optionsMonitor.CurrentValue.TestSubClass.TestLong);
        Assert.Equal(3.3f, optionsMonitor.CurrentValue.TestSubClass.TestFloat);
        Assert.Equal(3.3d, optionsMonitor.CurrentValue.TestSubClass.TestDouble);
        Assert.Equal(3.3m, optionsMonitor.CurrentValue.TestSubClass.TestDecimal);
        Assert.Equal("b", optionsMonitor.CurrentValue.TestSubClass.TestString);
        Assert.Equal('b', optionsMonitor.CurrentValue.TestSubClass.TestChar);
    }
}