using FluentAssertions;
using Lamar;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xpl.Framework.Messaging.IoC.Tests.TestModel;

namespace Xpl.Framework.Messaging.IoC.Tests
{
    [TestClass]
    public class RegistryExtensionsTests
    {
        [TestMethod]
        public void Pipeline_1_Decorator_DecoratesTHandler()
        {
            var container = new Container(cfg =>
            {
                cfg.For<ICommandBus>().Use<CommandBus>();
                cfg.RegisterPipelineFor<ICommandBus, ResultLogger>();
            });

            var handler = container.GetInstance<ICommandBus>();

            handler.Should().BeOfType<ResultLogger>()
                .Which.Inner.Should().BeOfType<CommandBus>();
        }

        [TestMethod]
        public void Pipeline_2_Decorator_DecoratesTHandler()
        {
            var container = new Container(cfg =>
            {
                cfg.For<ICommandBus>().Use<CommandBus>();
                cfg.RegisterPipelineFor<ICommandBus, ResultLogger, ExceptionToResult>();
            });

            var handler = container.GetInstance<ICommandBus>();

            handler.Should().BeOfType<ResultLogger>()
                .Which.Inner.Should().BeOfType<ExceptionToResult>()
                .Which.Inner.Should().BeOfType<CommandBus>();
        }

        [TestMethod]
        public void Pipeline_3_Decorator_DecoratesTHandler()
        {
            var container = new Container(cfg =>
            {
                cfg.For<ICommandBus>().Use<CommandBus>();
                cfg.RegisterPipelineFor<ICommandBus, ResultLogger, ExceptionToResult>();
            });

            var handler = container.GetInstance<ICommandBus>();

            handler.Should().BeOfType<ResultLogger>()
                .Which.Inner.Should().BeOfType<ExceptionToResult>()
                .Which.Inner.Should().BeOfType<CommandBus>();
        }

        [TestMethod]
        public void Pipeline_4_Decorator_DecoratesTHandler()
        {
            var container = new Container(cfg =>
            {
                cfg.For<ICommandBus>().Use<CommandBus>();
                cfg.RegisterPipelineFor<ICommandBus, ResultLogger, ExceptionToResult, CommandValidator>();
            });

            var handler = container.GetInstance<ICommandBus>();

            handler.Should().BeOfType<ResultLogger>()
                .Which.Inner.Should().BeOfType<ExceptionToResult>()
                .Which.Inner.Should().BeOfType<CommandValidator>()
                .Which.Inner.Should().BeOfType<CommandBus>();
        }
    }
}
