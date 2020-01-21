using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Domain;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Events;

namespace XPL.Modules.UserAccess.Application.Listeners
{
    public class UserRegistrationConfirmedListener : IDomainEventHandler<UserRegistrationConfirmed>
    {
        public Task Handle(UserRegistrationConfirmed notification, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
