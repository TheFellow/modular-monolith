using Functional.Either;
using Functional.Option;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;
using XPL.Modules.UserAccess.Infrastructure.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UseCases.UserRegistrations.ConfirmRegistration
{
    public class ConfirmRegistrationCommandHandler : ICommandHandler<ConfirmRegistrationCommand, ConfirmRegistrationResponse>
    {
        private readonly UserRegistrationRepository _repository;
        public ConfirmRegistrationCommandHandler(UserRegistrationRepository repository) => _repository = repository;

        public async Task<ConfirmRegistrationResponse> Handle(ConfirmRegistrationCommand request, CancellationToken cancellationToken)
        {
            var result = _repository
                .TryFind(request.RegistrationId)
                .Else("Cannot locate registration id")
                .Map(u => u.Confirm(request.ConfirmationCode)
                    .Map(_ => ConfirmRegistrationResponse.Confirmed)
                    .MapLeft(_ => "Invalid confirmation code"))
                .Reduce(err => ConfirmRegistrationResponse.Error(err));

            return result;
        }
    }
}
