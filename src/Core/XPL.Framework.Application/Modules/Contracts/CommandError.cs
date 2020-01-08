namespace XPL.Framework.Application.Modules.Contracts
{
    public class CommandError : ICommandError
    {
        public string Error { get; }
        public CommandError(string error) => Error = error;
        public override string ToString() => Error;
    }
}
