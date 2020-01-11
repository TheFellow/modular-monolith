using Functional.Either;
using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;
using static XPL.Modules.UserAccess.Domain.UserRegistrations.UserRegistration;

namespace XPL.Modules.UserAccess.Application.UserRegistrations
{
    public class RegisterNewUserCommandHandler : ICommandHandler<RegisterNewUserCommand, RegisterNewUserResponse>
    {
        private readonly Func<UserRegistrationBuilder> _builderFactory;

        public RegisterNewUserCommandHandler(Func<UserRegistrationBuilder> builderFactory) => _builderFactory = builderFactory;

        public async Task<Either<CommandError, RegisterNewUserResponse>> Handle(RegisterNewUserCommand request, CancellationToken cancellationToken)
        {
            var result = _builderFactory()
                .WithLogin(request.Login)
                .WithPassword(request.Password)
                .WithEmail(request.Email)
                .WithFirstName(request.FirstName)
                .WithLastName(request.LastName)
                .Build();

            return new RegisterNewUserResponse(); //
        }
    }
}
