using CLIService;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<CLIWorker>();
builder.Build().Run();
