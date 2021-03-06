﻿using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xpl.Core.Application;
using Xpl.Core.Domain;
using Xpl.Framework.Messaging;
using Xpl.Framework.Messaging.Commands;

namespace Xpl.Sandbox.Cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var ctr = Bootstrap.Container();

            var logger = ctr.GetInstance<ILogger>();

            var bus = ctr.GetInstance<ICommandBus>();
            await SendCommand(true, logger, bus);
            await SendCommand(false, logger, bus);
        }

        private static async Task SendCommand(bool success, ILogger logger, ICommandBus bus)
        {
            var cts = new CancellationTokenSource();
            var result = await bus.Send(new MyCommand(success), cts.Token);
            result
                .OnOk(i => logger.Information("Result is {Result}", i))
                .OnError(msg => logger.Information("Error is {Error}", msg));
        }
    }

    public class MyCommand : Command<int> {
        public bool Success { get; }
        public MyCommand(bool success) => Success = success;
    }
    public class MyCommandHandler : UseCase<MyCommand, int>
    {
        public override Task<int> Handle(MyCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine("Handled");
            if(request.Success) return Task.FromResult(4);
            throw new DomainException("This request was supposed to fail");
        }
    }
}
