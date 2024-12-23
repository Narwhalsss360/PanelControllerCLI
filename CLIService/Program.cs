using CLIService;
#if WINDOWS
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
#endif

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
#if WINDOWS
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PanelControllerCLIService";
});
#endif

builder.Services.AddHostedService<CLIWorker>();
IHost host = builder.Build();
host.Run();
