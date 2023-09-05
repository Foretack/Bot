﻿global using Serilog;
global using static Serilog.Log;
using Bot.Enums;
using Bot.Interfaces;
using Bot.Utils.Logging;
using Serilog.Core;
using Serilog.Enrichers.ClassName;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

namespace Bot.Workflows;

public class LoggerSetup: IWorkflow
{
    public static LoggingLevelSwitch LogSwitch { get; private set; } = new((LogEventLevel)Config.DefaultLogLevel);

    public ValueTask<WorkflowState> Run()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Filter.With<ClassNameFilter>()
            .Enrich.WithClassName()
            .Enrich.WithHeapSize()
            .Enrich.WithUptime()
            .WriteTo.Console(
                outputTemplate:
                "[{Timestamp:yyyy.mm.dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}",
                levelSwitch: LogSwitch)
            .WriteTo.File(new CompactJsonFormatter(), "data.log", flushToDiskInterval: TimeSpan.FromMinutes(2.5), rollingInterval: RollingInterval.Month)
            .WriteTo.File("readable_data.log", flushToDiskInterval: TimeSpan.FromMinutes(2.5), rollingInterval: RollingInterval.Month)
            .WriteTo.Discord(Config.Links["Webhook"])
            .WriteTo.GrafanaLoki("http://localhost:3100", new[]
            {
                new LokiLabel { Key = "Application", Value = "Bot" }
            }, new[]
            {
                "SourceContext", "ClassName"
            })
            .CreateLogger();

        return ValueTask.FromResult(WorkflowState.Completed);
    }
}