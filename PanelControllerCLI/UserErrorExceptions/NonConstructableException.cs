namespace PanelControllerCLI.UserErrorExceptions
{
    public class NonConstructableException : UserErrorException
    {
        public NonConstructableException(string message, Exception? inner = null)
            : base(message, inner) { }

        public NonConstructableException(Type type, Exception? inner = null)
            : this($"Type {type.Name} is not constructable.", inner) { }


        public NonConstructableException(Type type, Type asType, Exception? inner = null)
            : this($"Type {type.Name} is not constructable as {asType.Name}.", inner) { }

        public NonConstructableException(Type type, Type asType, string reason, Exception? inner = null)
            : this($"Type {type.Name} is not constructable as {asType.Name}. Reason: {reason}", inner) { }
    }
}
