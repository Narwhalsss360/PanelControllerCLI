using ConsoleRouter;
using System.IO.Pipes;

const string NEGOTIATOR_PIPE_NAME = "PanelControllerCLIService";
const string PIPE_NAME = "RemotePanelControllerCLI";

using NamedPipeClientStream negotiator = new(".", NEGOTIATOR_PIPE_NAME, PipeDirection.InOut, PipeOptions.Asynchronous);
negotiator.Connect();
PipeNegotiator.ClientNegotiateResult negotiatingResult = await PipeNegotiator.NegotiateWithServer(negotiator, PIPE_NAME);

if (!negotiatingResult.Success)
{
    Console.WriteLine($"Negotiation failure: {negotiatingResult.Message}");
    return;
}
Console.WriteLine(negotiatingResult.Message);


NamedPipeClientStream pipe = new(".", PIPE_NAME, PipeDirection.InOut, PipeOptions.Asynchronous);
pipe.Connect();
ClientIn client = new(pipe, pipe, () => Console.ReadLine() ?? "");

while (pipe.IsConnected)
{
    int next = client.ReceiveNext();
    if (next == -1)
        continue;
    Console.Out.Write((char)next);
}
