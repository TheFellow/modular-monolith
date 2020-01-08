using Functional.Option;
using System.Text.RegularExpressions;
using XPL.Framework.Application.Modules.Contracts;

namespace XPL.Modules.UserAccess.Application.Users.CreateUser.Rules
{
    public class ValidateEmailAddress : ICommandRule<CreateUserCommand>
    {
        public Option<CommandError> Validate(CreateUserCommand command)
        {
            if (Regex.IsMatch(command.EmailAddress, @"\w+@\w+\.\w{2,5}"))
                return None.Value;

            return new CommandError($"Email address \"{command.EmailAddress}\" is not in a valid format");
        }
    }
}
