using System;

namespace XPL.Modules.UserAccess.Application.Users.CreateUser
{
    public class CreateUserResponse
    {
        public Guid CorrelationId { get; set; }
        public int Id { get; set; }
        public string UserName { get; }

        public CreateUserResponse(Guid correlationId, int id, string userName)
        {
            CorrelationId = correlationId;
            Id = id;
            UserName = userName;
        }
    }
}