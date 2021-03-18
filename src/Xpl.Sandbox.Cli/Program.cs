using Lamar;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xpl.Framework.Messaging;
using Xpl.Framework.Messaging.Commands;

namespace Xpl.Sandbox.Cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Explicit sandbox
            var ctr = new Container(c =>
            {
                c.IncludeRegistry<Framework.IoC>();
                c.Scan(scanner =>
                {
                    scanner.AssemblyContainingType<Program>();
                    scanner.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<,>));
                });
            });

            var logger = ctr.GetInstance<ILogger>();

            var bus = ctr.GetInstance<ICommandBus>();
            var result = await bus.Send(new MyCommand());
            result
                .OnOk(i => logger.Information("Result is {Result}", i))
                .OnError(msg => logger.Information("Error is {Error}", msg));
        }
    }

    public class MyCommand : Command<int> { }
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        public Task<int> Handle(MyCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine("Handled");
            throw new Exception("Unexpected exception!");
            return Task.FromResult(4);
        }
    }
}
