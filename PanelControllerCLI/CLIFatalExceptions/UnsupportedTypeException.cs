namespace PanelControllerCLI.CLIFatalExceptions
{
    public class UnsupportedTypeException : CLIFatalException
    {
        public UnsupportedTypeException(string message,  Exception? innerException = null)
            : base(message, innerException) { }
        
        public UnsupportedTypeException(Type type, string doingWhat, Exception? inner = null)
            : this($"The type {type.Name} is unsupported for {doingWhat}. ", inner) { }
    }
}
