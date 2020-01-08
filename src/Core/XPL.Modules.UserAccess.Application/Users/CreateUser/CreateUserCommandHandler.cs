using Functional.Either;
using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;

namespace XPL.Modules.UserAccess.Application.Users.CreateUser
{
    public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, CreateUserResponse>
    {
        public async Task<Either<CommandError, CreateUserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            // Always succeed for now so we can test command rules
            return new CreateUserResponse(request.CorrelationId, new Random().Next(100), request.UserName);
        }
    }
}
