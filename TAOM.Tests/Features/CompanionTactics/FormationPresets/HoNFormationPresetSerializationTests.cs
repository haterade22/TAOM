using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.SaveSystem;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Pins the invariant that every <see cref="SaveableFieldAttribute"/> field on
/// <see cref="HoNFormationPreset"/> is a type the TaleWorlds SaveSystem can actually serialize.
///
/// Regression guard for the save-corruption CTD where a <c>DateTime _createdAt</c> field (id 3)
/// crashed every campaign save: DateTime has no registered definition, so on save
/// <c>VariableSaveData</c> fell through to the CustomStruct path (<c>writer.WriteInt((int)Value)</c>
/// → InvalidCastException), leaving a null serialized buffer that NRE'd in <c>GameData.Write</c>'s
/// <c>.Sum((byte[] x) =&gt; x.Length)</c> on the async save thread → AggregateException CTD.
///
/// PRECISION (Codex/deep-review 2026-06-21): the engine requires the EXACT closed container type to
/// be registered — a <c>List&lt;basic&gt;</c> / <c>Dictionary&lt;basic,basic&gt;</c> whose closed type is
/// NOT registered (e.g. <c>Dictionary&lt;float,int&gt;</c>) still crashes at save even though its element
/// types look fine. So this test uses an explicit "known-serializable for this save graph" allowlist
/// (basic types + registered classes + exactly-registered containers + registered enums), each entry
/// cited to its registering definer, rather than a structural "List-of-basic is fine" heuristic.
/// It fails CLOSED: a new field of an un-allowlisted type fails here, forcing the author to confirm the
/// type is registered (and extend the allowlist) before it can ship. Pure-managed (no live engine).
/// </summary>
[TestClass]
public class HoNFormationPresetSerializationTests
{
    // Engine basic value/string types — TaleWorlds.SaveSystem.SaveableBasicTypeDefiner.DefineBasicTypes()
    // (verified via ilspycmd on the installed TaleWorlds.SaveSystem.dll, 2026-06-21).
    private static readonly HashSet<Type> BasicTypes = new()
    {
        typeof(string), typeof(bool),
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double),
    };

    // Library math structs + MBGUID, also basic in the engine (same definer). Matched by full name to
    // avoid a hard reference; HoNFormationPreset uses none today, but a future field of one should pass.
    private static readonly HashSet<string> BasicStructFullNames = new()
    {
        "TaleWorlds.Library.Vec2", "TaleWorlds.Library.Vec2i",
        "TaleWorlds.Library.Vec3", "TaleWorlds.Library.Vec3i",
        "TaleWorlds.Library.Mat2", "TaleWorlds.Library.Mat3",
        "TaleWorlds.Library.MatrixFrame", "TaleWorlds.Library.Quaternion",
        "TaleWorlds.Library.Color", "TaleWorlds.ObjectSystem.MBGUID",
    };

    // Class types registered via FormationPresetSaveableTypeDefiner.DefineClassTypes(). Tied to the
    // definer source by Definer_RegistersOnlyModSpecificContainers_AndTheClass below.
    private static readonly HashSet<Type> RegisteredClassTypes = new()
    {
        typeof(HoNFormationPreset),
    };

    // EXACT closed container types registered for this save graph. Source per entry:
    //   [engine] = SaveableBasicTypeDefiner.DefineContainerDefinitions() (installed DLL, verified 2026-06-21)
    //   [taom]   = FormationPresetSaveableTypeDefiner.DefineContainerDefinitions()
    // To add a new container field: confirm the exact closed type is registered by one of those definers
    // (decompile the engine definer, or add a ConstructContainerDefinition to the TAOM definer), then
    // add it here with its source tag.
    private static readonly HashSet<Type> RegisteredContainerTypes = new()
    {
        typeof(Dictionary<string, int>),  // [engine]
        typeof(Dictionary<int, int>),     // [engine]
        typeof(List<string>),             // [engine]
        typeof(List<HoNFormationPreset>), // [taom] — the SyncData payload type
    };

    // Enum types registered via a SaveableTypeDefiner (engine SaveableCoreTypeDefiner registers e.g.
    // FormationClass). Empty today — HoNFormationPreset has no enum field. A future enum field will fail
    // EverySaveableField_* until its enum type is confirmed registered and listed here. This is
    // intentional: blanket-accepting all enums would let an UNregistered enum ship (a DateTime-class hole).
    private static readonly HashSet<Type> RegisteredEnumTypes = new();

    [TestMethod]
    public void EverySaveableField_IsSerializableByTheSaveSystem()
    {
        var offenders = new List<string>();

        foreach (var field in GetSaveableFields(typeof(HoNFormationPreset)))
        {
            if (!IsSaveSerializable(field.Field.FieldType))
            {
                offenders.Add($"id {field.LocalSaveId}: {field.Field.Name} ({field.Field.FieldType.FullName})");
            }
        }

        Assert.AreEqual(
            0, offenders.Count,
            "HoNFormationPreset has [SaveableField] members the TaleWorlds SaveSystem cannot serialize " +
            "(this corrupts every campaign save). Each field type must be a basic type, a registered enum, " +
            "an exactly-registered container, or a registered class — see the allowlists at the top of this " +
            "test. Offending fields: " + string.Join("; ", offenders));
    }

    [TestMethod]
    public void SaveableFieldIds_AreUnique()
    {
        var ids = GetSaveableFields(typeof(HoNFormationPreset)).Select(f => f.LocalSaveId).ToList();
        CollectionAssert.AllItemsAreUnique(ids, "Duplicate [SaveableField] ids corrupt serialization.");
    }

    [TestMethod]
    public void RetiredDateTimeFieldId3_IsNotReused()
    {
        // Id 3 held the unserializable DateTime _createdAt. The gap is intentional; reusing it for a
        // non-equivalent field would mismap any already-persisted data. Keep it retired.
        var usesId3 = GetSaveableFields(typeof(HoNFormationPreset)).Any(f => f.LocalSaveId == 3);
        Assert.IsFalse(usesId3, "SaveableField id 3 is retired (was DateTime _createdAt) — do not reuse it.");
    }

    [TestMethod]
    public void EveryContainerField_HasItsExactClosedTypeAllowlisted()
    {
        // Direct guard for the false-positive the previous structural check allowed: a generic container
        // field whose EXACT closed type is not in RegisteredContainerTypes (e.g. a future Dictionary<float,int>)
        // would crash at save. This asserts the allowlist actually covers the fields in use.
        var missing = GetSaveableFields(typeof(HoNFormationPreset))
            .Select(f => f.Field.FieldType)
            .Where(t => t.IsGenericType)
            .Where(t => !RegisteredContainerTypes.Contains(t))
            .Select(t => t.FullName)
            .Distinct()
            .ToList();

        Assert.AreEqual(0, missing.Count,
            "Container [SaveableField] type(s) not in RegisteredContainerTypes (the engine needs the exact " +
            "closed type registered): " + string.Join("; ", missing));
    }

    [TestMethod]
    public void Definer_RegistersOnlyModSpecificContainers_AndTheClass()
    {
        // Consistency check tying the TAOM-side allowlist entries to the definer source (prevents the
        // allowlist drifting from production). Also pins the 2026-06-21 hygiene fix: the definer must NOT
        // re-register the engine-provided containers (Dictionary<string,int>/<int,int>, List<string>) —
        // doing so triggers Debug.FailedAssert("duplicate definition") at save-system init.
        var src = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Main", "Features", "CompanionTactics", "FormationPresets", "Models",
            "FormationPresetSaveableTypeDefiner.cs"));

        StringAssert.Contains(src, "ConstructContainerDefinition(typeof(List<HoNFormationPreset>))",
            "Definer must register the mod-specific SyncData payload container.");
        StringAssert.Contains(src, "AddClassDefinition(typeof(HoNFormationPreset)",
            "Definer must register the HoNFormationPreset class.");

        foreach (var engineProvided in new[]
                 {
                     "ConstructContainerDefinition(typeof(Dictionary<string, int>))",
                     "ConstructContainerDefinition(typeof(Dictionary<int, int>))",
                     "ConstructContainerDefinition(typeof(List<string>))",
                 })
        {
            Assert.IsFalse(src.Contains(engineProvided),
                $"Definer re-registers an engine-provided container ({engineProvided}) → duplicate-definition " +
                "assert at save-system init. Rely on the engine's SaveableBasicTypeDefiner instead.");
        }
    }

    private static IEnumerable<(FieldInfo Field, short LocalSaveId)> GetSaveableFields(Type type)
    {
        return type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(f => (Field: f, Attr: f.GetCustomAttribute<SaveableFieldAttribute>()))
            .Where(x => x.Attr != null)
            .Select(x => (x.Field, x.Attr!.LocalSaveId));
    }

    private static bool IsSaveSerializable(Type t) =>
        IsBasic(t)
        || (t.IsEnum && RegisteredEnumTypes.Contains(t))
        || RegisteredClassTypes.Contains(t)
        || RegisteredContainerTypes.Contains(t);

    private static bool IsBasic(Type t) =>
        BasicTypes.Contains(t) || (t.FullName != null && BasicStructFullNames.Contains(t.FullName));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }
}
