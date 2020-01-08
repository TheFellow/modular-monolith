using Functional.Either;
using System;
using XPL.Framework.Application.Modules.Contracts;

namespace XPL.Modules.UserAccess.Application.Users.CreateUser
{
    public class CreateUserCommand : ICommand<Either<UserError, CreateUserResponse>>
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string UserName { get; }
        public string EmailAddress { get; }

        public CreateUserCommand(string userName, string emailAddress)
        {
            UserName = userName;
            EmailAddress = emailAddress;
        }
    }
}
