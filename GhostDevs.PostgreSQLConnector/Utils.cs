using System;
using System.Collections.Generic;
using System.Linq;

namespace GhostDevs.PostgreSQLConnector;

public static class QueryResultExtensions
{
    public static bool GetBool(this Dictionary<string, object> dictionary, string key, bool defaultValue = false)
    {
        if ( !dictionary.ContainsKey(key) ) return defaultValue;

        return ( bool ) dictionary[key];
    }


    public static int GetInt32(this Dictionary<string, object> dictionary, string key, int defaultValue = 0)
    {
        if ( !dictionary.ContainsKey(key) ) return defaultValue;

        if ( dictionary[key] is int ) return ( int ) dictionary[key];

        if ( dictionary[key] is long ) throw new Exception("Cannot convert Int64 to Int32");

        if ( dictionary[key] is string ) return int.Parse(( string ) dictionary[key]);

        // Default behaviour, will throw exception describing type incompatibility
        return ( int ) dictionary[key];
    }


    public static long GetInt64(this Dictionary<string, object> dictionary, string key, long defaultValue = 0)
    {
        if ( !dictionary.ContainsKey(key) ) return defaultValue;

        if ( dictionary[key] is int ||
             dictionary[key] is long )
            return ( long ) dictionary[key];

        if ( dictionary[key] is string ) return long.Parse(( string ) dictionary[key]);

        // Default behaviour, will throw exception describing type incompatibility
        return ( long ) dictionary[key];
    }


    public static string GetString(this Dictionary<string, object> dictionary, string key, string defaultValue = null)
    {
        if ( !dictionary.ContainsKey(key) ) return defaultValue;

        if ( dictionary[key] is string ) return ( string ) dictionary[key];

        return dictionary[key].ToString();
    }


    public static string GetString(this Dictionary<string, object> dictionary, int index, string defaultValue = null)
    {
        if ( dictionary.Count < index + 1 ) return defaultValue;

        var element = dictionary.ElementAt(index);
        if ( element.GetType() == typeof(string) ) return ( string ) element.Value;

        return element.Value.ToString();
    }
}
