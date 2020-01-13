using System;
using System.Linq.Expressions;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.RegisterNewUser.Rules
{
    public class EmailIsNotEmpty : NonEmptyRule<RegisterNewUserCommand>
    {
        protected override Expression<Func<RegisterNewUserCommand, string>> _selector => r => r.Email;
    }
}
