using System;
using System.Collections.Generic;
using System.Linq;

namespace GhostDevs
{
    public static class QueryResultExtensions
    {
        public static bool GetBool(this Dictionary<string, object> dictionary, string key, bool defaultValue = false)
        {
            if (!dictionary.ContainsKey(key))
            {
                return defaultValue;
            }

            return (bool)dictionary[key];
        }
        public static Int32 GetInt32(this Dictionary<string, object> dictionary, string key, Int32 defaultValue = 0)
        {
            if (!dictionary.ContainsKey(key))
            {
                return defaultValue;
            }

            if(dictionary[key].GetType() == typeof(Int32))
            {
                return (Int32)dictionary[key];
            }
            else if (dictionary[key].GetType() == typeof(Int64))
            {
                throw new Exception("Cannot convert Int64 to Int32");
            }
            else if (dictionary[key].GetType() == typeof(string))
            {
                return Int32.Parse((string)dictionary[key]);
            }

            // Default behaviour, will throw exception describing type incompatibility
            return (Int32)dictionary[key];
        }
        public static Int64 GetInt64(this Dictionary<string, object> dictionary, string key, Int64 defaultValue = 0)
        {
            if (!dictionary.ContainsKey(key))
            {
                return defaultValue;
            }

            if (dictionary[key].GetType() == typeof(Int32) ||
                dictionary[key].GetType() == typeof(Int64))
            {
                return (Int64)dictionary[key];
            }
            else if (dictionary[key].GetType() == typeof(string))
            {
                return Int64.Parse((string)dictionary[key]);
            }

            // Default behaviour, will throw exception describing type incompatibility
            return (Int64)dictionary[key];
        }
        public static string GetString(this Dictionary<string, object> dictionary, string key, string defaultValue = null)
        {
            if(!dictionary.ContainsKey(key))
            {
                return defaultValue;
            }

            if (dictionary[key].GetType() == typeof(string))
            {
                return (string)dictionary[key];
            }
            else
            {
                return dictionary[key].ToString();
            }
        }
        public static string GetString(this Dictionary<string, object> dictionary, Int32 index, string defaultValue = null)
        {
            if (dictionary.Count < index + 1)
            {
                return defaultValue;
            }

            var element = dictionary.ElementAt(index);
            if (element.GetType() == typeof(string))
            {
                return (string)element.Value;
            }
            else
            {
                return element.Value.ToString();
            }
        }
    }
}
