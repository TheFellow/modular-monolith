using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Domain.Model;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Events;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;

namespace XPL.Modules.UserAccess.Application.Listeners
{
    public class UserRegistrationConfirmedListener : IDomainEventHandler<UserRegistrationConfirmed>
    {
        private readonly UserRepository _repository;
        private readonly IEmailUsage _emailUsage;

        public UserRegistrationConfirmedListener(UserRepository repository, IEmailUsage emailUsage)
        {
            _repository = repository;
            _emailUsage = emailUsage;
        }

        public async Task Handle(UserRegistrationConfirmed confirmation, CancellationToken cancellationToken)
        {
            var user = new User(confirmation, _emailUsage);
            _repository.Add(user);
        }
    }
}
