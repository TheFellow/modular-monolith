namespace Xpl.Framework.Messaging.IoC.Tests.TestModel
{
    public class ResultLogger : ICommandBus
    {
        public ICommandBus Inner { get; }
        public ResultLogger(ICommandBus inner) => Inner = inner;
    }

}
