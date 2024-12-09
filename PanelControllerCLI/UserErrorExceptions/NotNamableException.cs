namespace PanelControllerCLI.UserErrorExceptions
{
    public class NotNamableException : UserErrorException
    {
        public NotNamableException(string message, Exception? inner = null)
            : base(message, inner) { }

        public NotNamableException(Type type, Exception? inner = null)
            : this($"Type of {type.Name} is not namable.", inner) { }
    }
}
