using System;
using System.Linq.Expressions;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.ConfirmRegistration.Rules
{
    public class ConfirmationCodeIsNotEmpty : NonEmptyRule<ConfirmRegistrationCommand>
    {
        protected override Expression<Func<ConfirmRegistrationCommand, string>> _selector => c => c.ConfirmationCode;
    }
}
