namespace GlucoseMonitor.Infrastructure.DependencyInjection;

public class ServiceContainer
{
    private readonly Dictionary<Type, object> _services = new();

    public void RegisterSingleton<TInterface, TImplementation>(TImplementation instance)
        where TImplementation : class, TInterface
    {
        _services[typeof(TInterface)] = instance;
    }

    public void RegisterSingleton<T>(T instance) where T : class
    {
        _services[typeof(T)] = instance;
    }

    public T GetService<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }
        throw new InvalidOperationException($"Service of type {typeof(T).Name} not registered");
    }

    public T? GetServiceOrDefault<T>() where T : class
    {
        return _services.TryGetValue(typeof(T), out var service) ? (T)service : null;
    }
}