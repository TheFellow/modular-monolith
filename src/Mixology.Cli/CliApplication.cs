using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mixology.Application;
using Mixology.Application.Authentication;
using Mixology.Application.Events;
using Mixology.Dispatcher;
using Mixology.Kernel.Errors;
using Mixology.Migrations;
using Mixology.Modules.Audit;
using Mixology.Modules.Ingredients;
using Mixology.Persistence;

namespace Mixology.Cli;

public static class CliApplication
{
    public static RootCommand Build(TextWriter? output = null, TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;
        RootCommand root = new("Manage the Mixology modular-monolith application.");
        Option<string> database = new("--db")
        {
            Description = "Path to the device-local Mixology SQLite database.",
            DefaultValueFactory = _ => Path.GetFullPath("mixology.db"),
        };
        Option<string> actor = new("--actor", "--as")
        {
            Description = "Actor identity: owner, manager, sommelier, bartender, or anonymous.",
            DefaultValueFactory = _ => "owner",
        };
        root.Options.Add(database);
        root.Options.Add(actor);

        Command status = new("status", "Initialize storage and report foundation readiness.");
        status.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                string databasePath = parseResult.GetValue(database)
                    ?? throw AppError.Invalid("database path is required");
                _ = Actor.Parse(parseResult.GetValue(actor));
                HostApplicationBuilder builder = MixologyHost.CreateBuilder([]);
                builder.Services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
                builder.AddMixology(databasePath, typeof(MigrationAssemblyMarker).Assembly);
                builder.Services.AddAuditModule();
                builder.Services.AddIngredientsModule();
                using IHost host = builder.Build();
                await host.Services.GetRequiredService<MixologyStore>()
                    .InitializeAsync(cancellationToken)
                    .ConfigureAwait(false);
                await output.WriteLineAsync("Mixology foundation is ready.").ConfigureAwait(false);
                return 0;
            }
            catch (Exception exception)
            {
                return await CliErrorAdapter.WriteAsync(error, exception).ConfigureAwait(false);
            }
        });

        root.Subcommands.Add(status);
        return root;
    }
}

public static class CliErrorAdapter
{
    public static async Task<int> WriteAsync(TextWriter error, Exception exception)
    {
        AppError? applicationError = AppError.Find(exception);
        if (applicationError is not null)
        {
            await error.WriteLineAsync(applicationError.UserMessage).ConfigureAwait(false);
            return applicationError.Spec.CliExitCode;
        }

        if (AppError.IsCancellation(exception))
        {
            await error.WriteLineAsync("operation cancelled").ConfigureAwait(false);
            return ErrorCatalog.ExitGeneral;
        }

        await error.WriteLineAsync("internal error").ConfigureAwait(false);
        return ErrorCatalog.ExitGeneral;
    }
}
