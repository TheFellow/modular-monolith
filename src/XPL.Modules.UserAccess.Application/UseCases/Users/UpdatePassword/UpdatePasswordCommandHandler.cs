using Functional.Either;
using Functional.Option;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Domain.Contracts;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;

namespace XPL.Modules.UserAccess.Application.UseCases.Users.UpdatePassword
{
    public class UpdatePasswordCommandHandler : ICommandHandler<UpdatePasswordCommand, CommandResult>
    {
        private readonly UserRepository _repository;
        public UpdatePasswordCommandHandler(UserRepository repository) => _repository = repository;

        public async Task<CommandResult> Handle(UpdatePasswordCommand request, CancellationToken cancellationToken)
        {
            var option = _repository.TryFindByLogin(request.Login);

            if (!(option is Some<User> some))
                return CommandResult.Error("Login not found");

            var user = some.Content;
            Either<UserError, PasswordUpdated> result = user.UpdatePassword(request.OldPassword, request.NewPassword);

            return result.Map(_ => CommandResult.Ok("Password updated successfully."))
                .Reduce(err => CommandResult.Error(err.Message));
        }
    }
}
