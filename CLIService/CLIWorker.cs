using CLIApplication;
using ConsoleRouter;
using PanelControllerCLI.CLIFatalExceptions;
using PanelControllerCLI.DataErrorExceptions;
using PanelControllerCLI.UserErrorExceptions;
using PanelControllerCLI;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using PanelController.Controller;

namespace CLIService
{
    public class CLIWorker : BackgroundService
    {
        public static readonly string NEGOTIATOR_PIPE_NAME = "PanelControllerCLIService";

        private readonly ILogger<CLIWorker> _logger;

        private CancellationTokenSource? _stoppingCts;

        private NamedPipeServerStream? _consolePipe;
        public CLIInterpreter Interpreter { get; set; } = new() { IgnoreCase = true };

        public Settings Settings;

        private bool Connected { get => _consolePipe is not null; }

        public CLIWorker(ILogger<CLIWorker> logger)
        {
            _logger = logger;
            PanelControllerCLI.PanelControllerCLI.Initialize(Interpreter);
            RouteInterpreterToVoid();
            Settings = Settings.Load();
            Interpreter.Commands.Add(new(Stop));
            Interpreter.Commands.Add(new(Exit));
            Interpreter.Commands.Add(new(Save));
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
            using NamedPipeServerStream pipe = CreateNamedPipeServer.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.FirstPipeInstance | PipeOptions.Asynchronous);
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

            rerun:
            try
            {

                Interpreter.StopRunExecution = false;
                Interpreter.Run(ctsCLIStop.Token);
            }
            catch (Exception ex)
            {
                if (!ProcessTargetException(ex))
                    goto rerun;
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
            Load();
            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            using NamedPipeServerStream negotiator = CreateNamedPipeServer.Create(NEGOTIATOR_PIPE_NAME, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.FirstPipeInstance | PipeOptions.Asynchronous);
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
            Save();
        }

        private void RouteInterpreterToVoid()
        {
            Interpreter.InterfaceName = "";
            Interpreter.Out = TextWriter.Null;
            Interpreter.Error = TextWriter.Null;
            Interpreter.In = TextReader.Null;
        }

        private void Load()
        {
            Settings.LoadExtensions();
            Settings.LoadPanels();
            Settings.LoadProfiles();
        }

        private void Save()
        {
            Settings.SavePanels();
            Settings.SaveProfiles();
            Settings.SaveSettings();
        }

        private bool ProcessTargetException(Exception exception)
        {
            if (exception is CLIFatalException fatal)
            {
                string message = $"Fatal error occurred({fatal.GetType().Name}): {fatal.Message}";
                Logger.Log(message, Logger.Levels.Error, "PanelControllerCLI-Host");
                Interpreter.Error.WriteLine(message);
                Interpreter.Error.WriteLine($"Trace:\n{fatal.StackTrace}");
                return true;
            }
            else if (exception is UserErrorException userError)
            {
                Interpreter.Error.WriteLine($"{userError.GetType().Name}: {userError.Message}");
            }
            else if (exception is DataErrorException dataError)
            {
                Interpreter.Error.WriteLine($"{dataError.GetType().Name}: {dataError.Message}");
            }
            else if (exception is UserCancelException)
            {
                Interpreter.Out.WriteLine("Canceled.");
            }
            else if (exception is NotImplementedException exc)
            {
                Interpreter.Error.WriteLine($"The requested operation is not implemented ({exc.TargetSite?.DeclaringType}.{exc.TargetSite?.Name}). {exc.Message}");
            }
            else if (exception is TargetInvocationException target)
            {
                if (target.InnerException is null)
                    throw new InvalidProgramException("Unkown exception occurred.", target);
                return ProcessTargetException(target.InnerException);
            }
            else
            {
                Interpreter.Error.WriteLine($"An exception occurred of type {exception.GetType().Name}:{exception.Message}\nTrace:\n{exception.StackTrace}");
                return true;
            }

            return false;
        }

        private void Stop() => Interpreter.Stop();

        private void Exit() => Stop();
    }
}
