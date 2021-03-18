using Lamar;
using Xpl.Framework.Messaging;

namespace Xpl.Sandbox.Cli
{
    public class Bootstrap
    {
        public static Container Container() =>
            new Container(c =>
                {
                    c.IncludeRegistry<Framework.IoC>();
                    c.Scan(scanner =>
                    {
                        scanner.AssemblyContainingType<Program>();
                        scanner.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<,>));
                        scanner.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<,>));
                    });
                });
    }
}