using Functional.Option;
using XPL.Framework.Modules.Contracts;

namespace XPL.Modules.UserAccess.Application.Users.CreateUser.Rules
{
    public class IsValidUserNameRule : ICommandRule<CreateUserCommand>
    {
        public Option<CommandError> Validate(CreateUserCommand command)
        {
            string userName = command.UserName;

            if (string.IsNullOrWhiteSpace(userName))
                return new CommandError("The username cannot be empty");

            if (userName.Contains(' '))
                return new CommandError($"The username \"{command.UserName}\" contains spaces");

            return None.Value;
        }
    }
}
