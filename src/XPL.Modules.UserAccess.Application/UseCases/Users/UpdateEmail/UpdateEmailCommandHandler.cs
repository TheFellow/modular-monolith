using Functional.Option;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Domain.Contracts;
using XPL.Modules.Kernel.Email;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;

namespace XPL.Modules.UserAccess.Application.UseCases.Users.UpdateEmail
{
    public class UpdateEmailCommandHandler : ICommandHandler<UpdateEmailCommand, CommandResult>
    {
        private readonly UserRepository _repository;
        public UpdateEmailCommandHandler(UserRepository repository) => _repository = repository;

        public async Task<CommandResult> Handle(UpdateEmailCommand request, CancellationToken cancellationToken)
        {
            var option = _repository.TryFindByLogin(request.Login);

            if (!(option is Some<User> some))
                return CommandResult.Fail("Unknown login.");

            var user = some.Content;
            var someError = user.UpdateEmail(new EmailAddress(request.NewEmail));

            if (someError is Some<UserError> error)
                return CommandResult.Fail(error.Content.Message);

            return CommandResult.Ok("Email updated");
        }
        
    }
}
