namespace PanelControllerCLI.UserErrorExceptions
{
    public class WrongTypeException : UserErrorException
    {
        public WrongTypeException(string message, Exception? inner = null)
            : base(message, inner) { }

        public WrongTypeException(Type type, Type correctType, Exception? inner = null)
            : this($"Expected type {correctType.Name} but got {type.Name}.", inner) { }
    }
}
