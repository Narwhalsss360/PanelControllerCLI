namespace PanelControllerCLI.UserErrorExceptions
{
    public class OutOfBoundsException : UserErrorException
    {
        public OutOfBoundsException(string message, Exception? inner = null)
            : base(message, inner) { }

        public OutOfBoundsException(int index, int low, int high, Exception? inner = null)
            : this($"Expected {low} to {high} but got {index}.", inner) { }


        public OutOfBoundsException(int index, int high, Exception? inner = null)
            : this(index, 0, high, inner) { }
    }
}
