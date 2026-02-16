using Example.Bindings;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll.Microsoft.Extensions.DependencyInjection;

namespace Example.NUnit;

public static class Setup
{
    private static IServiceCollection? _services;

    public static IServiceCollection Services
    {
        get
        {
            _services ??= new ServiceCollection()
                .AddScoped<IAssert,NUnitAssert>();

            return _services;
        }
    }

    [ScenarioDependencies]
    public static IServiceCollection SetupDependencyInjection()
    {
        return Services;
    }
}