using System;
using XPL.Framework.Application.Modules.Contracts;

namespace XPL.Modules.UserAccess.Users.CreateUser
{
    public class CreateUserCommand : ICommand<CreateUserResponse>
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
