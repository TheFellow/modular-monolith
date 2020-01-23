namespace XPL.Modules.UserAccess.Domain.Users
{
    public class UserError
    {
        public string Message { get; }

        public UserError(string message) => Message = message;
    }
}