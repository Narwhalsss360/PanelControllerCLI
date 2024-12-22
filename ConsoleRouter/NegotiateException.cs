namespace ConsoleRouter
{
    public class NegotiateException : Exception
    {
        public NegotiateException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
