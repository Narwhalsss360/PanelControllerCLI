using System.Text;

namespace ConsoleRouter
{
    public class ClientIn
    {
        public Stream In { get; init; }

        public Stream Out { get; init; }

        public bool Escaped { get; private set; } = false;

        public Task CurrentReadTask { get; private set; } = Task.CompletedTask;

        private Func<string> _readRequestHandler;

        public ClientIn(Stream @in, Stream @out, Func<string> readRequestHandler)
        {
            In = @in;
            Out = @out;
            _readRequestHandler = readRequestHandler;
        }

        public int ReceiveNext()
        {
            int read = In.ReadByte();

            if (read == -1)
                return read;

            if (Escaped)
                ProcessEscape(read);
            else if (read == ServerIn.ESCAPE_CHARACTER)
                Escaped = true;

            return read;
        }

        private void ProcessEscape(int read)
        {
            switch (read.AsEscapeCommand())
            {
                case ServerIn.EscapeCommands.READ_REQUEST:
                    CurrentReadTask.ContinueWith(task => CurrentReadTask = Task.Run(ProcessReadRequest));
                    break;
                default:
                    break;
            }

            Escaped = false;
        }

        private void ProcessReadRequest()
        {
            byte[] bytes =
            [
                ..Encoding.UTF8.GetBytes(_readRequestHandler()),
                ..Encoding.UTF8.GetBytes(Environment.NewLine)
            ];
            Out.Write(bytes);
            Out.Flush();
        }
    }
}
