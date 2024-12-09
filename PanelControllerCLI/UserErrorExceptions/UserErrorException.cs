namespace PanelControllerCLI.UserErrorExceptions
{
    public class UserErrorException : Exception
    {
        public UserErrorException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
