using System.Threading.Tasks;
using XPL.Framework.Modules;
using XPL.Framework.Modules.Contracts;

namespace XPL.Modules.UserAccess
{
    public class UserAccessModule : IModule
    {
        private readonly Module _module;

        public UserAccessModule(Module module) => _module = module;

        public Task ExecuteCommandAsync(ICommand command) => _module.ExecuteCommandAsync(command);
        public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command) => ((IModule)_module).ExecuteCommandAsync(command);
        public Task ExecuteQueryAsync<TResult>(IQuery<TResult> query) => ((IModule)_module).ExecuteQueryAsync(query);
    }
}
