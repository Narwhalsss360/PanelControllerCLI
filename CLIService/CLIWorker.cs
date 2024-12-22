using CLIApplication;
using ConsoleRouter;
using System.IO.Pipes;
using System.Text;

namespace CLIService
{
    public class CLIWorker : BackgroundService
    {
        public static readonly string NEGOTIATOR_PIPE_NAME = "PanelControllerCLIService";

        private readonly ILogger<CLIWorker> _logger;

        private CancellationTokenSource? _stoppingCts;

        private NamedPipeServerStream? _consolePipe;

        private bool Connected { get => _consolePipe is not null; }

        public CLIInterpreter Interpreter { get; set; } = new() { IgnoreCase = true };

        public CLIWorker(ILogger<CLIWorker> logger)
        {
            _logger = logger;
            PanelControllerCLI.PanelControllerCLI.Initialize(Interpreter);
            RouteInterpreterToVoid();
            Interpreter.Commands.Add(new(Stop));
            Interpreter.Commands.Add(new(Exit));
        }

        private void NegotiateWithClient(object? sender, PipeNegotiator.ClientNegotiateResult e)
        {
            e.Success = false;
            string pipeName = e.Message;
            if (Connected)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.LogError("{PipeName} requested connection, but a connection already exists.", pipeName);
                e.Message = "Connection already exists.";
                return;
            }

            Task.Delay(100).Wait();
            ServeClient(pipeName, _stoppingCts!.Token).ConfigureAwait(ConfigureAwaitOptions.None);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Pipe {PipeName} spooling up.", pipeName);
            e.Success = true;
            e.Message = $"Successfully connected as {pipeName}.";
        }

        private async Task ServeClient(string pipeName, CancellationToken cancellationToken)
        {
            using NamedPipeServerStream pipe = new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.FirstPipeInstance | PipeOptions.Asynchronous);
            _consolePipe = pipe;
            await pipe.WaitForConnectionAsync(cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation($"Pipe {pipeName} connected.");

            CancellationTokenSource ctsCLIStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Task checkTask = Task.Run(async () =>
            {
                while (pipe.IsConnected)
                    await Task.Delay(250);
            }, cancellationToken).ContinueWith(task =>
            {
                Interpreter.Stop();
                ctsCLIStop.Cancel();
            }, cancellationToken);

            try
            {
                using ServerIn @in = new(pipe, pipe);
                using StreamWriter @out = new(pipe) { AutoFlush = true };
                Interpreter.InterfaceName = pipeName;
                Interpreter.In = @in;
                Interpreter.Out = @out;
                Interpreter.Error = @out;

                Task consumer = @in.Consume(ctsCLIStop.Token).ContinueWith(task =>
                {
                    if (task.Exception?.InnerException is ObjectDisposedException)
                        return;

                    if (_logger.IsEnabled(LogLevel.Error))
                        _logger.LogError("Consumer error: {}", task.Exception);
                });

                Interpreter.StopRunExecution = false;
                Interpreter.Run(ctsCLIStop.Token);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.LogError("Error occurred: {}", ex);
            }

            _consolePipe = null;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Pipe {PipeName} finished.", pipeName);

            pipe.Close();
            await checkTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            using NamedPipeServerStream negotiator = new(NEGOTIATOR_PIPE_NAME, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.FirstPipeInstance | PipeOptions.Asynchronous);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Exception? exception = await new PipeNegotiator(negotiator, NegotiateWithClient).Listen(stoppingToken);
                    if (exception is not null && _logger.IsEnabled(LogLevel.Critical))
                        _logger.LogCritical("Critical negotiator error: {Exception}", exception);
                    _consolePipe?.Write(Encoding.UTF8.GetBytes("Negotiator listener stopped, restarting..."));
                    Interpreter.Stop();;
                    await Task.Delay(1000);
                }
                catch (OperationCanceledException)
                {
                    Interpreter.Stop();
                }
            }
        }

        private void RouteInterpreterToVoid()
        {
            Interpreter.InterfaceName = "";
            Interpreter.Out = TextWriter.Null;
            Interpreter.Error = TextWriter.Null;
            Interpreter.In = TextReader.Null;
        }

        private void Stop() => Interpreter.Stop();

        private void Exit() => Stop();
    }
}
