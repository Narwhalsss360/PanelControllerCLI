namespace PanelControllerCLI.DataErrorExceptions
{
    public class MoreThanOneMatchException : DataErrorException
    {
        public MoreThanOneMatchException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
