using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.SystemConsole.Themes;
using Shared.Constants;

namespace Shared.Configurations
{
    /// <summary>
    /// Represents the log configuration.
    /// </summary>
    public static class LogConfiguration
    {
        /// <summary>
        /// Initializes the log configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="applicationName">The application name to use for log files.</param>
        public static void Initialize(IConfiguration configuration, string applicationName)
        {
            const string template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

            bool useJsonFormat = configuration.GetValue<bool>(ConfigurationConstant.UseJsonFormat);
            var loggerConfiguration = new LoggerConfiguration()
                            .MinimumLevel.Debug()
                            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                            .Enrich.FromLogContext()
                            .WriteTo.File(
                                $"logs/{applicationName}.txt",
                                rollingInterval: RollingInterval.Day,
                                outputTemplate: template);

            if (useJsonFormat)
            {
                loggerConfiguration.WriteTo.Console(new JsonFormatter())
                .WriteTo.File(new JsonFormatter(), $"logs/{applicationName}.txt", rollingInterval: RollingInterval.Day);
            }
            else
            {
                loggerConfiguration.WriteTo.Console(outputTemplate: template, theme: AnsiConsoleTheme.Sixteen)
                .WriteTo.File($"logs/{applicationName}.txt", rollingInterval: RollingInterval.Day, outputTemplate: template);
            }

            Log.Logger = loggerConfiguration.CreateLogger();
        }
    }
}
