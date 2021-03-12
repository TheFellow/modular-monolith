using Lamar;

namespace Xpl.Framework
{
    public class XplApp
    {
        public IContainer Container { get; }
        private XplApp() => Container = new Container(new XplRegistry());
        public static XplApp Start() => new XplApp();
    }
}
