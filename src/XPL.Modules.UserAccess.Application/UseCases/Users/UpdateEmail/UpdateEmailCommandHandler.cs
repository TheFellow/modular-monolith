using Functional.Option;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Contracts;
using XPL.Modules.Kernel.Email;
using XPL.Modules.UserAccess.Domain;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;

namespace XPL.Modules.UserAccess.Application.UseCases.Users.UpdateEmail
{
    public class UpdateEmailCommandHandler : ICommandHandler<UpdateEmailCommand, string>
    {
        private readonly UserRepository _repository;
        public UpdateEmailCommandHandler(UserRepository repository) => _repository = repository;

        public Task<Result<string>> Handle(UpdateEmailCommand request, CancellationToken cancellationToken)
        {
            var option = _repository.TryFindByLogin(request.Login);

            if (!(option is Some<User> some))
                return request.Fail("Unknown login.");

            var user = some.Content;
            var someError = user.UpdateEmail(new EmailAddress(request.NewEmail));

            if (someError is Some<UserAccessError> error)
                return request.Fail(error.Content.Message);

            return request.Ok("Email updated");
        }
        
    }
}
