using System.IO;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using Shouldly;
using Xunit;

namespace Backend.Service.Api.Tests.Logging;

public sealed class LogExTests
{
    [Fact]
    public void ExceptionDoesNotMaskPostgresServerErrorsAsConnectivityWarnings()
    {
        var previousLogger = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Information().CreateLogger();

        try
        {
            var postgresException = new PostgresException(
                "duplicate key value violates unique constraint",
                "ERROR",
                "ERROR",
                PostgresErrorCodes.UniqueViolation);
            var exception = new DbUpdateException("Save failed", postgresException);

            var message = LogEx.Exception("Block process", exception);

            message.ShouldBe("Block process exception caught:");
            message.ShouldNotContain("Database request issue");
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public void ExceptionKeepsCompactWarningsForNpgsqlTransportFailures()
    {
        var previousLogger = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Information().CreateLogger();

        try
        {
            var exception = new NpgsqlException(
                "Exception while reading from stream",
                new IOException("Connection reset by peer"));

            var message = LogEx.Exception("Block process", exception);

            message.ShouldContain("Block process: Database request issue");
            message.ShouldContain("NpgsqlException");
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }
}
