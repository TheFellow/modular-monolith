namespace XPL.Framework.Domain
{
    public interface IUserInfo
    {
        string UserDomainName { get; }
        string UserFullName { get; }
        string UserName { get; }
    }
}
