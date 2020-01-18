using Functional.Either;
using Functional.Option;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Framework.Modules.Contracts;
using XPL.Modules.UserAccess.Domain.UserRegistrations;
using XPL.Modules.UserAccess.Infrastructure.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.ConfirmRegistration
{
    public class ConfirmRegistrationCommandHandler : ICommandHandler<ConfirmRegistrationCommand, ConfirmRegistrationResponse>
    {
        private readonly UserRegistrationRepository _repository;
        public ConfirmRegistrationCommandHandler(UserRegistrationRepository repository) => _repository = repository;

        public async Task<Either<CommandError, ConfirmRegistrationResponse>> Handle(ConfirmRegistrationCommand request, CancellationToken cancellationToken)
        {
            return _repository
                .TryFind(request.RegistrationId)
                .Else("Cannot locate registration id")
                .Map(u => u.Confirm(request.ConfirmationCode)
                    .Map(_ => ConfirmRegistrationResponse.Confirmed)
                    .MapLeft(_ => "Invalid confirmation code"))
                .Reduce(err => ConfirmRegistrationResponse.Error(err));
        }
            //_repository.TryFind(request.RegistrationId) switch
            //{
            //    Some<UserRegistration> registration => registration.Content.Confirm(request.ConfirmationCode) switch
            //    {
            //        Left<InvalidConfirmationCode, UserRegistration> error => ConfirmRegistrationResponse.Error("Incorrect confirmation code"),
            //        _ => ConfirmRegistrationResponse.Confirmed
            //    },
            //    _ => ConfirmRegistrationResponse.Error("Cannot locate registration id")
            //};
}
}
