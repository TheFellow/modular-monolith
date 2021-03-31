using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xpl.Core.Domain;
using Xpl.Framework.Messaging.Commands;
using Xpl.Framework.Messaging.Commands.Pipeline;

namespace Xpl.Framework.Messaging.Tests.Commands.Pipeline
{
    [TestClass]
    public class ExceptionToResultTests_Struct : ExceptionToResultTests<int>
    {
        protected override int OkResult => 4;
    }

    [TestClass]
    public class ExceptionToResultTests_Class : ExceptionToResultTests<object>
    {
        protected override object OkResult { get; } = new object();
    }

    public abstract class ExceptionToResultTests<TResult>
    {
        private static Command<TResult> _command;
        private static CancellationTokenSource _cancellationTokenSource;
        private static CancellationToken _cancellationToken;

        private static Mock<ICommandDispatcher> _dispatcherMock;
        private static Mock<ILogger> _loggerMock;

        private static ExceptionToResult GetSut() => GetSut(_dispatcherMock.Object, _loggerMock.Object);
        private static ExceptionToResult GetSut(ICommandDispatcher dispatcher, ILogger logger) => new(dispatcher, logger);

        protected abstract TResult OkResult { get; }

        [TestInitialize]
        public async Task TestInit()
        {
            _command = new Command<TResult>();
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;

            _dispatcherMock = new Mock<ICommandDispatcher>();
            _loggerMock = new Mock<ILogger>();
            await Task.CompletedTask;
        }

        [TestMethod]
        public async Task Send_SendsCommandAndCancellationTokenAsync()
        {
            _dispatcherMock.Setup(d => d.Send(_command, _cancellationToken)).Verifiable();

            var sut = GetSut();
            var result = await sut.Send(_command, _cancellationToken);

            _dispatcherMock.Verify();
        }

        [TestMethod]
        public void NoException_ReturnsResult_OfDispatcher()
        {
            var dispatcherMock = new Mock<ICommandDispatcher>();
            dispatcherMock.Setup(d => d.Send(It.IsAny<ICommand<TResult>>(), It.IsAny<CancellationToken>()))
                .Returns(_command.Ok(OkResult));

            var sut = GetSut(dispatcherMock.Object, _loggerMock.Object);
            Func<Task<Result<TResult>>> func = () => sut.Send(_command, _cancellationToken);

            func.Should().NotThrow("no exception was thrown");
        }

        [TestMethod]
        public async Task DomainException_ReturnsError_WithMessageAsync()
        {
            var dispatcher = new ThrowingDispatcher(() => throw new DomainException("The message"));
            var sut = GetSut(dispatcher, _loggerMock.Object);

            var result = await sut.Send(_command, _cancellationToken);

            result.Should().BeOfType<Error<TResult>>("it threw an exception")
                .Which.Message.Should().Be("The message", "that was the exception message");
        }

        [TestMethod]
        public async Task Exception_ReturnsError_WithCorrelationIdAsync()
        {
            var dispatcher = new ThrowingDispatcher(() => throw new Exception("An non-domain exception!"));
            var sut = GetSut(dispatcher, _loggerMock.Object);

            var result = await sut.Send(_command, _cancellationToken);

            result.Should().BeOfType<Error<TResult>>("it threw an exception")
                .Which.Message.Should().Contain(_command.CorrelationId.ToString(), "The correlation id should be returned");
        }

        class ThrowingDispatcher : ICommandDispatcher
        {
            private readonly Action _onSend;

            public ThrowingDispatcher(Action onSend)
            {
                _onSend = onSend;
            }

            public Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken cancellationToken)
            {
                _onSend();
                Assert.Inconclusive("This dispatcher should throw");
                throw new InvalidOperationException();
            }
        }
    }
}
