namespace XPL.Framework.Domain.Contracts
{
    public class CommandResult
    {
        public bool Success { get; }
        public string Message { get; }

        private CommandResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static CommandResult Ok(string message) => new CommandResult(true, message);
        public static CommandResult Error(string message) => new CommandResult(false, message);
    }
}
