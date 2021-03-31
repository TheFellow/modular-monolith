using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xpl.Framework.Messaging.Commands;
using Xpl.Framework.Messaging.Commands.Pipeline;

namespace Xpl.Framework.Messaging.Tests.Commands.Pipeline
{
    [TestClass]
    public class CommandResultLoggerTests
    {
        class Command : ICommand<Response>
        {
            public Guid CorrelationId { get; } = Guid.NewGuid();
        }
        class Response { }

        [TestMethod]
        public async Task Send_InvokesDispatcher_WithCommandAndCancellationTokenAsync()
        {
            var cmd = new Command();
            var token = new CancellationTokenSource().Token;

            var dispatcher = new Mock<ICommandDispatcher>();
            dispatcher.Setup(d => d.Send(cmd, token))
                .Verifiable("the command and cancellation token should flow through");
            var logger = new Mock<IDispatcherLogger>();

            var sut = new CommandResultLogger(dispatcher.Object, logger.Object);
            await sut.Send(cmd, token);

            dispatcher.Verify();
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task Send_LogsSending(bool isOk)
        {
            var dispatcher = new Mock<ICommandDispatcher>();
            dispatcher.Setup(d => d.Send(It.IsAny<Command>(), default))
                .Returns(isOk ? Result.Ok(new Response()) : Result.Error<Response>("Error"));

            var logger = new Mock<IDispatcherLogger>();
            var cmd = new Command();

            var sut = new CommandResultLogger(dispatcher.Object, logger.Object);
            await sut.Send(cmd, default);

            logger.Verify(l => l.Sending(typeof(Command), cmd.CorrelationId), Times.Once);
        }

        [TestMethod]
        public async Task Send_OnOk_LogsSuccess()
        {
            var dispatcher = new Mock<ICommandDispatcher>();
            dispatcher.Setup(d => d.Send(It.IsAny<Command>(), default))
                .Returns(Result.Ok(new Response()));

            var logger = new Mock<IDispatcherLogger>();
            var cmd = new Command();

            var sut = new CommandResultLogger(dispatcher.Object, logger.Object);
            await sut.Send(cmd, default);

            logger.Verify(l => l.Success(cmd.CorrelationId), Times.Once);
        }

        [TestMethod]
        public async Task Send_OnError_LogsError()
        {
            var dispatcher = new Mock<ICommandDispatcher>();
            dispatcher.Setup(d => d.Send(It.IsAny<Command>(), default))
                .Returns(Result.Error<Response>("The error message"));

            var logger = new Mock<IDispatcherLogger>();
            var cmd = new Command();

            var sut = new CommandResultLogger(dispatcher.Object, logger.Object);
            await sut.Send(cmd, default);

            logger.Verify(l => l.Error(cmd.CorrelationId, "The error message"), Times.Once);
        }
    }
}
