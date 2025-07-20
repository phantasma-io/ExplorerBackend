using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class Supply
{
    [ProducesResponseType(typeof(double), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(double), "Returns circulating supply of SOUL token", false, 10)]
    public static async Task<double> Execute()
    {
        string stringSupply;
        try
        {
            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            stringSupply = databaseContext.Tokens.AsQueryable().AsNoTracking()
                .Where(x => x.SYMBOL == "SOUL")
                .Select(x => x.CURRENT_SUPPLY)
                .FirstOrDefault();

            var responseTime = DateTime.Now - startTime;
            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch (ApiParameterException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logMessage = LogEx.Exception("Supply()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        if (!double.TryParse(stringSupply, out var parsed))
        {
            throw new ApiUnexpectedException($"Cannot parse {stringSupply} supply", null);
        }
        return parsed;
    }
}
