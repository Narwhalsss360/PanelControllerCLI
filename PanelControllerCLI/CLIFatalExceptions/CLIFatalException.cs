namespace PanelControllerCLI.CLIFatalExceptions
{
    public class CLIFatalException : Exception
    {
        public CLIFatalException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
