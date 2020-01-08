namespace XPL.Framework.Modules.Contracts
{
    public class CommandError
    {
        public string Error { get; protected set; }
        public CommandError(string error) => Error = error;
        private protected CommandError() => Error = "";
        public override string ToString() => Error;
    }
}
