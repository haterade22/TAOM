namespace TAOM.Core.Infrastructure;

public static class ReflectionHelper
{
    private static IReflectionService _service;

    static ReflectionHelper()
    {
        _service = IoC.Resolve<IReflectionService>();
    }

    public static TReturn GetFieldValue<TOwner, TReturn>(TOwner owner, string name)
        where TOwner : class
    {
        return _service.GetFieldValue<TOwner, TReturn>(owner, name);
    }

    public static void SetFieldValue<TOwner>(TOwner owner, string name, object value)
        where TOwner : class
    {
        _service.SetFieldValue(owner, name, value);
    }

    public static TReturn GetPropertyValue<TOwner, TReturn>(TOwner owner, string name)
        where TOwner : class
    {
        return _service.GetPropertyValue<TOwner, TReturn>(owner, name);
    }

    public static object GetPropertyValue<TOwner>(TOwner owner, string name)
        where TOwner : class
    {
        return _service.GetPropertyValue(owner, name);
    }

    public static void SetPropertyValue<TOwner>(TOwner owner, string name, object value)
        where TOwner : class
    {
        _service.SetPropertyValue(owner, name, value);
    }

    public static object CallPrivateMethod<TType>(TType owner, string name, object[] values)
        where TType : class
    {
        return _service.CallPrivateMethod(owner, name, values);
    }
}
