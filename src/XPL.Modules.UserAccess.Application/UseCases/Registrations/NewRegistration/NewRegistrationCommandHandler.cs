using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Contracts;
using XPL.Modules.UserAccess.Domain.Registrations;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Registrations;

namespace XPL.Modules.UserAccess.Application.UseCases.Registrations.NewUserRegistration
{
    public class NewRegistrationCommandHandler : ICommandHandler<NewRegistrationCommand, NewRegistrationResponse>
    {
        private readonly RegistrationRepository _repository;
        private readonly Func<Builder> _builderFactory;

        public NewRegistrationCommandHandler(
            RegistrationRepository repository,
            Func<Builder> builderFactory)
        {
            _repository = repository;
            _builderFactory = builderFactory;
        }

        public Task<Result<NewRegistrationResponse>> Handle(NewRegistrationCommand request, CancellationToken cancellationToken)
        {
            Registration result = _builderFactory()
                .WithLogin(request.Login)
                .WithPassword(request.Password)
                .WithEmail(request.Email)
                .WithFirstName(request.FirstName)
                .WithLastName(request.LastName)
                .Build();

            _repository.Add(result);

            return request.Ok(new NewRegistrationResponse(result, request.Login));
        }
    }
}
