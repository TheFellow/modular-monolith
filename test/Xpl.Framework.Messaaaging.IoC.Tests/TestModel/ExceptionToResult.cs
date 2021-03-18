namespace Xpl.Framework.Messaging.IoC.Tests.TestModel
{
    public class ExceptionToResult : ICommandBus
    {
        public ICommandBus Inner { get; }
        public ExceptionToResult(ICommandBus inner) => Inner = inner;
    }

}
