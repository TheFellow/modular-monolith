namespace Xpl.Framework.Messaging.IoC.Tests.TestModel
{
    public class CommandValidator : ICommandBus
    {
        public ICommandBus Inner { get; }
        public CommandValidator(ICommandBus inner) => Inner = inner;
    }

}
