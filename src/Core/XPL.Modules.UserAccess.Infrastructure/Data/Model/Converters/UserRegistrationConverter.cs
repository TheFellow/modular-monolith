using XPL.Framework.Infrastructure.Persistence;
using XPL.Framework.Kernel.DateTimes;
using XPL.Modules.UserAccess.Domain.UserRegistrations;
using static XPL.Modules.UserAccess.Domain.UserRegistrations.UserRegistration;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Converters
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
                FirstName = m.FirstName,
                ExpiryDate = m.ExpiryDate,
                LastName = m.LastName,
                Login = m.Login,
                PasswordHash = m.PasswordHash,
                PasswordSalt = m.PasswordSalt,
                RegistrationId = m.RegistrationId,
                Status = m.Status,
                UpdatedBy = "Current User",
                UpdatedOn = _systemClock.Now
            };
        }

        public void CopyChanges(UserRegistration from, SqlUserRegistration to)
        {
            var m = Memento.Get(from);
            to.ConfirmationCode = m.ConfirmationCode;
            to.Email = m.Email;
            to.FirstName = m.FirstName;
            to.ExpiryDate = m.ExpiryDate;
            to.LastName = m.LastName;
            to.Login = m.Login;
            to.PasswordHash = m.PasswordHash;
            to.PasswordSalt = m.PasswordSalt;
            to.RegistrationId = m.RegistrationId;
            to.Status = m.Status;
            to.StatusDate = m.StatusDate;
        }
    }
}
