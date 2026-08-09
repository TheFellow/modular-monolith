using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mixology.DispatchGenerator;

public static class DispatcherGenerator
{
    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string Generate(string manifestJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestJson);
        DispatcherManifest manifest = JsonSerializer.Deserialize<DispatcherManifest>(manifestJson, JsonOptions)
            ?? throw new ArgumentException("Dispatcher manifest is empty.", nameof(manifestJson));
        ValidatedManifest model = Validate(manifest);
        return Render(model);
    }

    private static ValidatedManifest Validate(DispatcherManifest manifest)
    {
        if (manifest.Version != 1)
        {
            throw new ArgumentException($"Unsupported dispatcher manifest version: {manifest.Version}.");
        }

        ValidateIdentifier(manifest.Namespace, qualified: true, "namespace");
        ValidateIdentifier(manifest.ClassName, qualified: false, "className");

        RouteManifest[] routes = manifest.Routes
            ?? throw new ArgumentException("Dispatcher manifest requires a routes array.");
        List<ValidatedRoute> validatedRoutes = new(routes.Length);
        HashSet<string> eventNames = new(StringComparer.Ordinal);

        foreach (RouteManifest route in routes)
        {
            ValidateIdentifier(route.Event, qualified: true, "event");
            if (!eventNames.Add(route.Event!))
            {
                throw new ArgumentException($"Duplicate event route: {route.Event}");
            }

            HandlerManifest[] handlers = route.Handlers
                ?? throw new ArgumentException($"Route {route.Event} requires a handlers array.");
            if (handlers.Length == 0)
            {
                throw new ArgumentException($"Route {route.Event} requires at least one handler.");
            }

            HashSet<string> handlerNames = new(StringComparer.Ordinal);
            List<ValidatedHandler> validatedHandlers = new(handlers.Length);
            foreach (HandlerManifest handler in handlers)
            {
                ValidateIdentifier(handler.Type, qualified: true, "handler type");
                if (!handlerNames.Add(handler.Type!))
                {
                    throw new ArgumentException(
                        $"Duplicate handler {handler.Type} for event {route.Event}.");
                }

                validatedHandlers.Add(new ValidatedHandler(handler.Type!, handler.Prepare));
            }

            validatedHandlers.Sort(static (left, right) =>
                StringComparer.Ordinal.Compare(left.Type, right.Type));
            validatedRoutes.Add(new ValidatedRoute(route.Event!, validatedHandlers));
        }

        validatedRoutes.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.Event, right.Event));
        return new ValidatedManifest(manifest.Namespace!, manifest.ClassName!, validatedRoutes);
    }

    private static void ValidateIdentifier(string? value, bool qualified, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Dispatcher manifest {label} is required.");
        }

        string[] segments = value.Split('.');
        if ((!qualified && segments.Length != 1)
            || segments.Any(static segment => !IsIdentifier(segment)))
        {
            throw new ArgumentException($"Invalid C# {label}: {value}");
        }
    }

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0
            || CSharpKeywords.Contains(value)
            || !(value[0] == '_' || char.IsLetter(value[0])))
        {
            return false;
        }

        foreach (char character in value.AsSpan(1))
        {
            if (character != '_' && !char.IsLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static string Render(ValidatedManifest manifest)
    {
        StringBuilder source = new();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("using Microsoft.Extensions.Logging;");
        if (manifest.Routes.Count > 0)
        {
            source.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        }

        source.AppendLine("using Mixology.Application.Events;");
        source.AppendLine("using Mixology.Application.Operations;");
        source.AppendLine();
        source.Append("namespace ").Append(manifest.Namespace).AppendLine(";");
        source.AppendLine();
        source.Append("public sealed class ").Append(manifest.ClassName)
            .AppendLine(" : IDomainEventDispatcher");
        source.AppendLine("{");
        if (manifest.Routes.Count > 0)
        {
            source.AppendLine("    private readonly IServiceScopeFactory scopeFactory;");
        }

        source.Append("    private readonly ILogger<").Append(manifest.ClassName).AppendLine("> logger;");
        source.AppendLine();
        source.Append("    public ").Append(manifest.ClassName).Append('(');
        if (manifest.Routes.Count > 0)
        {
            source.AppendLine();
            source.AppendLine("        IServiceScopeFactory scopeFactory,");
            source.Append("        ILogger<").Append(manifest.ClassName).AppendLine("> logger)");
        }
        else
        {
            source.Append("ILogger<").Append(manifest.ClassName).AppendLine("> logger)");
        }

        source.AppendLine("    {");
        if (manifest.Routes.Count > 0)
        {
            source.AppendLine("        this.scopeFactory = scopeFactory;");
        }

        source.AppendLine("        this.logger = logger;");
        source.AppendLine("    }");
        source.AppendLine();

        if (manifest.Routes.Count == 0)
        {
            RenderEmptyDispatch(source);
        }
        else
        {
            RenderDispatch(source, manifest);
            for (int index = 0; index < manifest.Routes.Count; index++)
            {
                source.AppendLine();
                RenderRoute(source, manifest.Routes[index], index);
            }
        }

        source.AppendLine("}");
        return source.ToString().ReplaceLineEndings("\n");
    }

    private static void RenderEmptyDispatch(StringBuilder source)
    {
        source.AppendLine("    public Task DispatchAsync(EventHandlerContext context, object domainEvent)");
        source.AppendLine("    {");
        source.AppendLine("        ArgumentNullException.ThrowIfNull(context);");
        source.AppendLine("        ArgumentNullException.ThrowIfNull(domainEvent);");
        source.AppendLine("        logger.LogDebug(\"Unhandled domain event {EventType}\", domainEvent.GetType().FullName);");
        source.AppendLine("        return Task.CompletedTask;");
        source.AppendLine("    }");
    }

    private static void RenderDispatch(StringBuilder source, ValidatedManifest manifest)
    {
        source.AppendLine("    public async Task DispatchAsync(EventHandlerContext context, object domainEvent)");
        source.AppendLine("    {");
        source.AppendLine("        ArgumentNullException.ThrowIfNull(context);");
        source.AppendLine("        ArgumentNullException.ThrowIfNull(domainEvent);");
        source.AppendLine();
        source.AppendLine("        switch (domainEvent)");
        source.AppendLine("        {");
        for (int index = 0; index < manifest.Routes.Count; index++)
        {
            ValidatedRoute route = manifest.Routes[index];
            source.Append("            case global::").Append(route.Event).AppendLine(" typedEvent:");
            source.Append("                await DispatchRoute").Append(index)
                .AppendLine("Async(context, typedEvent).ConfigureAwait(false);");
            source.AppendLine("                return;");
        }

        source.AppendLine("            default:");
        source.AppendLine("                logger.LogDebug(\"Unhandled domain event {EventType}\", domainEvent.GetType().FullName);");
        source.AppendLine("                return;");
        source.AppendLine("        }");
        source.AppendLine("    }");
    }

    private static void RenderRoute(StringBuilder source, ValidatedRoute route, int routeIndex)
    {
        source.Append("    private async Task DispatchRoute").Append(routeIndex)
            .Append("Async(EventHandlerContext context, global::").Append(route.Event)
            .AppendLine(" domainEvent)");
        source.AppendLine("    {");
        source.AppendLine("        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();");
        for (int index = 0; index < route.Handlers.Count; index++)
        {
            ValidatedHandler handler = route.Handlers[index];
            source.Append("        ");
            if (handler.Prepare)
            {
                source.Append("IPreparingDomainEventHandler");
            }
            else
            {
                source.Append("IDomainEventHandler");
            }

            source.Append("<global::").Append(route.Event).Append("> handler").Append(index)
                .AppendLine(" =");
            source.Append("            ActivatorUtilities.CreateInstance<global::")
                .Append(handler.Type).AppendLine(">(scope.ServiceProvider);");
        }

        foreach ((ValidatedHandler handler, int index) in route.Handlers.Select(static (item, index) => (item, index)))
        {
            if (handler.Prepare)
            {
                source.Append("        await handler").Append(index)
                    .AppendLine(".PrepareAsync(context, domainEvent).ConfigureAwait(false);");
            }
        }

        for (int index = 0; index < route.Handlers.Count; index++)
        {
            source.Append("        await handler").Append(index)
                .AppendLine(".HandleAsync(context, domainEvent).ConfigureAwait(false);");
        }

        source.AppendLine("    }");
    }

    private sealed record ValidatedManifest(
        string Namespace,
        string ClassName,
        IReadOnlyList<ValidatedRoute> Routes);

    private sealed record ValidatedRoute(string Event, IReadOnlyList<ValidatedHandler> Handlers);

    private sealed record ValidatedHandler(string Type, bool Prepare);

    private sealed class DispatcherManifest
    {
        [JsonPropertyName("version")]
        public int Version { get; init; }

        [JsonPropertyName("namespace")]
        public string? Namespace { get; init; }

        [JsonPropertyName("className")]
        public string? ClassName { get; init; }

        [JsonPropertyName("routes")]
        public RouteManifest[]? Routes { get; init; }
    }

    private sealed class RouteManifest
    {
        [JsonPropertyName("event")]
        public string? Event { get; init; }

        [JsonPropertyName("handlers")]
        public HandlerManifest[]? Handlers { get; init; }
    }

    private sealed class HandlerManifest
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("prepare")]
        public bool Prepare { get; init; }
    }
}
