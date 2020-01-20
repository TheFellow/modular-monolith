using Functional.Either;
using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Framework.Modules.Contracts;
using XPL.Modules.UserAccess.Application.UserRegistrations.Builder;
using XPL.Modules.UserAccess.Domain.UserRegistrations;
using XPL.Modules.UserAccess.Infrastructure.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.NewUserRegistration
{
    public class NewUserRegistrationCommandHandler : ICommandHandler<NewUserRegistrationCommand, NewUserRegistrationResponse>
    {
        private readonly UserRegistrationRepository _repository;
        private readonly Func<IUserRegistrationBuilder> _builderFactory;

        public NewUserRegistrationCommandHandler(
            UserRegistrationRepository repository,
            Func<IUserRegistrationBuilder> builderFactory)
        {
            _repository = repository;
            _builderFactory = builderFactory;
        }

        public async Task<Either<CommandError, NewUserRegistrationResponse>> Handle(NewUserRegistrationCommand request, CancellationToken cancellationToken)
        {
            Either<UserRegistrationError, UserRegistration> result = _builderFactory()
                .WithLogin(request.Login)
                .WithPassword(request.Password)
                .WithEmail(request.Email)
                .WithFirstName(request.FirstName)
                .WithLastName(request.LastName)
                .Build();

            if (result is Right<UserRegistrationError, UserRegistration> registration)
                _repository.Add(registration.Content);

            return result
                .Map(r => new NewUserRegistrationResponse(r, request.Login))
                .MapLeft(e => new CommandError(e.Error));
        }
    }
}
