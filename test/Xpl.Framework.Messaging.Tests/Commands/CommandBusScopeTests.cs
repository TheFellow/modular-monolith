using FluentAssertions;
using Lamar;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xpl.Framework.Messaging.Commands;
using Xpl.Framework.Messaging.IoC;

namespace Xpl.Framework.Messaging.Tests.Commands
{
    [TestClass]
    public class CommandBusScopeTests
    {
        [TestMethod]
        public void CommandBusCreatesExpectedIoCScope()
        {
            var container = new Container(cfg =>
            {
                cfg.IncludeRegistry<MessagingRegistry>();

                cfg.For<OnlyOne>().Use<OnlyOne>().Singleton();
                cfg.For<PerTransaction>().Use<PerTransaction>().Scoped();
                cfg.For<AlwaysNew>().Use<AlwaysNew>().Transient();

                cfg.For<ICommandHandler<ScopeTestCommand, bool>>().Use<ScopeTestHandler>();
            });

            var sut = container.GetInstance<ICommandBus>();

            ScopeTestHandler handler1 = null, handler2 = null;
            var cmd1 = new ScopeTestCommand(handler => handler1 = handler);
            var cmd2 = new ScopeTestCommand(handler => handler2 = handler);

            sut.Send(cmd1, default);
            sut.Send(cmd2, default);

            handler1.Only1.Should().Be(handler1.Only2);
            handler2.Only1.Should().Be(handler2.Only2);
            handler1.Only1.Should().Be(handler2.Only1);

            handler1.Per1.Should().Be(handler1.Per2);
            handler2.Per1.Should().Be(handler2.Per2);
            handler1.Per1.Should().NotBe(handler2.Per1);

            handler1.New1.Should().NotBe(handler1.New2);
            handler1.New1.Should().NotBe(handler2.New1);
            handler1.New1.Should().NotBe(handler2.New2);
            handler2.New1.Should().NotBe(handler1.New2);
            handler2.New1.Should().NotBe(handler2.New2);
            handler2.New2.Should().NotBe(handler1.New2);
        }
    }

    public class ScopeTestCommand : ICommand<bool>
    {
        public Guid CorrelationId => Guid.Empty;
        public Action<ScopeTestHandler> OnReceive { get; }

        public ScopeTestCommand(Action<ScopeTestHandler> onReceive) => OnReceive = onReceive;
    }
    public class ScopeTestHandler : ICommandHandler<ScopeTestCommand, bool>
    {
        public OnlyOne Only1 { get; }
        public OnlyOne Only2 { get; }
        public PerTransaction Per1 { get; }
        public PerTransaction Per2 { get; }
        public AlwaysNew New1 { get; }
        public AlwaysNew New2 { get; }

        public ScopeTestHandler(OnlyOne only1, OnlyOne only2, PerTransaction per1, PerTransaction per2, AlwaysNew new1, AlwaysNew new2)
        {
            Only1 = only1;
            Only2 = only2;
            Per1 = per1;
            Per2 = per2;
            New1 = new1;
            New2 = new2;
        }

        public Task<bool> Handle(ScopeTestCommand request, CancellationToken cancellationToken)
        {
            request.OnReceive(this);
            return Task.FromResult(true);
        }
    }

    public class GuidMark
    {
        public Guid Mark { get; } = Guid.NewGuid();
        public override string ToString() => Mark.ToString();
    }
    public class OnlyOne : GuidMark { }
    public class PerTransaction : GuidMark { }
    public class AlwaysNew : GuidMark { }
}
