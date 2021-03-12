using Lamar;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xpl.Framework.Messaging;
using Xpl.Framework.Messaging.Results;

namespace Xpl.Sandbox.Cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Explicit sandbox
            var ioc = new Framework.IoC();
            var ctr = new Container(c =>
            {
                c.IncludeRegistry<Framework.IoC>();
                c.Scan(scanner =>
                {
                    scanner.AssemblyContainingType<Program>();
                    scanner.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<,>));
                });
            });
            var bus = ctr.GetInstance<ICommandBus>();
            var result = await bus.Send(new MyCommand());
            Console.WriteLine($"Result is {((result is Ok<int> r) ? r.Value.ToString() : "Error")}");
        }

        public class MyCommand : ICommand<int> { }
        public class MyCommandHandler : ICommandHandler<MyCommand, int>
        {
            public Task<int> Handle(MyCommand request, CancellationToken cancellationToken)
            {
                Console.WriteLine("Handled");
                return Task.FromResult(4);
            }
        }
    }
}
