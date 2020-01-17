using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XPL.Modules.UserAccess.Domain.UserRegistrations;
using XPL.Modules.UserAccess.Infrastructure.Persitence;

namespace XPL.Modules.UserAccess.Infrastructure.UserRegistrations
{
    public class UserRegistrationRepository : IUserRegistrationRepository
    {
        private readonly UserAccessDbContext _dbContext;
        

        public UserRegistrationRepository(UserAccessDbContext dbContext) => _dbContext = dbContext;

        public Task<UserRegistration> FindAsync(int id) => throw new NotImplementedException();
        public Task AddAsync(UserRegistration userRegistration) => throw new NotImplementedException();
    }
}
