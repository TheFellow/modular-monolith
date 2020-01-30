namespace XPL.Modules.UserAccess.Domain
{
    public class UserAccessError
    {
        public string Message { get; }

        public UserAccessError(string message) => Message = message;
    }
}