using TAOM.Core.Logging;
using System;
using System.Diagnostics;
using System.Reflection;

namespace TAOM.Core.Infrastructure;

public class ReflectionService : IReflectionService
{
    private readonly IModLogger _logger;

    public ReflectionService(IModLogger logger)
    {
        _logger = logger;
    }

    public TReturn GetFieldValue<TOwner, TReturn>(TOwner owner, string name)
        where TOwner : class
    {
        return (TReturn)typeof(TOwner).GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).GetValue(owner);
    }

    public void SetFieldValue<TOwner>(TOwner owner, string name, object value)
        where TOwner : class
    {
        var field = typeof(TOwner).GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        if (field == null)
        {
            _logger?.LogError($"ReflectionService: Field '{name}' not found on type '{typeof(TOwner).Name}'");
            throw new InvalidOperationException($"Field '{name}' not found on type '{typeof(TOwner).Name}'");
        }
        field.SetValue(owner, value);
    }

    public TReturn GetPropertyValue<TOwner, TReturn>(TOwner owner, string name)
        where TOwner : class
    {
        return (TReturn)GetPropertyValue(owner, name);
    }

    public object GetPropertyValue<TOwner>(TOwner owner, string name)
        where TOwner : class
    {
        return typeof(TOwner).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(owner);
    }

    public void SetPropertyValue<TOwner>(TOwner owner, string name, object value)
        where TOwner : class
    {
        var property = typeof(TOwner).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
        {
            _logger?.LogError($"ReflectionService: Property '{name}' not found on type '{typeof(TOwner).Name}'");
            throw new InvalidOperationException($"Property '{name}' not found on type '{typeof(TOwner).Name}'");
        }
        if (!property.CanWrite)
        {
            _logger?.LogError($"ReflectionService: Property '{name}' on type '{typeof(TOwner).Name}' is read-only");
            throw new InvalidOperationException($"Property '{name}' on type '{typeof(TOwner).Name}' is read-only");
        }
        property.SetValue(owner, value);
    }

    public object CallPrivateMethod<TType>(TType owner, string name, object[] values)
        where TType : class
    {
        var typeToSearch = typeof(TType) == typeof(object) ? owner.GetType() : typeof(TType);

        var method = typeToSearch.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        if (method == null)
        {
            _logger?.LogError($"ReflectionService: Method '{name}' not found on type '{typeToSearch.Name}'");
            throw new InvalidOperationException($"Method '{name}' not found on type '{typeToSearch.Name}'");
        }

        try
        {
            return method.Invoke(owner, values);
        }
        catch (Exception ex)
        {
            if (_logger != null)
            {
                _logger.LogError($"Error in CallPrivateMethod: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
            }
            else
            {
                Debug.WriteLine($"Error in CallPrivateMethod: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return null;
        }
    }
}
