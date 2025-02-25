﻿using System.Collections.Concurrent;

namespace ConsoleRouter
{
    public class ServerIn : TextReader
    {
        public static readonly bool DUAL_CAHARACTER_NEW_LINE = Environment.NewLine.Length == 2;

        public static readonly byte ESCAPE_CHARACTER = 0x1B;

        private ConcurrentQueue<byte> _buffer = new();

        private object _blockFront = new();

        public enum EscapeCommands
        {
            READ_REQUEST = 0x30
        }

        public Stream In { get; init; }

        public Stream Out {  get; init; }

        public ServerIn(Stream @in, Stream @out)
        {
            In = @in;
            Out = @out;
        }

        public async Task Consume(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte read = await In.ReadByteAsync(cancellationToken);
                lock (_buffer)
                {
                    _buffer.Enqueue(read);
                    if (DUAL_CAHARACTER_NEW_LINE && read == Environment.NewLine[0])
                        lock (_blockFront)
                            _buffer.Enqueue((byte)In.ReadByte());
                }
            }
        }

        public override int Read()
        {
            lock (_blockFront) { }
            if (_buffer.IsEmpty)
                SendCommand(EscapeCommands.READ_REQUEST);

            Task.Run(async () =>
            {
                while (_buffer.IsEmpty)
                    await Task.Delay(2);
            }).Wait();

            byte result;
            lock (_buffer)
                while (!_buffer.TryDequeue(out result)) ;
            return result;
        }

        public override int Peek()
        {
            lock (_blockFront) { }
            if (_buffer.IsEmpty)
                return -1;

            byte result;
            lock (_buffer)
                while (!_buffer.TryPeek(out result)) ;
            return result;
        }

        private void SendCommand(EscapeCommands command)
        {
            Out.Write([ESCAPE_CHARACTER, command.AsByte()]);
            Out.Flush();
        }
    }

    public static class EscapeCommandsExtensions
    {
        public static byte AsByte(this ServerIn.EscapeCommands command) => (byte)command;

        public static ServerIn.EscapeCommands AsEscapeCommand(this byte b) => (ServerIn.EscapeCommands)b;

        public static ServerIn.EscapeCommands AsEscapeCommand(this int b) => (ServerIn.EscapeCommands)b;
    }
}
