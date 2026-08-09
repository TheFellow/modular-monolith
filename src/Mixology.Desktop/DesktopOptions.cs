using Mixology.Application.Authentication;

namespace Mixology.Desktop;

public sealed record DesktopOptions(string DatabasePath, Actor Actor)
{
    public static DesktopOptions Create(string? databasePath, string? actor)
    {
        string database = Path.GetFullPath(
            string.IsNullOrWhiteSpace(databasePath)
                ? Path.Combine("data", "mixology.db")
                : databasePath.Trim());
        return new DesktopOptions(database, Actor.Parse(actor));
    }
}
