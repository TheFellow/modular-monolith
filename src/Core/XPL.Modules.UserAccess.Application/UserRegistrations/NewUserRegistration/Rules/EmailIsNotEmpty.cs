using System;
using System.Linq.Expressions;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.NewUserRegistration.Rules
{
    public class EmailIsNotEmpty : NonEmptyRule<NewUserRegistrationCommand>
    {
        protected override Expression<Func<NewUserRegistrationCommand, string>> _selector => r => r.Email;
    }
}
