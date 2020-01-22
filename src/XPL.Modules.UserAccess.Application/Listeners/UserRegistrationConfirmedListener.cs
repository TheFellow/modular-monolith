using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Domain;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Events;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;

namespace XPL.Modules.UserAccess.Application.Listeners
{
    public class UserRegistrationConfirmedListener : IDomainEventHandler<UserRegistrationConfirmed>
    {
        private readonly UserRepository _repository;
        public UserRegistrationConfirmedListener(UserRepository repository) => _repository = repository;

        public async Task Handle(UserRegistrationConfirmed confirmation, CancellationToken cancellationToken)
        {
            var user = new User(confirmation);
            _repository.Add(user);
        }
    }
}
