using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Binding gate for every UIExtenderEx <c>[ViewModelMixin("Method")]</c> in TAOM.
///
/// <para>
/// A mixin runs its <c>OnRefresh</c> from a Harmony transpiler that UIExtenderEx attaches to the named method of the target
/// view model, at the end of every constructor and every <c>ret</c> of that method. A name that no longer exists on the
/// target (a rename or a removal in an engine update) makes UIExtenderEx 2.13.2 and later throw a NullReferenceException at
/// <c>UIExtender.Register</c> (it removed the null guard; docs/migration/dependency-audit-2026-07-15.md:134), which takes
/// the module's UI extensions down at startup. This gate names the mixin and the method instead. <c>PrefabExtensionBindingTests</c>
/// is the sibling that does this for the XPaths; this one pins the other half of a UI extension.
/// </para>
///
/// <para>
/// The method is resolved the way UIExtenderEx resolves it: by name on the target type or the nearest base type that declares
/// it, any visibility. It must be unambiguous (one declaration, so no overload picks silently) and not abstract (a transpiler
/// has no body to extend). The no-overloads rule is stricter than UIExtenderEx, which accepts one.
/// </para>
/// </summary>
[TestClass]
public class ViewModelMixinBindingTests
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryMixinRefreshMethod_ResolvesOnTarget()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var failures = new StringBuilder();
        var checkedCount = 0;
        foreach (var mixin in typeof(TAOM.IoC).Assembly.GetTypes())
        {
            var attr = mixin.GetCustomAttribute<ViewModelMixinAttribute>(inherit: false);
            if (attr?.RefreshMethodName == null) continue;
            checkedCount++;

            var target = TargetOf(mixin);
            if (target == null)
            {
                failures.AppendLine($"{mixin.Name}: not a BaseViewModelMixin<T>, so the refresh method has no target.");
                continue;
            }

            var found = Declarations(target, attr.RefreshMethodName).ToList();
            if (found.Count == 0)
                failures.AppendLine($"{mixin.Name}: '{attr.RefreshMethodName}' is not declared on {target.FullName} or any base type.");
            else if (found.Count > 1)
                failures.AppendLine($"{mixin.Name}: '{attr.RefreshMethodName}' has {found.Count} overloads on {found[0].DeclaringType!.FullName}; the transpiler target is ambiguous.");
            else if (found[0].IsAbstract)
                failures.AppendLine($"{mixin.Name}: '{attr.RefreshMethodName}' is abstract on {found[0].DeclaringType!.FullName}; there is no body to extend.");
        }

        Assert.AreEqual(0, failures.Length,
            "[ViewModelMixin] refresh methods that no longer resolve (UIExtenderEx throws a NullReferenceException at registration):" + Environment.NewLine + failures);
        Assert.IsTrue(checkedCount >= 5, "Expected every named-refresh mixin in TAOM to be checked; only " + checkedCount + " found.");
    }

    [TestMethod]
    public void TheResolver_FindsAnInheritedPrivateMethod_AndRefusesAMissingOne()
    {
        // The gate's own logic, with no game needed: a base-declared private method resolves, a typo does not.
        Assert.AreEqual(1, Declarations(typeof(Derived), "Refill").Count());
        Assert.AreEqual(0, Declarations(typeof(Derived), "Refil").Count());
        Assert.AreEqual(2, Declarations(typeof(Derived), "Overloaded").Count());
        Assert.AreEqual(typeof(Target), TargetOf(typeof(Mixin)));
        Assert.IsNull(TargetOf(typeof(string)));
    }

    // The nearest type, from the target up, that declares a method of this name; every overload it declares.
    private static IEnumerable<MethodInfo> Declarations(Type target, string name)
    {
        for (var type = target; type != null; type = type.BaseType)
        {
            var here = type.GetMethods(Declared).Where(m => m.Name == name).ToList();
            if (here.Count > 0) return here;
        }

        return Enumerable.Empty<MethodInfo>();
    }

    private static Type? TargetOf(Type mixin)
    {
        for (var type = mixin; type != null; type = type.BaseType)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BaseViewModelMixin<>))
                return type.GetGenericArguments()[0];
        return null;
    }

    private class Base
    {
        private void Refill() { }

        protected void Overloaded() { }

        protected void Overloaded(int _) { }
    }

    private class Derived : Base
    {
    }

    private class Target : TaleWorlds.Library.ViewModel
    {
    }

    private class Mixin : BaseViewModelMixin<Target>
    {
        public Mixin(Target vm) : base(vm)
        {
        }
    }
}
