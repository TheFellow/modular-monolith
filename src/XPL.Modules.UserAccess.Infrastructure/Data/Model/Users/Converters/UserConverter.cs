using System;
using System.Collections.Generic;
using System.Linq;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.Kernel;
using XPL.Modules.Kernel.DateTimes;
using XPL.Modules.UserAccess.Domain.Users;
using static XPL.Modules.UserAccess.Domain.Users.User;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users.Converters
{
    public class UserConverter : IModelConverter<User, SqlUser>
    {
        private readonly ISystemClock _systemClock;
        public UserConverter(ISystemClock systemClock) => _systemClock = systemClock;

        public User ToModel(SqlUser p)
        {
            var currentLogin = p.Logins.Single(l => l.EndOnUtc == null);

            return new Memento(
                p.UserId,
                p.RegistrationId,
                p.Login,
                p.Emails.Single(e => e.Status == SqlUserEmail.ActiveStatus).Email,
                p.FirstName,
                p.LastName,
                currentLogin.PasswordHash,
                currentLogin.PasswordSalt)
                .From();
        }

        public SqlUser ToPersisted(User model)
        {
            var m = Memento.Get(model);

            var email = new SqlUserEmail
            {
                Email = m.CurrentEmail,
                Status = SqlUserEmail.ActiveStatus,
                StatusDate = _systemClock.Now.Date,
                UpdatedBy = UserInfo.UserFullName,
                UpdatedOn = _systemClock.Now,
            };

            var login = new SqlUserLogin
            {
                BeginOnUtc = _systemClock.UtcNow,
                EndOnUtc = null,
                PasswordHash = m.PasswordHash,
                PasswordSalt = m.PasswordSalt,
                UpdatedBy = UserInfo.UserFullName,
                UpdatedOn = _systemClock.Now,
            };

            return new SqlUser
            {
                UserId = m.UserId,
                RegistrationId = (m.RegistrationId ?? Guid.Empty),
                FirstName = m.FirstName,
                LastName = m.LastName,
                Login = m.CurrentLogin,
                UpdatedBy = UserInfo.UserFullName,
                UpdatedOn = _systemClock.Now,
                Logins = new List<SqlUserLogin>(new[] { login }),
                Emails = new List<SqlUserEmail>(new[] { email }),
            };
        }

        public void CopyChanges(User from, SqlUser to)
        {
            var m = Memento.Get(from);
            to.UserId = m.UserId;
            to.RegistrationId = m.RegistrationId;
            to.FirstName = m.FirstName;
            to.LastName = m.LastName;

            if (m.CurrentLogin != to.Login)
            {
                var cutoff = _systemClock.UtcNow;

                var oldLogin = to.Logins.Single(l => l.EndOnUtc == null);
                oldLogin.EndOnUtc = cutoff.AddTicks(-1);

                var newLogin = new SqlUserLogin
                {
                    BeginOnUtc = cutoff,
                    EndOnUtc = null,
                    PasswordHash = m.PasswordHash,
                    PasswordSalt = m.PasswordSalt,
                };

                to.Logins.Add(newLogin);

                to.Login = m.CurrentLogin;
            }

            // TODO: Deal with the schema
        }
    }
}
