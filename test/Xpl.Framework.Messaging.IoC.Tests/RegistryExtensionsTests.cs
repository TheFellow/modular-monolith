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
                cfg.For<ICommandBus>().Use<CommandDispatcher>();
                cfg.RegisterPipelineFor<ICommandBus, Step1>();
            });

            var handler = container.GetInstance<ICommandBus>();

            handler.Should().BeOfType<Step1>()
                .Which.Inner.Should().BeOfType<CommandDispatcher>();
        }

        [TestMethod]
        public void Pipeline_2_Decorator_DecoratesTHandler()
        {
            var container = new Container(cfg =>
            {
                cfg.For<ICommandBus>().Use<CommandDispatcher>();
                cfg.RegisterPipelineFor<ICommandBus, Step1, Step2>();
            });

            var handler = container.GetInstance<ICommandBus>();

            handler.Should().BeOfType<Step1>()
                .Which.Inner.Should().BeOfType<Step2>()
                .Which.Inner.Should().BeOfType<CommandDispatcher>();
        }
        
        [TestMethod]
        public void Pipeline_3_Decorator_DecoratesTHandler()
        {
            var container = new Container(cfg =>
            {
                cfg.For<ICommandBus>().Use<CommandDispatcher>();
                cfg.RegisterPipelineFor<ICommandBus, Step1, Step2, Step3>();
            });

            var handler = container.GetInstance<ICommandBus>();

            handler.Should().BeOfType<Step1>()
                .Which.Inner.Should().BeOfType<Step2>()
                .Which.Inner.Should().BeOfType<Step3>()
                .Which.Inner.Should().BeOfType<CommandDispatcher>();
        }

        [TestMethod]
        public void Pipeline_4_Decorator_DecoratesTHandler()
        {
            var container = new Container(cfg =>
            {
                cfg.For<ICommandBus>().Use<CommandDispatcher>();
                cfg.RegisterPipelineFor<ICommandBus, Step1, Step2, Step3, Step4>();
            });

            var handler = container.GetInstance<ICommandBus>();

            handler.Should().BeOfType<Step1>()
                .Which.Inner.Should().BeOfType<Step2>()
                .Which.Inner.Should().BeOfType<Step3>()
                .Which.Inner.Should().BeOfType<Step4>()
                .Which.Inner.Should().BeOfType<CommandDispatcher>();
        }
    }
}
