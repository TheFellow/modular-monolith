using Functional.Option;

namespace XPL.Framework.Application.Modules.Contracts
{
    public interface ICommandValidator
    {
        Option<CommandErrorList> Validate<TResult>(ICommand<TResult> command);
    }
}
