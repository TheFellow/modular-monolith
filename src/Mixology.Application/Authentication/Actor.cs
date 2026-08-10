using Mixology.Kernel.Errors;

namespace Mixology.Application.Authentication;

public readonly record struct Actor
{
    private Actor(string id)
    {
        Id = id;
    }

    public string Id { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Id);

    public static Actor Anonymous { get; } = new("anonymous");
    public static Actor Owner { get; } = new("owner");
    public static Actor Manager { get; } = new("manager");
    public static Actor Sommelier { get; } = new("sommelier");
    public static Actor Bartender { get; } = new("bartender");

    public static Actor Parse(string? value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "" or "owner" => Owner,
            "anonymous" or "anon" => Anonymous,
            "manager" => Manager,
            "sommelier" => Sommelier,
            "bartender" => Bartender,
            _ => throw AppError.Invalid($"unknown actor: \"{value}\""),
        };
    }

    public override string ToString() => Id ?? string.Empty;
}
