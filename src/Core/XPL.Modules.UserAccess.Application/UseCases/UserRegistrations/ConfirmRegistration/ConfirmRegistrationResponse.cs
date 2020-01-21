namespace XPL.Modules.UserAccess.Application.UseCases.UserRegistrations.ConfirmRegistration
{
    public class ConfirmRegistrationResponse
    {
        public bool Success { get; }
        public string Message { get; }
        private ConfirmRegistrationResponse(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static ConfirmRegistrationResponse Confirmed =>
            new ConfirmRegistrationResponse(true, "Registration Confirmed.");

        public static ConfirmRegistrationResponse Error(string msg) =>
            new ConfirmRegistrationResponse(false, msg);
    }
}