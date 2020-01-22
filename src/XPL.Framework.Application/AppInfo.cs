namespace XPL.Framework.Application
{
    public class AppInfo
    {
        public string Name { get; }
        public string Type { get; set; }

        public AppInfo(string appName, string type = "")
        {
            Name = appName;
            Type = type;
        }
    }
}