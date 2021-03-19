namespace Xpl.Framework.Messaging.IoC.Tests.TestModel
{
    public class Step1 : StepBase
    {
        public Step1(ICommandBus inner) : base(inner)
        {
        }
    }

}
