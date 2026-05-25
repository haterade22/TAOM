using System;
using System.Linq;
using System.Reflection;

namespace TAOM.Features.CrashReport.Adapters;

public sealed class ButterLibExceptionHandlerAdapter : IButterLibExceptionHandlerAdapter
{
    private const string TypeFullName = "Bannerlord.ButterLib.ExceptionHandler.ExceptionHandlerSubSystem";

    private readonly Lazy<Type?> _type = new(ResolveType);

    public bool IsPresent => _type.Value != null;

    public bool TrySuspend()
    {
        var t = _type.Value;
        if (t == null) return false;
        try
        {
            var instProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var instance = instProp?.GetValue(null);
            if (instance == null) return false;
            var disable = t.GetMethod("Disable", BindingFlags.Public | BindingFlags.Instance);
            if (disable == null) return false;
            disable.Invoke(instance, parameters: null);
            return true;
        }
        catch { return false; }
    }

    private static Type? ResolveType()
    {
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(TypeFullName, throwOnError: false);
                if (t != null) return t;
            }
        }
        catch { }
        return null;
    }
}
