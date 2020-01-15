using Functional.Either;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;
using XPL.Modules.UserAccess.Domain.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.ConfirmRegistration
{
    public class ConfirmRegistrationCommandHandler : ICommandHandler<ConfirmRegistrationCommand, ConfirmRegistrationResponse>
    {
        public async Task<Either<CommandError, ConfirmRegistrationResponse>> Handle(ConfirmRegistrationCommand request, CancellationToken cancellationToken)
        {
            var registrationId = new RegistrationId(request.RegistrationId);

            return new CommandError("Not implemented");
        }
    }
}
