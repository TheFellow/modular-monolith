using System;
using System.Collections.Generic;
using System.Linq;
using XPL.Framework.Infrastructure.Data;
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
            var currentLogin = p.Passwords.Single(l => l.EndOnUtc == null);

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

            var login = new SqlUserPassword
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
                Passwords = new List<SqlUserPassword>(new[] { login }),
                Emails = new List<SqlUserEmail>(new[] { email }),
            };
        }

        public void CopyChanges(User from, SqlUser sql)
        {
            var cutoff = _systemClock.UtcNow;

            var user = Memento.Get(from);

            var audit = new AuditFieldUpdater<Memento, SqlUser>(_systemClock, user, sql, u => u.UpdatedBy, u => u.UpdatedOn);
            audit.Map(m => m.FirstName, u => u.FirstName)
                .Map(m => m.LastName, u => u.LastName)
                .Map(m => m.CurrentLogin, u => u.Login)
                .Map(m => m.RegistrationId, u => u.RegistrationId)
                .Map(m => m.UserId, u => u.UserId)
                .Audit();
            
            if (user.PasswordHash != sql.Passwords.Single(p => p.EndOnUtc == null).PasswordHash)
            {
                audit.Force();

                var oldPassword = sql.Passwords.Single(l => l.EndOnUtc == null);
                oldPassword.EndOnUtc = cutoff.AddTicks(-1);

                var newLogin = new SqlUserPassword
                {
                    BeginOnUtc = cutoff,
                    EndOnUtc = null,
                    PasswordHash = user.PasswordHash,
                    PasswordSalt = user.PasswordSalt,
                };

                sql.Passwords.Add(newLogin);
            }

            if(user.CurrentEmail != sql.Emails.Single(e => e.Status == SqlUserEmail.ActiveStatus).Email)
            {
                audit.Force();

                var oldEmail = sql.Emails.Single(e => e.Status == SqlUserEmail.ActiveStatus);

                oldEmail.Status = SqlUserEmail.InactiveStatus;
                oldEmail.StatusDate = _systemClock.Now.Date;

                var newEmail = new SqlUserEmail
                {
                    Email = user.CurrentEmail,
                    Status = SqlUserEmail.ActiveStatus,
                    StatusDate = _systemClock.Now.Date,
                    UpdatedBy = UserInfo.UserFullName,
                    UpdatedOn = cutoff
                };

                sql.Emails.Add(newEmail);
            }
        }
    }
}
