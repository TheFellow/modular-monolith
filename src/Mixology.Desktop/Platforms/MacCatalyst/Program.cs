using UIKit;

namespace Mixology.Desktop;

public static class MacCatalystProgram
{
    public static void Main(string[] args) =>
        UIApplication.Main(args, null, typeof(AppDelegate));
}
