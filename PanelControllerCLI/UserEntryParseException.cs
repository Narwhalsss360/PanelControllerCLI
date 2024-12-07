namespace PanelControllerCLI
{
    public class UserEntryParseException : Exception
    {
        public readonly bool AllowRetry;

        public UserEntryParseException(bool allowRetry)
        {
            AllowRetry = allowRetry;
        }
    }
}
