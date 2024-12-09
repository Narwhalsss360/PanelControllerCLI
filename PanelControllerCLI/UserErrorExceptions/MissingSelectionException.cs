namespace PanelControllerCLI.UserErrorExceptions
{
    public class MissingSelectionException : UserErrorException
    {
        public MissingSelectionException(string message, Exception? inner = null)
            : base(message, inner) { }

        public MissingSelectionException(Type expected, Exception? inner = null)
            : this($"Expected {expected.Name} to be selected", inner) { }
    }
}
