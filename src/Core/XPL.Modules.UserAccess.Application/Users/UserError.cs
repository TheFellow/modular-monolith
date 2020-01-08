namespace XPL.Modules.UserAccess.Application.Users
{
    public class UserError
    {
        public string Error { get; }
        public UserError(string error) => Error = error;
        public override string ToString() => Error;
    }
}