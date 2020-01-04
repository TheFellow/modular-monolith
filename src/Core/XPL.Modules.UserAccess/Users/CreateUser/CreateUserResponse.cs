using System;

namespace XPL.Modules.UserAccess.Users.CreateUser
{
    public class CreateUserResponse
    {
        public Guid CorrelationId { get; private set; }
        public bool Success { get; private set; }
        public string Error { get; private set; } = "";
        public int Id { get; private set; }

        private CreateUserResponse() { }

        public static CreateUserResponse Ok(Guid correlationid, int id) => new CreateUserResponse { Success = true, Id = id, CorrelationId = correlationid };
        public static CreateUserResponse Fail(Guid correlationid, string error) => new CreateUserResponse { Success = false, Error = error, CorrelationId = correlationid };
    }
}