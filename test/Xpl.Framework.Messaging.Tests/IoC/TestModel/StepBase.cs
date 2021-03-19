namespace Xpl.Framework.Messaging.IoC.Tests.TestModel
{
    public class StepBase : ICommandBus
    {
        public ICommandBus Inner { get; }
        public StepBase(ICommandBus inner) => Inner = inner;
    }
}