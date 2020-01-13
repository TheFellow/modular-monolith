using System;
using System.Linq.Expressions;

namespace XPL.Modules.UserAccess.Application.Users.CreateUser.Rules
{
    public class EmailIsNotEmpty : NonEmptyRule<CreateUserCommand>
    {
        protected override Expression<Func<CreateUserCommand, string>> _selector => c => c.EmailAddress;
    }
}
