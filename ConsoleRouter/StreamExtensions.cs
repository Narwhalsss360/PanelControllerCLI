namespace ConsoleRouter
{
    public static class StreamExtensions
    {
        public static async Task<byte> ReadByteAsync(this Stream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = [0];
            await stream.ReadAsync(buffer, cancellationToken);
            return buffer[0];
        }

        public static byte? TimedReadByte(this Stream stream, int millisecondsTimeout)
        {
            CancellationTokenSource cts = new();
            Task<byte> readTask = ReadByteAsync(stream, cts.Token);
            if (readTask.Wait(millisecondsTimeout))
                return readTask.Result;
            cts.Cancel();
            return null;
        }
    }
}
