using Functional.Either;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Modules.Contracts;

namespace XPL.Modules.UserAccess.Application.Users.CreateUser
{
    public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Either<UserError, CreateUserResponse>>
    {
        public async Task<Either<UserError, CreateUserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            // Dummy response until we have a domain model
            if (request.UserName == "Alice")
                return CreateUserResponse.Ok(request.CorrelationId, 10);

            return new UserError($"User {request.UserName} already exists.");
        }
    }
}
