using Mixology.Kernel.Errors;
using Mixology.Modules.Tagging.Models;

namespace Mixology.Modules.Tagging;

public sealed class TagTargetRegistry
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, TagTargetRegistration> registrations = new(StringComparer.Ordinal);

    public TagTargetRegistry()
    {
    }

    public TagTargetRegistry(IEnumerable<ITagTargetRegistrationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        foreach (ITagTargetRegistrationProvider provider in providers)
        {
            Register(provider.Registration);
        }
    }

    public void Register(TagTargetRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (string.IsNullOrWhiteSpace(registration.EntityType) ||
            registration.GetAction.IsEmpty ||
            registration.TagAction.IsEmpty ||
            registration.UntagAction.IsEmpty ||
            registration.LoadAsync is null ||
            registration.ActiveIdsAsync is null)
        {
            throw new InvalidOperationException("tagging: incomplete target registration");
        }

        lock (gate)
        {
            if (!registrations.TryAdd(registration.EntityType, registration))
            {
                throw new InvalidOperationException(
                    $"tagging: duplicate target registration for {registration.EntityType}");
            }
        }
    }

    public TagTargetRegistration Resolve(string entityType)
    {
        lock (gate)
        {
            return registrations.TryGetValue(entityType, out TagTargetRegistration? registration)
                ? registration
                : throw AppError.Invalid($"unsupported tag target type: {entityType}");
        }
    }
}
