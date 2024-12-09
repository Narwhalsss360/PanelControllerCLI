namespace PanelControllerCLI.DataErrorExceptions
{
    public class NameCollisionException : MoreThanOneMatchException
    {
        public NameCollisionException(string message, Exception? inner = null)
            : base(message, inner) { }

        public NameCollisionException(Type type, string name, string collection, Exception? inner = null)
            : base($"More than one {type.Name} in {collection} is named {name}.", inner) { }
    }
}
