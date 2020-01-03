using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;

namespace XPL.Modules.UserAccess.Users.CreateUser
{
    public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, CreateUserResponse>
    {
        public Task<CreateUserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            // Dummy response until we have a domain model
            if (request.UserName == "Alice")
                return Task.FromResult(CreateUserResponse.Ok(10));

            return Task.FromResult(CreateUserResponse.Fail($"User {request.UserName} already exists"));
        }
    }
}
