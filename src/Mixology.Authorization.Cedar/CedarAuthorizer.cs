using Cedar.Core;
using Cedar.Schema;
using Cedar.Types;
using Mixology.Application.Authentication;
using Mixology.Kernel.Errors;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Authorization.Cedar;

public sealed class CedarAuthorizer : IEntityAuthorizer
{
    private readonly PolicySet policies = new();
    private readonly Dictionary<string, SchemaValidator> validators;

    public CedarAuthorizer(IEnumerable<ICedarAuthorizationModule> modules)
    {
        try
        {
            validators = BuildCatalog(modules);
        }
        catch (Exception exception) when (exception is not AppError)
        {
            throw AppError.Internal("assemble Cedar authorization catalog", exception);
        }
    }

    public ValueTask AuthorizeAsync(
        Actor principal,
        KernelEntityUid action,
        Entity resource,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!validators.TryGetValue(resource.Uid.Type.Value, out SchemaValidator? validator))
            {
                throw AppError.Internal($"unknown Cedar resource type: {resource.Uid.Type}");
            }

            RequireValid(validator.ValidateEntity(resource), "Cedar resource");
            Request request = new(
                principal.ToCedarUid(),
                action.ToCedarUid(),
                resource.Uid,
                new CedarRecord());
            RequireValid(validator.ValidateRequest(request), "Cedar request");

            EntityMap entities = new([principal.ToCedarEntity(), resource]);
            (Decision decision, Diagnostic diagnostic) = global::Cedar.Core.Authorization.Authorize(policies, entities, request);
            if (!diagnostic.Errors.IsEmpty)
            {
                string details = string.Join(
                    "; ",
                    diagnostic.Errors
                        .OrderBy(static item => item.PolicyId.Value, StringComparer.Ordinal)
                        .Select(static item => item.ToString()));
                throw AppError.Internal($"Cedar evaluation failed: {details}");
            }

            if (decision != Decision.Allow)
            {
                throw AppError.Permission(
                    $"{principal.Id} cannot perform {action.Id} on {resource.Uid}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AppError)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw AppError.Internal("evaluate Cedar authorization", exception);
        }
    }

    private Dictionary<string, SchemaValidator> BuildCatalog(IEnumerable<ICedarAuthorizationModule> modules)
    {
        Dictionary<string, SchemaValidator> result = new(StringComparer.Ordinal);
        HashSet<string> policyDocuments = new(StringComparer.Ordinal);
        foreach (ICedarAuthorizationModule module in modules.OrderBy(
                     static module => module.SchemaName,
                     StringComparer.Ordinal))
        {
            SchemaDocument schema = SchemaDocument.UnmarshalCedar(module.SchemaText, module.SchemaName);
            SchemaValidator validator = new(schema.Resolve());
            foreach (string resourceType in module.ResourceTypes)
            {
                if (!result.TryAdd(resourceType, validator))
                {
                    throw new InvalidOperationException($"duplicate Cedar resource type: {resourceType}");
                }
            }

            foreach (CedarPolicyDocument document in module.Policies.OrderBy(
                         static document => document.Name,
                         StringComparer.Ordinal))
            {
                if (!policyDocuments.Add(document.Name))
                {
                    throw new InvalidOperationException($"duplicate Cedar policy document: {document.Name}");
                }

                PolicySet parsed = PolicySet.ParseCedarFile(document.Name, document.Text);
                foreach ((PolicyId id, Policy policy) in parsed.All().OrderBy(
                             static entry => entry.Key.Value,
                             StringComparer.Ordinal))
                {
                    string generatedId = $"{document.Name}/{id.Value}";
                    RequireValid(validator.ValidatePolicy(generatedId, policy), $"Cedar policy {generatedId}");
                    policies.Add(new PolicyId(generatedId), policy);
                }
            }
        }

        return result;
    }

    private static void RequireValid(ValidationResult result, string subject)
    {
        if (!result.IsValid)
        {
            throw AppError.Internal($"invalid {subject}: {string.Join("; ", result.Errors)}");
        }
    }
}
