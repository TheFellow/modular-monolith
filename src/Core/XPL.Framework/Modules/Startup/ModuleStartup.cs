using Lamar;
using XPL.Framework.Modules.Contracts;

namespace XPL.Framework.Modules.Startup
{
    public abstract class ModuleStartup
    {
        public abstract string ModuleName { get; }
        public abstract ServiceRegistry ModuleRegistry { get; }

        public ModuleStartup() => RegisterContractsTypes();

        private void RegisterContractsTypes() => ModuleRegistry.Scan(s =>
        {
            s.Assembly(GetType().Assembly);

            s.ConnectImplementationsToTypesClosing(typeof(ICommand));
            s.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<>));
            s.ConnectImplementationsToTypesClosing(typeof(ICommand<>));
            s.ConnectImplementationsToTypesClosing(typeof(ICommandHandler<,>));
            s.ConnectImplementationsToTypesClosing(typeof(IQuery<>));
            s.ConnectImplementationsToTypesClosing(typeof(IQueryHandler<,>));
        });
    }
}
