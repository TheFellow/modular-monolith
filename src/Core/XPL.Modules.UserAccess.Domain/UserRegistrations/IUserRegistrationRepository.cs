using System.Threading.Tasks;

namespace XPL.Modules.UserAccess.Domain.UserRegistrations
{
    public interface IUserRegistrationRepository
    {
        Task<UserRegistration> FindAsync(int id);

        Task AddAsync(UserRegistration userRegistration);
    }
}
