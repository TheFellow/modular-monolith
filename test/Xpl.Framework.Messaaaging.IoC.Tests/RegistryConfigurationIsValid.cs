using Lamar;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xpl.Framework.Messaging.IoC.Tests
{
    [TestClass]
    public class RegistryConfigurationIsValid
    {
        [TestMethod]
        public void MessagingRegistryIsValid()
        {
            var reg = new ModuleRegistry();
            var container = new Container(reg);

            container.AssertConfigurationIsValid();
        }
    }
}
