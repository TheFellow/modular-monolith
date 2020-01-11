using XPL.Framework.Modules.Contracts;

namespace XPL.Modules.UserAccess.Application.UserRegistrations
{
    public class RegisterNewUserCommand : ICommand<RegisterNewUserResponse>
    {
        public string Login { get; }
        public string Password { get; }
        public string Email { get; }
        public string FirstName { get; }
        public string LastName { get; }

        public RegisterNewUserCommand(string login, string password, string email, string firstName, string lastName)
        {
            Login = login;
            Password = password;
            Email = email;
            FirstName = firstName;
            LastName = lastName;
        }

    }
}
