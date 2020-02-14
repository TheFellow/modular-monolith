using XPL.Framework.Application.Contracts;

namespace XPL.Framework.Application.Ports
{
    public interface IAuthorization
    {
        void Authorize<TResult>(ICommand<TResult> command);

        public const string AuthorizationIssuer = "XPL Authorization Authority";
    }
}
