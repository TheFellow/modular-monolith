using Functional.Either;
using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;
using XPL.Modules.UserAccess.Application.UserRegistrations.Builder;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.RegisterNewUser
{
    public class RegisterNewUserCommandHandler : ICommandHandler<RegisterNewUserCommand, RegisterNewUserResponse>
    {
        private readonly Func<IUserRegistrationBuilder> _builderFactory;

        public RegisterNewUserCommandHandler(Func<IUserRegistrationBuilder> builderFactory) => _builderFactory = builderFactory;

        public async Task<Either<CommandError, RegisterNewUserResponse>> Handle(RegisterNewUserCommand request, CancellationToken cancellationToken)
        {
            var result = _builderFactory()
                .WithLogin(request.Login)
                .WithPassword(request.Password)
                .WithEmail(request.Email)
                .WithFirstName(request.FirstName)
                .WithLastName(request.LastName)
                .Build();

            return result
                .Map(r => new RegisterNewUserResponse(r))
                .MapLeft(e => new CommandError(e.Error));
        }
    }
}
