using Functional.Option;

namespace XPL.Framework.Modules.Contracts
{
    public interface ICommandValidator
    {
        Option<CommandErrorList> Validate<TResult>(ICommand<TResult> command);
    }
}
