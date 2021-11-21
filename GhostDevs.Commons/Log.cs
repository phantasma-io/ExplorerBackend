using Serilog;
using Serilog.Core;
using System;
using System.IO;
using Serilog.Events;
using System.Text.Json;

namespace GhostDevs.Commons
{
    public static class LogEx
    {
        public static void Init(string fileName, Serilog.Events.LogEventLevel minimumLevel, bool overwriteOldContent = false)
        {
            string filePath = Path.Combine(Path.GetFullPath("."), fileName);

            var levelSwitch = new LoggingLevelSwitch
            {
                MinimumLevel = minimumLevel
            };

            if (overwriteOldContent)
            {
                File.Delete(filePath);
            }

            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.ControlledBy(levelSwitch)
                .Enrich.WithThreadId()
                .Destructure.ByTransforming<JsonDocument>(node => JsonSerializer.Serialize(node))
                .WriteTo.Console(outputTemplate: "{Timestamp:u} {Timestamp:ffff} [{Level:u3}] <{ThreadId}> {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(filePath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:u} {Timestamp:ffff} [{Level:u3}] <{ThreadId}> {Message:lj}{NewLine}{Exception}");

            Log.Logger = logConfig.CreateLogger();
        }
        public static string Exception(string module, Exception ex, string rpc = null, bool warningMode = false)
        {
            string logMessage;
            if ((ex.Message.Contains("Rpc timeout after") || // Nethereum exception
                ex.Message.Contains("Error occurred when trying to send rpc requests")) && // Nethereum exception
                !Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logMessage = $"{module}: RPC request timeout";
                warningMode = true;
            }
            else
            {
                if(warningMode)
                    logMessage = $"{module} warning: {ex.GetType().ToString().Replace("exception", "e_xception").Replace("Exception", "E_xception")}: " + ex.Message.Replace("exception", "e_xception").Replace("Exception", "E_xception");
                else
                    logMessage = $"{module} exception caught:";
            }

            if (!string.IsNullOrEmpty(rpc))
            {
                logMessage += "\n\nRPC node: " + rpc;
            }

            if(warningMode)
                Log.Warning(logMessage);
            else
                Log.Error(ex, logMessage);

            return logMessage;
        }
    }
}