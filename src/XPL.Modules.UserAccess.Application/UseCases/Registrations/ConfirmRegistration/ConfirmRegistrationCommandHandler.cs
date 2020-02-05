using Functional.Either;
using Functional.Option;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Contracts;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Registrations;

namespace XPL.Modules.UserAccess.Application.UseCases.Registrations.ConfirmRegistration
{
    public class ConfirmRegistrationCommandHandler : ICommandHandler<ConfirmRegistrationCommand, string>
    {
        private readonly RegistrationRepository _repository;
        public ConfirmRegistrationCommandHandler(RegistrationRepository repository) => _repository = repository;

        public Task<Result<string>> Handle(ConfirmRegistrationCommand request, CancellationToken cancellationToken) =>
            _repository
                .TryFind(request.Login)
                .Tee(u => u.Confirm(request.ConfirmationCode))
                .Map(_ => request.Ok("Registration confirmed"))
                .Reduce(request.Fail("Registration Id not found"));
    }
}
