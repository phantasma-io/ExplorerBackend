using System;

namespace GhostDevs.Commons
{
    public static class UnixSeconds
    {
        public static Int64 Now()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        public static Int64 MaxValue()
        {
            return FromDateTime(DateTime.MaxValue);
        }
        public static DateTime ToDateTime(Int64 unixSeconds)
        {
            try
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(unixSeconds);
            }
            catch(Exception)
            {
                Serilog.Log.Error($"UnixSeconds.ToDateTime() crashed on this date: '{unixSeconds}'");
                throw;
            }
        }
        public static Int64 FromString(string unixSeconds)
        {
            return Int64.Parse(unixSeconds);
        }
        public static Int64 FromDateTime(DateTime dateTime)
        {
            if (dateTime == DateTime.MinValue)
                return 0;
            return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
        }
        public static Int64 FromDateTimeString(string dateTime)
        {
            return FromDateTime(DateTime.Parse(dateTime));
        }
        public static Int64 AddMinutes(Int64 unixSeconds, Int64 minutes)
        {
            return unixSeconds + minutes * 60;
        }
        public static Int64 AddHours(Int64 unixSeconds, Int64 hours)
        {
            return unixSeconds + hours * 3600;
        }
        public static Int64 AddDays(Int64 unixSeconds, Int64 days)
        {
            return unixSeconds + days * 86400;
        }
        // Removes time
        public static Int64 GetDate(Int64 unixSeconds)
        {
            return unixSeconds - unixSeconds % 86400;
        }
        public static Int64 CountDays(Int64 unixSeconds)
        {
            return unixSeconds / 86400;
        }
        public static string Log(Int64 unixSeconds)
        {
            return $"{ToDateTime(unixSeconds):yyyy'-'MM'-'dd' 'HH':'mm':'ss}";
        }
        public static string LogDate(Int64 unixSeconds)
        {
            return $"{ToDateTime(unixSeconds):yyyy'-'MM'-'dd}";
        }
    }
    public static class UnixMilliseconds
    {
        public static DateTime ToDateTime(Int64 unixMilliseconds)
        {
            try
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddMilliseconds(unixMilliseconds);
            }
            catch(Exception)
            {
                Serilog.Log.Error($"UnixMilliseconds.ToDateTime() crashed on this date: '{unixMilliseconds}'");
                throw;
            }
        }
    }
}
