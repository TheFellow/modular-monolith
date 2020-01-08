using System;

namespace XPL.Modules.UserAccess.Application.Users.CreateUser
{
    public class CreateUserResponse
    {
        public Guid CorrelationId { get; private set; }
        public int Id { get; private set; }

        private CreateUserResponse() { }

        public static CreateUserResponse Ok(Guid correlationid, int id) => new CreateUserResponse { Id = id, CorrelationId = correlationid };
    }
}