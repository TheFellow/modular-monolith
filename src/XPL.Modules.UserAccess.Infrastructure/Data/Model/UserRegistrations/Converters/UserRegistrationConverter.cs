using System;
using XPL.Framework.Infrastructure.Data;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.Kernel.DateTimes;
using XPL.Modules.UserAccess.Domain.UserRegistrations;
using static XPL.Modules.UserAccess.Domain.UserRegistrations.UserRegistration;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.UserRegistrations.Converters
{
    public class UserRegistrationConverter : IModelConverter<UserRegistration, SqlUserRegistration>
    {
        private readonly ISystemClock _systemClock;
        public UserRegistrationConverter(ISystemClock systemClock) => _systemClock = systemClock;

        public UserRegistration ToModel(SqlUserRegistration persisted) => new Memento(
                persisted.Email,
                persisted.Login,
                persisted.ConfirmationCode,
                persisted.PasswordHash,
                persisted.PasswordSalt,
                persisted.FirstName,
                persisted.LastName,
                persisted.Status,
                persisted.StatusDate,
                _systemClock,
                persisted.RegistrationId,
                persisted.ExpiryDate)
            .From();

        public SqlUserRegistration ToPersisted(UserRegistration model)
        {
            var m = Memento.Get(model);
            return new SqlUserRegistration
            {
                ConfirmationCode = m.ConfirmationCode,
                Email = m.Email,
                ExpiryDate = m.ExpiryDate,
                FirstName = m.FirstName,
                LastName = m.LastName,
                Login = m.Login,
                PasswordHash = m.PasswordHash,
                PasswordSalt = m.PasswordSalt,
                RegistrationId = m.RegistrationId,
                Status = m.Status,
                StatusDate = m.StatusDate,
                UpdatedBy = Environment.UserName,
                UpdatedOn = _systemClock.Now
            };
        }

        public void CopyChanges(UserRegistration from, SqlUserRegistration to)
        {
            var m = Memento.Get(from);

            new AuditFieldUpdater<Memento, SqlUserRegistration>(_systemClock, m, to, t => t.UpdatedBy, t => t.UpdatedOn)
                .Map(m => m.ConfirmationCode, t => t.ConfirmationCode)
                .Map(m => m.Email, t => t.Email)
                .Map(m => m.ExpiryDate, t => t.ExpiryDate)
                .Map(m => m.FirstName, t => t.FirstName)
                .Map(m => m.LastName, t => t.LastName)
                .Map(m => m.Login, t => t.Login)
                .Map(m => m.PasswordHash, t => t.PasswordHash)
                .Map(m => m.PasswordSalt, t => t.PasswordSalt)
                .Map(m => m.RegistrationId, t => t.RegistrationId)
                .Map(m => m.Status, t => t.Status)
                .Map(m => m.StatusDate, t => t.StatusDate)
                .Audit();
        }
    }
}
