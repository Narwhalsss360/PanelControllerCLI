namespace PanelControllerCLI.DataErrorExceptions
{
    public class DataErrorException : Exception
    {
        public DataErrorException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
