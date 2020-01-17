using Functional.Either;
using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;
using XPL.Modules.UserAccess.Application.UserRegistrations.Builder;
using XPL.Modules.UserAccess.Domain.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.NewUserRegistration
{
    public class NewUserRegistrationCommandHandler : ICommandHandler<NewUserRegistrationCommand, NewUserRegistrationResponse>
    {
        private readonly IUserRegistrationRepository _repository;
        private readonly Func<IUserRegistrationBuilder> _builderFactory;

        public NewUserRegistrationCommandHandler(
            IUserRegistrationRepository repository,
            Func<IUserRegistrationBuilder> builderFactory)
        {
            _repository = repository;
            _builderFactory = builderFactory;
        }

        public async Task<Either<CommandError, NewUserRegistrationResponse>> Handle(NewUserRegistrationCommand request, CancellationToken cancellationToken)
        {
            var result = _builderFactory()
                .WithLogin(request.Login)
                .WithPassword(request.Password)
                .WithEmail(request.Email)
                .WithFirstName(request.FirstName)
                .WithLastName(request.LastName)
                .Build();

            if (result is Right<UserRegistrationError, UserRegistration> userRegistration)
                await _repository.AddAsync(userRegistration.Content);

            return result
                .Map(r => new NewUserRegistrationResponse(r, request.Login))
                .MapLeft(e => new CommandError(e.Error));
        }
    }
}
