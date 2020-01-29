using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Contracts;
using XPL.Modules.UserAccess.Domain.UserRegistrations;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UseCases.UserRegistrations.NewUserRegistration
{
    public class NewUserRegistrationCommandHandler : ICommandHandler<NewUserRegistrationCommand, NewUserRegistrationResponse>
    {
        private readonly UserRegistrationRepository _repository;
        private readonly Func<Builder> _builderFactory;

        public NewUserRegistrationCommandHandler(
            UserRegistrationRepository repository,
            Func<Builder> builderFactory)
        {
            _repository = repository;
            _builderFactory = builderFactory;
        }

        public async Task<NewUserRegistrationResponse> Handle(NewUserRegistrationCommand request, CancellationToken cancellationToken)
        {
            UserRegistration result = _builderFactory()
                .WithLogin(request.Login)
                .WithPassword(request.Password)
                .WithEmail(request.Email)
                .WithFirstName(request.FirstName)
                .WithLastName(request.LastName)
                .Build();

            _repository.Add(result);

            return new NewUserRegistrationResponse(result, request.Login);
        }
    }
}
