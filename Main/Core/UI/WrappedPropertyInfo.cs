using System;
using System.Globalization;
using System.Reflection;

namespace TAOM.Core.UI;

/// <summary>
/// PropertyInfo wrapper that redirects GetValue/SetValue to a mixin instance
/// instead of the ViewModel instance. This lets Gauntlet's property resolution
/// find mixin properties via the ViewModel's _propertiesAndMethods dictionary.
/// </summary>
internal sealed class WrappedPropertyInfo : PropertyInfo
{
    private readonly PropertyInfo _inner;
    private readonly object _mixinInstance;

    public WrappedPropertyInfo(PropertyInfo inner, object mixinInstance)
    {
        _inner = inner;
        _mixinInstance = mixinInstance;
    }

    public override string Name => _inner.Name;
    public override Type PropertyType => _inner.PropertyType;
    public override Type DeclaringType => _inner.DeclaringType;
    public override Type ReflectedType => _inner.ReflectedType;
    public override PropertyAttributes Attributes => _inner.Attributes;
    public override bool CanRead => _inner.CanRead;
    public override bool CanWrite => _inner.CanWrite;

    public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
    {
        return _inner.GetValue(_mixinInstance, invokeAttr, binder, index, culture);
    }

    public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
    {
        _inner.SetValue(_mixinInstance, value, invokeAttr, binder, index, culture);
    }

    public override MethodInfo GetGetMethod(bool nonPublic) => WrapMethod(_inner.GetGetMethod(nonPublic));
    public override MethodInfo GetSetMethod(bool nonPublic) => WrapMethod(_inner.GetSetMethod(nonPublic));

    public override ParameterInfo[] GetIndexParameters() => _inner.GetIndexParameters();
    public override MethodInfo[] GetAccessors(bool nonPublic) => _inner.GetAccessors(nonPublic);

    public override object[] GetCustomAttributes(bool inherit) => _inner.GetCustomAttributes(inherit);
    public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _inner.GetCustomAttributes(attributeType, inherit);
    public override bool IsDefined(Type attributeType, bool inherit) => _inner.IsDefined(attributeType, inherit);

    private MethodInfo WrapMethod(MethodInfo method)
    {
        return method != null ? new WrappedMethodInfo(method, _mixinInstance) : null;
    }
}
