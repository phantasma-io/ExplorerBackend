using System;
using Serilog;
using Serilog.Events;

namespace Backend.Commons;

public static class LogEx
{
    public static string Exception(string module, Exception ex, string rpc = null, bool warningMode = false)
    {
        string logMessage;
        if ( ( ex.Message.Contains("Rpc timeout after") || // Nethereum exception
               ex.Message.Contains("Error occurred when trying to send rpc requests") ) && // Nethereum exception
             !Log.IsEnabled(LogEventLevel.Debug) )
        {
            logMessage = $"{module}: RPC request timeout";
            warningMode = true;
        }
        else
        {
            if ( warningMode )
                logMessage =
                    $"{module} warning: {ex.GetType().ToString().Replace("exception", "e_xception").Replace("Exception", "E_xception")}: " +
                    ex.Message.Replace("exception", "e_xception").Replace("Exception", "E_xception");
            else
                logMessage = $"{module} exception caught:";
        }

        if ( !string.IsNullOrEmpty(rpc) ) logMessage += "\n\nRPC node: " + rpc;

        if ( warningMode )
            Log.Warning(logMessage);
        else
            Log.Error(ex, logMessage);

        return logMessage;
    }
}
