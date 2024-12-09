namespace PanelControllerCLI.UserErrorExceptions
{
    public class NotFoundException : UserErrorException
    {
        public NotFoundException(string message, Exception? inner = null)
            : base(message, inner) { }

        public NotFoundException(string query, string collection, Exception? inner = null)
            : this($"Could not find {query} in {collection}", inner) { }
    }
}
