using Functional.Option;
using System;
using XPL.Framework.Modules.Contracts;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.ConfirmRegistration.Rules
{
    public class RegistrationIdIsNotEmpty : ICommandRule<ConfirmRegistrationCommand>
    {
        public Option<CommandError> Validate(ConfirmRegistrationCommand command)
        {
            if (command.RegistrationId == Guid.Empty)
                return new CommandError("Registration Id cannot be empty.");

            return None.Value;
        }
    }
}
