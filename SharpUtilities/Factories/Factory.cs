using Microsoft.Extensions.DependencyInjection;

namespace SharpUtilities.Factories;

public class Factory<T> : IFactory<T> where T : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ObjectFactory _factory;

    public Factory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _factory = ActivatorUtilities.CreateFactory(typeof(T), Type.EmptyTypes);
    }

    public T CreateObject() => (T)_factory(_serviceProvider, null);
}
