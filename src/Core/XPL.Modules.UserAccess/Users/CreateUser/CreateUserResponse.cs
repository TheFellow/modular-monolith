using System;

namespace XPL.Modules.UserAccess.Users.CreateUser
{
    public class CreateUserResponse
    {
        public bool Success { get; private set; }
        public string Error { get; private set; } = "";
        public int Id { get; private set; }

        private CreateUserResponse() { }

        public static CreateUserResponse Ok(int id) => new CreateUserResponse { Success = true, Id = id };
        public static CreateUserResponse Fail(string error) => new CreateUserResponse { Success = false, Error = error };
    }
}