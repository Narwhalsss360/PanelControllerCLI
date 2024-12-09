namespace PanelControllerCLI.UserErrorExceptions
{
    public class UserEntryParseException : UserErrorException
    {
        public readonly bool AllowRetry;

        public UserEntryParseException(string message, bool allowRetry = false, Exception? inner = null)
            : base(message, inner)
        {
            AllowRetry = allowRetry;
        }

        public UserEntryParseException(Type type, string entered, bool allowRetry = false, Exception? inner = null)
            : this($"Could not parse '{entered}' as type {type.Name}", allowRetry, inner) { }
    }
}
