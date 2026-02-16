using Example.Bindings;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll.Microsoft.Extensions.DependencyInjection;

namespace Example.XUnit2;

public static class Setup
{
    private static IServiceCollection? _services;

    public static IServiceCollection Services
    {
        get
        {
            _services ??= new ServiceCollection()
                .AddScoped<IAssert,XUnit2Assert>();

            return _services;
        }
    }

    [ScenarioDependencies]
    public static IServiceCollection SetupDependencyInjection()
    {
        return Services;
    }
}