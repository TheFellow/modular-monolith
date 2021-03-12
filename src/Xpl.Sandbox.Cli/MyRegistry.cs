namespace Xpl.Sandbox.Cli
{
    internal class MyRegistry : ServiceRegistry
    {
        public MyRegistry()
        {
            Scan(scanner =>
            {
                scanner.AssemblyContainingType<MyRegistry>();
                scanner.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<,>));
            });
        }
    }
}