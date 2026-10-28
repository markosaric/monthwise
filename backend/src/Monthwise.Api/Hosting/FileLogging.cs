using Serilog;
using Serilog.Events;

namespace Monthwise.Api.Hosting;

/// <summary>
/// Optional log file for the native background service: one file per day in <c>Logging:File:Path</c>,
/// the newest <c>Logging:File:RetainedDays</c> (default 14) kept, with the levels of <c>Logging:LogLevel</c>.
/// </summary>
internal static class FileLogging
{
    public static ILoggingBuilder AddFileLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        var folder = configuration["Logging:File:Path"];
        if (string.IsNullOrWhiteSpace(folder))
        {
            return logging;
        }

        var retainedDays = configuration.GetValue("Logging:File:RetainedDays", 14);
        var loggerConfiguration = new LoggerConfiguration();
        // Serilog ignores the Microsoft logging filters, so apply Logging:LogLevel here.
        foreach (var level in configuration.GetSection("Logging:LogLevel").GetChildren())
        {
            if (Enum.TryParse<LogLevel>(level.Value, ignoreCase: true, out var parsed))
            {
                var serilogLevel = ToSerilog(parsed);
                if (level.Key == "Default")
                {
                    loggerConfiguration.MinimumLevel.Is(serilogLevel);
                }
                else
                {
                    loggerConfiguration.MinimumLevel.Override(level.Key, serilogLevel);
                }
            }
        }

        var logger = loggerConfiguration
            .WriteTo.File(
                Path.Combine(folder, "monthwise-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retainedDays,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return logging.AddSerilog(logger, dispose: true);
    }

    private static LogEventLevel ToSerilog(LogLevel level) => level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        // Serilog has no "None": Fatal comes closest.
        _ => LogEventLevel.Fatal,
    };
}
