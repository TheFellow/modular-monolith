namespace XPL.Framework.Domain
{
    public interface IExecutionContext
    {
        IUserInfo UserInfo { get; }
        ISystemClock SystemClock { get; }
    }
}
