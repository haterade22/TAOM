using System;
using System.Globalization;
using System.Reflection;

namespace TAOM.Core.UI;

/// <summary>
/// MethodInfo wrapper that redirects Invoke to a mixin instance.
/// Used by WrappedPropertyInfo for getter/setter methods,
/// and directly for ExecuteCommand resolution.
/// </summary>
internal sealed class WrappedMethodInfo : MethodInfo
{
    private readonly MethodInfo _inner;
    private readonly object _mixinInstance;

    public WrappedMethodInfo(MethodInfo inner, object mixinInstance)
    {
        _inner = inner;
        _mixinInstance = mixinInstance;
    }

    public override string Name => _inner.Name;
    public override Type DeclaringType => _inner.DeclaringType;
    public override Type ReflectedType => _inner.ReflectedType;
    public override Type ReturnType => _inner.ReturnType;
    public override RuntimeMethodHandle MethodHandle => _inner.MethodHandle;
    public override MethodAttributes Attributes => _inner.Attributes;
    public override ICustomAttributeProvider ReturnTypeCustomAttributes => _inner.ReturnTypeCustomAttributes;

    public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
    {
        return _inner.Invoke(_mixinInstance, invokeAttr, binder, parameters, culture);
    }

    public override ParameterInfo[] GetParameters() => _inner.GetParameters();
    public override MethodImplAttributes GetMethodImplementationFlags() => _inner.GetMethodImplementationFlags();
    public override MethodInfo GetBaseDefinition() => _inner.GetBaseDefinition();

    public override object[] GetCustomAttributes(bool inherit) => _inner.GetCustomAttributes(inherit);
    public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _inner.GetCustomAttributes(attributeType, inherit);
    public override bool IsDefined(Type attributeType, bool inherit) => _inner.IsDefined(attributeType, inherit);
}
