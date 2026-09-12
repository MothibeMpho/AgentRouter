using Microsoft.Extensions.Configuration;
using Serilog;

// Keep Program.cs minimal: build configuration, initialize logging and hand off to AppRunner
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsetting.json", optional: false, reloadOnChange: true)
    .Build();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

await new AgentRouter.AppRunner(config).RunAsync();

