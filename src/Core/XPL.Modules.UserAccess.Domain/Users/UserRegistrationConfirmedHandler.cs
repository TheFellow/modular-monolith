using System;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Modules.Domain;
using XPL.Modules.UserAccess.Domain.UserRegistrations.Events;

namespace XPL.Modules.UserAccess.Domain.Users
{
    public class UserRegistrationConfirmedHandler : IDomainEventHandler<UserRegistrationConfirmed>
    {
        public async Task Handle(UserRegistrationConfirmed notification, CancellationToken cancellationToken)
        {
            // TODO: Set up the mapping repository so that we have a
            // list of entities to search for domain events
            Console.WriteLine("User registration confirmed");
        }
    }
}
