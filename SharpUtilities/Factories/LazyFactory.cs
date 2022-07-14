using Microsoft.Extensions.DependencyInjection;

namespace SharpUtilities.Factories;

public class LazyFactory<T> : ILazyFactory<T> where T : notnull
{
    private readonly Lazy<T> _lazy;

    public LazyFactory(IServiceProvider serviceProvider)
    {
        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        _lazy = new Lazy<T>(() => serviceProvider.GetRequiredService<T>());
    }

    public T Value => _lazy.Value;
}