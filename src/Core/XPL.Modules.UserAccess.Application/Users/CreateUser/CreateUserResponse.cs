using System;

namespace XPL.Modules.UserAccess.Application.Users.CreateUser
{
    public class CreateUserResponse
    {
        public Guid CorrelationId { get; private set; }
        public int Id { get; private set; }

        public CreateUserResponse(Guid correlationId, int id)
        {
            CorrelationId = correlationId;
            Id = id;
        }
    }
}