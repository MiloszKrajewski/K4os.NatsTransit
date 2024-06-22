using K4os.KnownTypes;

namespace K4os.NatsTransit.Api.Extensions;

public static class Extensions
{
    public static void Register<T>(this KnownTypesRegistry registry, string name) =>
        registry.Register(name, typeof(T));
}
