using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Pins <c>PatchCategoryIndex</c>: one patch class whose attributes cannot be read (a
/// <c>[HarmonyPatch]</c> naming a type the engine no longer has) must cost only that class. Harmony
/// 2.4.2 indexes categories once per assembly with no catch and never caches a failed build, so
/// through <c>Harmony.PatchCategory</c> that one class failed every category of the assembly.
/// The probe assembly is emitted at run time because a compiled attribute cannot name a missing
/// type, and a broken class in this test assembly would break every other category test here.
/// </summary>
[TestClass]
public class PatchCategoryIndexTests
{
    internal const string BrokenCategory = "Test_IndexProbe_Broken";
    internal const string HealthyCategory = "Test_IndexProbe_Healthy";
    private const string BrokenClassName = "TaomIndexProbe.BrokenAttributeProbe";
    private const string HealthyClassName = "TaomIndexProbe.HealthyProbe";

    private static readonly Lazy<Assembly> ProbeAssembly = new(EmitProbeAssembly);

    private static readonly MethodInfo Target =
        typeof(PatchCategoryIndexProbeTarget).GetMethod(nameof(PatchCategoryIndexProbeTarget.Probe))!;

    private IModLogger _logger = null!;
    private Harmony _harmony = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _harmony = new Harmony("taom.tests.patchcategoryindex");
    }

    [TestCleanup]
    public void Cleanup() => _harmony.UnpatchAll(_harmony.Id);

    // The premise: Harmony's own category apply fails a healthy category because a class in
    // another category has an unreadable attribute. If Harmony ever isolates this itself, this
    // fails and PatchCategoryIndex can go.
    [TestMethod]
    public void RealHarmony_AnUnreadableAttribute_FailsEveryCategoryOfItsAssembly()
    {
        Assert.ThrowsException<TypeLoadException>(
            () => _harmony.PatchCategory(ProbeAssembly.Value, HealthyCategory),
            "Harmony now indexes categories without throwing; PatchCategoryIndex may be redundant");
    }

    [TestMethod]
    public void Build_SkipsOnlyTheClassWhoseAttributesCannotBeRead()
    {
        var index = PatchCategoryIndex.Build(ProbeAssembly.Value);

        CollectionAssert.AreEqual(new[] { BrokenClassName },
            index.SkippedClasses.Select(s => s.Key.FullName).ToArray());
        Assert.IsInstanceOfType(index.SkippedClasses[0].Value, typeof(TypeLoadException));
    }

    // The maintainer's decision (#653): one unreadable class is skipped and reported, and every
    // other category still applies.
    [TestMethod]
    public void TryApply_ThroughTheIndex_AppliesTheHealthyCategory_AndReportsOnlyTheBrokenClass()
    {
        var index = PatchCategoryIndex.Build(ProbeAssembly.Value);
        var sut = new PatchCategoryApplier(category => index.Apply(_harmony, category), _logger);
        sut.RecordSkippedClasses(index.SkippedClasses);

        Assert.IsTrue(sut.TryApply(HealthyCategory), "the healthy category must apply");
        Assert.IsTrue(sut.TryApply(BrokenCategory), "the broken class is skipped, not retried");

        CollectionAssert.Contains(PostfixOwners(), _harmony.Id);
        _logger.Received(1).LogError(Arg.Is<string>(s =>
            s.Contains("[PatchApply]") && s.Contains(BrokenClassName) && s.Contains("TypeLoadException")));
        var groups = PatchCategoryApplierTests.Variable(sut.TakeFailureSummary(new TextObject("startup"))!, "GROUPS");
        Assert.AreEqual(BrokenClassName, groups);
    }

    // Parity with Harmony.PatchCategory: an unknown category name applies nothing, silently.
    [TestMethod]
    public void Apply_AnUnknownCategory_AppliesNothingAndDoesNotThrow()
    {
        PatchCategoryIndex.Build(ProbeAssembly.Value).Apply(_harmony, "Test_IndexProbe_NoSuchCategory");

        CollectionAssert.DoesNotContain(PostfixOwners(), _harmony.Id);
    }

    // Harmony keeps an empty patch record after an unpatch, so ask for owners, not for null.
    private static string[] PostfixOwners()
        => Harmony.GetPatchInfo(Target)?.Postfixes.Select(p => p.owner).ToArray() ?? Array.Empty<string>();

    private static Assembly EmitProbeAssembly()
    {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
            new AssemblyName("TAOM.Tests.PatchCategoryIndexProbe"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("TAOM.Tests.PatchCategoryIndexProbe");

        // A type the engine "no longer has": its assembly loads, the type is gone.
        var missingType = "TaleWorlds.RemovedByAnEngineUpdate.GoneType, " + typeof(PatchCategoryIndexTests).Assembly.FullName;
        EmitPatchClass(module, BrokenClassName, BrokenCategory, harmonyPatch: builder =>
            builder.SetCustomAttribute(
                typeof(HarmonyPatch).GetConstructor(new[] { typeof(Type) })!,
                TypeArgumentBlob(missingType)));
        EmitPatchClass(module, HealthyClassName, HealthyCategory, harmonyPatch: builder =>
            builder.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(HarmonyPatch).GetConstructor(new[] { typeof(Type), typeof(string) })!,
                new object[] { typeof(PatchCategoryIndexProbeTarget), nameof(PatchCategoryIndexProbeTarget.Probe) })));
        return assembly;
    }

    private static void EmitPatchClass(ModuleBuilder module, string name, string category, Action<TypeBuilder> harmonyPatch)
    {
        var type = module.DefineType(name,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed);
        type.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(HarmonyPatchCategory).GetConstructor(new[] { typeof(string) })!, new object[] { category }));
        harmonyPatch(type);
        var postfix = type.DefineMethod("Postfix", MethodAttributes.Public | MethodAttributes.Static,
            typeof(void), Type.EmptyTypes);
        postfix.GetILGenerator().Emit(OpCodes.Ret);
        type.CreateType();
    }

    // ECMA-335 II.23.3 custom attribute blob for a single System.Type argument: prolog 0x0001,
    // the type's assembly-qualified name as a SerString, then zero named arguments. Writing the
    // blob by hand is the only way to name a type that does not exist.
    private static byte[] TypeArgumentBlob(string assemblyQualifiedName)
    {
        var name = Encoding.UTF8.GetBytes(assemblyQualifiedName);
        var blob = new List<byte> { 0x01, 0x00 };
        if (name.Length < 0x80)
            blob.Add((byte)name.Length);
        else
            blob.AddRange(new[] { (byte)(0x80 | (name.Length >> 8)), (byte)(name.Length & 0xFF) });
        blob.AddRange(name);
        blob.AddRange(new byte[] { 0x00, 0x00 });
        return blob.ToArray();
    }
}

/// <summary>The healthy probe's patch target. Nothing else patches it.</summary>
public static class PatchCategoryIndexProbeTarget
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Probe() => 1;
}
