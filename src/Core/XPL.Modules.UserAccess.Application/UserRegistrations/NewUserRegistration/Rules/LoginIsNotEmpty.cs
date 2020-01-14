using System;
using System.Linq.Expressions;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.NewUserRegistration.Rules
{
    public class LoginIsNotEmpty : NonEmptyRule<NewUserRegistrationCommand>
    {
        protected override Expression<Func<NewUserRegistrationCommand, string>> _selector => r => r.Login;
    }
}
