using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;

namespace Backend.Commons;

public static class LogEx
{
    private static bool IsPostgresServerError(Exception ex)
    {
        while (ex != null)
        {
            if (string.Equals(ex.GetType().FullName, "Npgsql.PostgresException", StringComparison.Ordinal))
                return true;

            ex = ex.InnerException;
        }

        return false;
    }

    private static bool IsDatabaseConnectivityIssue(Exception ex)
    {
        if (IsPostgresServerError(ex))
            return false;

        while (ex != null)
        {
            var type = ex.GetType();
            var fullName = type.FullName ?? "";
            var ns = type.Namespace ?? "";
            if (fullName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                ns.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            ex = ex.InnerException;
        }

        return false;
    }

    private static bool IsRpcConnectivityIssue(Exception ex)
    {
        while (ex != null)
        {
            if (ex is TimeoutException or TaskCanceledException or OperationCanceledException
                or HttpRequestException or SocketException)
                return true;

            var message = ex.Message?.ToLowerInvariant() ?? "";
            if (message.Contains("rpc request timeout") ||
                message.Contains("api request failed after") ||
                message.Contains("connection refused") ||
                message.Contains("response ended prematurely") ||
                message.Contains("unable to connect") ||
                message.Contains("no such host") ||
                message.Contains("name or service not known"))
            {
                return true;
            }

            ex = ex.InnerException;
        }

        return false;
    }

    private static string ToSingleLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();
    }

    public static string Exception(string module, Exception ex, string rpc = null, bool warningMode = false)
    {
        string logMessage;
        if (IsDatabaseConnectivityIssue(ex) && !Log.IsEnabled(LogEventLevel.Debug))
        {
            // Keep DB transport/transient failures visible but avoid mislabeling them as RPC issues.
            logMessage = $"{module}: Database request issue ({ex.GetType().Name}: {ToSingleLine(ex.Message)})";
            warningMode = true;
        }
        else if ((ex.Message.Contains("Rpc timeout after") || // Nethereum exception
                  ex.Message.Contains("Error occurred when trying to send rpc requests") ||
                  IsRpcConnectivityIssue(ex)) && // Generic HTTP/RPC transport failures
                 !Log.IsEnabled(LogEventLevel.Debug))
        {
            logMessage = $"{module}: RPC request issue ({ex.GetType().Name}: {ToSingleLine(ex.Message)})";
            warningMode = true;
        }
        else
        {
            if (warningMode)
                logMessage =
                    $"{module} warning: {ex.GetType().ToString().Replace("exception", "e_xception").Replace("Exception", "E_xception")}: " +
                    ex.Message.Replace("exception", "e_xception").Replace("Exception", "E_xception");
            else
                logMessage = $"{module} exception caught:";
        }

        if (!string.IsNullOrEmpty(rpc)) logMessage += $" | RPC node: {ToSingleLine(rpc)}";

        if (warningMode)
            Log.Warning(logMessage);
        else
            Log.Error(ex, logMessage);

        return logMessage;
    }
}
