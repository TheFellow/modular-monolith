using Functional.Either;
using XPL.Modules.UserAccess.Domain.UserRegistrations;

namespace XPL.Modules.UserAccess.Application.UserRegistrations.Builder
{
    public interface IUserRegistrationBuilder
    {
        Either<UserRegistrationError, UserRegistration> Build();
        IUserRegistrationBuilder WithEmail(string email);
        IUserRegistrationBuilder WithFirstName(string firstName);
        IUserRegistrationBuilder WithLastName(string lastName);
        IUserRegistrationBuilder WithLogin(string login);
        IUserRegistrationBuilder WithPassword(string password);
    }
}