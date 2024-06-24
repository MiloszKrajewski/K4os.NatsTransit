using K4os.KnownTypes;

namespace FlowDemo.Hosting.Extensions;

public static class Extensions
{
    public static void Register<T>(this KnownTypesRegistry registry, string name) =>
        registry.Register(name, typeof(T));
}
