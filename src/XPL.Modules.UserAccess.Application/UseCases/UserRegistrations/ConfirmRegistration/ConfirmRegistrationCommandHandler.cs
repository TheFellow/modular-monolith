using Functional.Either;
using Functional.Option;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Domain.Contracts;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UseCases.UserRegistrations.ConfirmRegistration
{
    public class ConfirmRegistrationCommandHandler : ICommandHandler<ConfirmRegistrationCommand, CommandResult>
    {
        private readonly UserRegistrationRepository _repository;
        public ConfirmRegistrationCommandHandler(UserRegistrationRepository repository) => _repository = repository;

        public async Task<CommandResult> Handle(ConfirmRegistrationCommand request, CancellationToken cancellationToken)
        {
            var result = _repository
                .TryFind(request.RegistrationId)
                .Else("Cannot locate registration id")
                .Map(u => u.Confirm(request.ConfirmationCode)
                    .Map(_ => CommandResult.Ok("Registration confirmed."))
                    .MapLeft(_ => "Invalid confirmation code"))
                .Reduce(err => CommandResult.Error(err));

            return result;
        }
    }
}
