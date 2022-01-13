using System;
using Serilog;

namespace GhostDevs.Commons;

public static class UnixSeconds
{
    public static long Now()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }


    public static long MaxValue()
    {
        return FromDateTime(DateTime.MaxValue);
    }


    public static DateTime ToDateTime(long unixSeconds)
    {
        try
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixSeconds);
        }
        catch ( Exception )
        {
            Serilog.Log.Error("UnixSeconds.ToDateTime() crashed on this date: '{UnixSeconds}'", unixSeconds);
            throw;
        }
    }


    public static long FromString(string unixSeconds)
    {
        return long.Parse(unixSeconds);
    }


    public static long FromDateTime(DateTime dateTime)
    {
        return dateTime == DateTime.MinValue ? 0 : new DateTimeOffset(dateTime).ToUnixTimeSeconds();
    }


    public static long FromDateTimeString(string dateTime)
    {
        return FromDateTime(DateTime.Parse(dateTime));
    }


    public static long AddMinutes(long unixSeconds, long minutes)
    {
        return unixSeconds + minutes * 60;
    }


    public static long AddHours(long unixSeconds, long hours)
    {
        return unixSeconds + hours * 3600;
    }


    public static long AddDays(long unixSeconds, long days)
    {
        return unixSeconds + days * 86400;
    }


    // Removes time
    public static long GetDate(long unixSeconds)
    {
        return unixSeconds - unixSeconds % 86400;
    }


    public static long CountDays(long unixSeconds)
    {
        return unixSeconds / 86400;
    }


    public static string Log(long unixSeconds)
    {
        return $"{ToDateTime(unixSeconds):yyyy'-'MM'-'dd' 'HH':'mm':'ss}";
    }


    public static string LogDate(long unixSeconds)
    {
        return $"{ToDateTime(unixSeconds):yyyy'-'MM'-'dd}";
    }
}

public static class UnixMilliseconds
{
    public static DateTime ToDateTime(long unixMilliseconds)
    {
        try
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(unixMilliseconds);
        }
        catch ( Exception )
        {
            Log.Error("UnixMilliseconds.ToDateTime() crashed on this date: '{UnixMilliseconds}'", unixMilliseconds);
            throw;
        }
    }
}
