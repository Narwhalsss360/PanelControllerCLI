using NStreamCom;
using System.IO.Pipes;
using System.Text;

namespace ConsoleRouter
{
    public class PipeNegotiator
    {
        public class ClientNegotiateResult : EventArgs
        {
            public bool Success { get; set; } = false;

            public string Message { get; set; } = "";
        }

        public delegate void ClientNegotiationResolver(object? sender, ClientNegotiateResult result);

        public static readonly byte SUCCESS_RESULT_BYTE = 1;

        public static readonly byte FAILURE_RESULT_BYTE = 0;

        private readonly NamedPipeServerStream _server;

        private readonly ClientNegotiationResolver _resolver;

        public PipeNegotiator(NamedPipeServerStream server, ClientNegotiationResolver resolver)
        {
            RequireReadWrite(server, nameof(server));
            _server = server;
            _resolver = resolver;
        }

        public async Task<Exception?> Listen(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await WaitForNextClient(cancellationToken);
                }
                catch (Exception exception)
                {
                    return exception;
                }
            }
            return null;
        }

        public async Task WaitForNextClient(CancellationToken cancellationToken)
        {
            await _server.WaitForConnectionAsync(cancellationToken);
            Collector collector = new();

            while (!collector.State.ErrorState() && !collector.DataReady)
            {
                if (_server.TimedReadByte(500) is not byte b)
                    break;
                collector.Collect(b);
            }

            if (!collector.DataReady)
                throw new NegotiateException($"Did not collect full negotiation message, state: {collector.State}");

            ClientNegotiateResult result = new() { Message = Encoding.UTF8.GetString(collector.Data) };
            _resolver.Invoke(this, result);

            await _server.WriteAsync(NEncode.EncodeWithSize
            ([
                result.Success ? SUCCESS_RESULT_BYTE : FAILURE_RESULT_BYTE,
                ..Encoding.UTF8.GetBytes(result.Message)
            ]), cancellationToken);
            await _server.FlushAsync(cancellationToken);

            _server.Disconnect();
        }

        public static async Task<ClientNegotiateResult> NegotiateWithServer(NamedPipeClientStream client, string message, CancellationToken cancellationToken = default)
        {
            if (!client.IsConnected)
                throw new ArgumentException("Client must be connected to the server", nameof(client));
            RequireReadWrite(client, nameof(client));

            await client.WriteAsync(NEncode.EncodeWithSize(Encoding.UTF8.GetBytes(message)), cancellationToken);
            await client.FlushAsync(cancellationToken);

            Collector collector = new();
            while (!collector.State.ErrorState() && !collector.DataReady)
            {
                if (client.TimedReadByte(500) is not byte b)
                    break;
                collector.Collect(b);
            }

            if (!collector.DataReady || collector.Size == 0)
                throw new NegotiateException($"Did not collect full negotiation message, state: {collector.State}");

            return new()
            {
                Success = collector.Data[0] > 0,
                Message = Encoding.UTF8.GetString(collector.Data[1..])
            };
        }

        private static void RequireReadWrite(PipeStream pipe, string name)
        {
            if (!pipe.CanRead || !pipe.CanWrite)
                throw new ArgumentException("The pipe must have read/write permissions.", name);
        }
    }
}
