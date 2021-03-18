using Lamar;

namespace Xpl.Framework.Messaging.IoC
{
    public static class RegistryExtensions
    {
        public static void RegisterPipelineFor<TInterface, T1>(this ServiceRegistry registry)
            where T1 : TInterface
            where TInterface : class
        {
            registry.For<TInterface>().DecorateAllWith<T1>();
        }

        public static void RegisterPipelineFor<TInterface, T1, T2>(this ServiceRegistry registry)
            where T2 : TInterface
            where T1 : TInterface
            where TInterface : class
        {
            registry.For<TInterface>().DecorateAllWith<T2>();
            registry.For<TInterface>().DecorateAllWith<T1>();
        }

        public static void RegisterPipelineFor<TInterface, T1, T2, T3>(this ServiceRegistry registry)
            where T3 : TInterface
            where T2 : TInterface
            where T1 : TInterface
            where TInterface : class
        {
            registry.For<TInterface>().DecorateAllWith<T3>();
            registry.For<TInterface>().DecorateAllWith<T2>();
            registry.For<TInterface>().DecorateAllWith<T1>();
        }
    }
}