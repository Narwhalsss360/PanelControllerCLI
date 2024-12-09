namespace PanelControllerCLI.CLIFatalExceptions
{
    public class UninitializedContextException : CLIFatalException
    {
        public UninitializedContextException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
