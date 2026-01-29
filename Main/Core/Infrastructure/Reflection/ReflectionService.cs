using TAOM.Core.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace TAOM.Core.Infrastructure;

public class ReflectionService : IReflectionService
{
    private const BindingFlags AllInstanceAndStatic =
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    private readonly IModLogger _logger;
    private readonly ConcurrentDictionary<(Type, string), FieldInfo> _fieldCache = new();
    private readonly ConcurrentDictionary<(Type, string), PropertyInfo> _propertyCache = new();
    private readonly ConcurrentDictionary<(Type, string), MethodInfo> _methodCache = new();

    public ReflectionService(IModLogger logger)
    {
        _logger = logger;
    }

    public TReturn GetFieldValue<TOwner, TReturn>(TOwner owner, string name)
        where TOwner : class
    {
        var type = typeof(TOwner);
        var field = _fieldCache.GetOrAdd((type, name), key =>
            key.Item1.GetField(key.Item2, AllInstanceAndStatic));

        if (field == null)
        {
            _logger?.LogError($"ReflectionService: Field '{name}' not found on type '{type.Name}'");
            throw new InvalidOperationException($"Field '{name}' not found on type '{type.Name}'");
        }

        return (TReturn)field.GetValue(owner);
    }

    public void SetFieldValue<TOwner>(TOwner owner, string name, object value)
        where TOwner : class
    {
        var type = typeof(TOwner);
        var field = _fieldCache.GetOrAdd((type, name), key =>
            key.Item1.GetField(key.Item2, AllInstanceAndStatic));

        if (field == null)
        {
            _logger?.LogError($"ReflectionService: Field '{name}' not found on type '{type.Name}'");
            throw new InvalidOperationException($"Field '{name}' not found on type '{type.Name}'");
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
        var type = typeof(TOwner);
        var property = _propertyCache.GetOrAdd((type, name), key =>
            key.Item1.GetProperty(key.Item2, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));

        if (property == null)
        {
            _logger?.LogError($"ReflectionService: Property '{name}' not found on type '{type.Name}'");
            throw new InvalidOperationException($"Property '{name}' not found on type '{type.Name}'");
        }

        return property.GetValue(owner);
    }

    public void SetPropertyValue<TOwner>(TOwner owner, string name, object value)
        where TOwner : class
    {
        var type = typeof(TOwner);
        var property = _propertyCache.GetOrAdd((type, name), key =>
            key.Item1.GetProperty(key.Item2, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));

        if (property == null)
        {
            _logger?.LogError($"ReflectionService: Property '{name}' not found on type '{type.Name}'");
            throw new InvalidOperationException($"Property '{name}' not found on type '{type.Name}'");
        }

        if (!property.CanWrite)
        {
            _logger?.LogError($"ReflectionService: Property '{name}' on type '{type.Name}' is read-only");
            throw new InvalidOperationException($"Property '{name}' on type '{type.Name}' is read-only");
        }

        property.SetValue(owner, value);
    }

    public object CallPrivateMethod<TType>(TType owner, string name, object[] values)
        where TType : class
    {
        var typeToSearch = typeof(TType) == typeof(object) ? owner.GetType() : typeof(TType);

        var method = _methodCache.GetOrAdd((typeToSearch, name), key =>
            key.Item1.GetMethod(key.Item2, AllInstanceAndStatic));

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
