namespace PanelControllerCLI.CLIFatalExceptions
{
    public class AlreadyInitializedException : CLIFatalException
    {
        public AlreadyInitializedException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
