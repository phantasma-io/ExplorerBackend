using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Foundatio.Messaging;
using GhostDevs.Service.Infrastructure;

namespace GhostDevs.Service;

#region SERVICE CLASSES

public class APIException : Exception
{
    public APIException(string msg) : base(msg)
    {
    }


    public APIException(string msg, Exception innerException) : base(msg, innerException)
    {
    }
}

public class APIDescriptionAttribute : Attribute
{
    public readonly string Description;


    public APIDescriptionAttribute(string description)
    {
        Description = description;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class APIFailCaseAttribute : APIDescriptionAttribute
{
    public readonly string Value;


    public APIFailCaseAttribute(string description, string value) : base(description)
    {
        Value = value;
    }
}

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = true)]
public class APIParameterAttribute : APIDescriptionAttribute
{
    public readonly string Value;


    public APIParameterAttribute(string description, string value) : base(description)
    {
        Value = value;
    }
}

public class APIInfoAttribute : APIDescriptionAttribute
{
    public readonly int CacheDuration;
    public readonly string CacheTag;
    public readonly bool InternalEndpoint;
    public readonly bool Paginated;
    public readonly Type ReturnType;


    public APIInfoAttribute(Type returnType, string description, bool paginated = false, int cacheDuration = 0,
        bool internalEndpoint = false, string cacheTag = null) : base(description)
    {
        ReturnType = returnType;
        Paginated = paginated;
        CacheDuration = cacheDuration;
        InternalEndpoint = internalEndpoint;
        CacheTag = cacheTag;
    }
}

public struct APIValue
{
    public readonly Type Type;
    public readonly string Name;
    public readonly string Description;
    public readonly string ExampleValue; // example value
    public readonly object DefaultValue;
    public readonly bool HasDefaultValue;


    public APIValue(Type type, string name, string description, string exampleValue, object defaultValue,
        bool hasDefaultValue)
    {
        Type = type;
        Name = name;
        Description = description;
        ExampleValue = exampleValue;
        DefaultValue = defaultValue;
        HasDefaultValue = hasDefaultValue;
    }
}

public struct APIModelValue
{
    public readonly Type Type;
    public readonly string Name;
    public readonly string Description;
    public readonly bool IsArray;


    public APIModelValue(Type type, string name, string description, bool isArray)
    {
        Type = type;
        Name = name;
        Description = description;
        IsArray = isArray;
    }
}

public struct APIEntry
{
    public readonly string Name;
    public readonly List<APIValue> Parameters;

    public readonly Type ReturnType;
    public readonly string ReturnTypeDescription;
    public readonly string Description;

    public readonly bool IsPaginated;
    public readonly bool IsInternal;

    public readonly bool IsPost;

    public readonly APIFailCaseAttribute[] FailCases;

    private readonly Endpoints _api;
    private readonly MethodInfo _info;


    private int GetParameterIndex(string name)
    {
        for ( var i = 0; i < Parameters.Count; i++ )
            if ( string.Equals(Parameters[i].Name.ToUpper(), name.ToUpper()) )
                return i;

        return -1;
    }


    private object GetParameter(string name, params object[] input)
    {
        var parameterIndex = GetParameterIndex(name);
        if ( parameterIndex == -1 ) return null;

        return input.Length <= parameterIndex ? null : input[parameterIndex];
    }


    public APIEntry(Endpoints api, MethodInfo info)
    {
        _api = api;
        _info = info;
        Name = info.Name;

        IsPost = false;

        var parameters = info.GetParameters();
        Parameters = new List<APIValue>();
        foreach ( var entry in parameters )
        {
            string description;
            string exampleValue;

            var descAttr = entry.GetCustomAttribute<APIParameterAttribute>();
            if ( descAttr != null )
            {
                description = descAttr.Description;
                exampleValue = descAttr.Value;
            }
            else
            {
                description = "TODO document me";
                exampleValue = "TODO document me";
            }

            var defaultValue = entry.HasDefaultValue ? entry.DefaultValue : null;

            Parameters.Add(new APIValue(entry.ParameterType, entry.Name, description, exampleValue, defaultValue,
                entry.HasDefaultValue));
        }

        try
        {
            FailCases = info.GetCustomAttributes<APIFailCaseAttribute>().ToArray();
        }
        catch
        {
            FailCases = Array.Empty<APIFailCaseAttribute>();
        }

        try
        {
            var attr = info.GetCustomAttribute<APIInfoAttribute>();
            if ( attr == null )
            {
                ReturnType = null;
                ReturnTypeDescription = null;
                Description = null;
                IsPaginated = false;
                IsInternal = false;
                return;
            }

            ReturnType = attr.ReturnType;

            var returnTypeDescAttr = attr.ReturnType.GetCustomAttribute<APIDescriptionAttribute>();
            ReturnTypeDescription = returnTypeDescAttr != null ? returnTypeDescAttr.Description : "TODO document me";

            Description = attr.Description;
            IsPaginated = attr.Paginated;
            IsInternal = attr.InternalEndpoint;
        }
        catch
        {
            ReturnType = null;
            ReturnTypeDescription = "TODO document me";
            Description = "TODO document me";
            IsPaginated = false;
            IsInternal = false;
        }
    }


    public override string ToString()
    {
        return Name;
    }
}

public struct APIModel
{
    public readonly string Name;
    public readonly List<APIModelValue> Fields;
    public bool InternalOnly { get; set; }


    public APIModel(string name, IEnumerable<FieldInfo> fields, bool internalOnly) : this()
    {
        Name = name;
        Fields = new List<APIModelValue>();
        InternalOnly = internalOnly;
        foreach ( var entry in fields )
        {
            string description = null;

            var descAttr = entry.GetCustomAttribute<APIDescriptionAttribute>();
            if ( descAttr != null ) description = descAttr.Description;

            var isArray = false;
            Type type;
            if ( entry.FieldType.IsArray )
            {
                isArray = true;
                type = entry.FieldType.GetElementType();
            }
            else if ( entry.FieldType.IsGenericType &&
                      entry.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>) )
                type = Nullable.GetUnderlyingType(entry.FieldType);
            else
                type = entry.FieldType;

            Fields.Add(new APIModelValue(type, entry.Name, description, isArray));
        }
    }
}

#endregion

public partial class Endpoints : ApiEndpointBase
{
    private readonly IMessagePublisher _publisher;


    public Endpoints(IMessagePublisher publisher)
    {
        _publisher = publisher;
    }
}
