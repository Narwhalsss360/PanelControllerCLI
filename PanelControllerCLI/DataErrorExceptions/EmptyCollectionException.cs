namespace PanelControllerCLI.DataErrorExceptions
{
    public class EmptyCollectionException : DataErrorException
    {
        public EmptyCollectionException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
