#!/usr/bin/env pwsh
#requires -Version 7.0
<#
.SYNOPSIS
    Resolves every engine MemberReference and TypeReference of one or more DLLs against the
    installed Bannerlord assemblies, to catch a shipped binary that uses a member or type the
    engine update removed.

.DESCRIPTION
    A fresh compile binds whatever overload the new engine offers, so it hides a member that a
    previously shipped TAOM.dll still calls. v1.5.4 removed a TooltipProperty constructor and the
    v2.0.33 build threw MissingMethodException at runtime while every source and binding gate stayed
    green. This tool reads each DLL's metadata (System.Reflection.Metadata, no assembly loading),
    takes every MemberRef and TypeRef whose type lives in a TaleWorlds/SandBox/StoryMode/NavalDLC
    assembly, and looks it up by assembly, declaring type, name and full signature in the engine
    assemblies, walking the base type chain for members referenced through a derived type. A member
    of a generic type (List<Foo>::Add) is resolved through its generic type definition. A type that
    moved to another assembly with no type forwarder is MISSING.

    The C# is compiled in memory with Add-Type; nothing is written to disk.

    Output per DLL (a DLL with no engine refs is skipped):
        MISSING NEW|PRE-EXISTING [assembly] Type::member|sig
        MISSING NEW|PRE-EXISTING type [assembly] Type
        UNCHECKED [assembly] referenced, but no engine directory holds it
    then
        checked N distinct engine refs, M unresolved (K new)

    A miss is PRE-EXISTING only when the baseline holds that assembly and also lacks the member or
    type. With -Baseline the engine and baseline BuildInfo.GameVersion are printed.

    Exit codes: 1 if any NEW miss in any DLL (any miss at all when -Baseline is not given), 0
    otherwise, 2 on bad input or an unreliable check: a DLL, engine or baseline directory that does
    not exist, an index with no engine assembly, a DLL that is not a .NET assembly, a -Dll wildcard
    that matches nothing, a baseline of the same build as the engine, or an UNCHECKED assembly.

.PARAMETER Dll
    One or more DLLs to check; wildcards (*, ?) are expanded. Under `pwsh -File` pass several as one
    comma-joined string.

.PARAMETER GameDir
    Install root. Default mirrors Directory.Build.props: BANNERLORD_OVERRIDE_DIR (if its bin holds
    Bannerlord.exe) else BANNERLORD_GAME_DIR.

.PARAMETER EngineDir
    Overrides the engine directories (default: the game bin folder plus the Native, SandBox,
    SandBoxCore, StoryMode, CustomBattle, BirthAndDeath, Multiplayer and NavalDLC module bin
    folders that exist). Lets a test point at a reference-assembly folder. Every one must exist.

.PARAMETER Baseline
    Directories of OLD engine assemblies (for example the previous version's NuGet
    bannerlord.referenceassemblies.*\<build>\ref\net472 folders); every one must exist. A miss that
    also misses against the baseline is reported PRE-EXISTING (it was already broken before the
    update), otherwise NEW.

.EXAMPLE
    pwsh tools/check_engine_member_refs.ps1 -Dll E:\LOTRAOM_Releases\testing\Modules\TAOM\bin\Win64_Shipping_Client\TAOM.dll

.EXAMPLE
    pwsh tools/check_engine_member_refs.ps1 -Dll 'E:\LOTRAOM_Releases\testing\Modules\*\bin\Win64_Shipping_Client\*.dll'

.NOTES
    Used by .claude/skills/engine-bump/SKILL.md Phase 1 step 5. Run a fresh build as the negative
    control (it must report 0 unresolved).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$Dll,
    [string]$GameDir,
    [string[]]$EngineDir,
    [string[]]$Baseline
)

$ErrorActionPreference = 'Stop'

# `pwsh -File` hands a list parameter one string, so a comma-joined value ("a,b") is split here.
$Dll = @($Dll | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
if ($EngineDir) { $EngineDir = @($EngineDir | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) }
if ($Baseline) { $Baseline = @($Baseline | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) }

$csharp = @'
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

public sealed class EngineIndex
{
    // Defs and Bases are keyed "assembly|Type", so a type that moved assemblies is not found by name alone.
    public readonly Dictionary<string, HashSet<string>> Defs = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    public readonly Dictionary<string, string> Bases = new Dictionary<string, string>(StringComparer.Ordinal);
    public readonly Dictionary<string, string> Forwards = new Dictionary<string, string>(StringComparer.Ordinal);
    public readonly HashSet<string> Assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public string GameVersion;

    public EngineIndex(string[] dirs)
    {
        foreach (var dir in dirs)
        foreach (var f in Directory.GetFiles(dir, "*.dll"))
        {
            if (!Refs.IsEngineName(Path.GetFileNameWithoutExtension(f))) continue;
            try
            {
                using (var pe = new PEReader(File.OpenRead(f)))
                {
                    if (!pe.HasMetadata) continue;
                    var md = pe.GetMetadataReader();
                    var asm = md.GetString(md.GetAssemblyDefinition().Name);
                    Assemblies.Add(asm);
                    var prov = new SigProv(md);
                    foreach (var eh in md.ExportedTypes)
                    {
                        var et = md.GetExportedType(eh);
                        if (!et.IsForwarder || et.Implementation.Kind != HandleKind.AssemblyReference) continue;
                        var ns = md.GetString(et.Namespace);
                        var n = (ns.Length > 0 ? ns + "." : "") + md.GetString(et.Name);
                        Forwards[asm + "|" + n] = md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)et.Implementation).Name);
                    }
                    foreach (var th in md.TypeDefinitions)
                    {
                        var td = md.GetTypeDefinition(th);
                        var tn = asm + "|" + Refs.TypeDefName(md, th);
                        HashSet<string> set;
                        if (!Defs.TryGetValue(tn, out set)) Defs[tn] = set = new HashSet<string>();
                        if (!td.BaseType.IsNil) { try { var b = Refs.Qualified(md, td.BaseType, asm); if (b != null) Bases[tn] = b; } catch (BadImageFormatException) { } }
                        if (tn == "TaleWorlds.Library|BuildInfo") ReadGameVersion(md, td);
                        foreach (var mh in td.GetMethods())
                        {
                            var m = md.GetMethodDefinition(mh);
                            set.Add(md.GetString(m.Name) + "|" + Refs.Sig(m.DecodeSignature(prov, null)));
                        }
                        foreach (var fh in td.GetFields())
                        {
                            var fd = md.GetFieldDefinition(fh);
                            set.Add(md.GetString(fd.Name) + "|F:" + fd.DecodeSignature(prov, null));
                        }
                    }
                }
            }
            catch (BadImageFormatException) { }
        }
    }

    void ReadGameVersion(MetadataReader md, TypeDefinition td)
    {
        foreach (var gh in td.GetFields())
        {
            var gf = md.GetFieldDefinition(gh);
            if (md.GetString(gf.Name) != "GameVersion" || gf.GetDefaultValue().IsNil) continue;
            var br = md.GetBlobReader(md.GetConstant(gf.GetDefaultValue()).Value);
            GameVersion = br.ReadUTF16(br.Length);
        }
    }

    // Follows a type forwarder one hop at a time.
    string Forwarded(string t)
    {
        string fwd;
        if (!Defs.ContainsKey(t) && Forwards.TryGetValue(t, out fwd)) return fwd + t.Substring(t.IndexOf('|'));
        return t;
    }

    public bool HasType(string asm, string typeName)
    {
        return Defs.ContainsKey(Forwarded(asm + "|" + typeName));
    }

    public bool Has(string asm, string typeName, string key)
    {
        var t = asm + "|" + typeName; int guard = 0;
        while (t != null && guard++ < 20)
        {
            t = Forwarded(t);
            HashSet<string> set;
            if (Defs.TryGetValue(t, out set) && set.Contains(key)) return true;
            string next;
            t = Bases.TryGetValue(t, out next) ? next : null;
        }
        return false;
    }
}

public sealed class CheckResult
{
    public int Checked;
    public int Missing;
    public int New;
    public HashSet<string> Unchecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public List<string> Lines = new List<string>();
}

public static class Refs
{
    public static bool IsEngineName(string an)
    {
        return an.StartsWith("TaleWorlds") || an.StartsWith("SandBox") || an.StartsWith("StoryMode") || an.StartsWith("NavalDLC");
    }

    public static CheckResult Check(string modPath, EngineIndex engine, EngineIndex baseline)
    {
        var res = new CheckResult();
        using (var mpe = new PEReader(File.OpenRead(modPath)))
        {
            if (!mpe.HasMetadata) throw new InvalidDataException("not a .NET assembly: " + modPath);
            var mmd = mpe.GetMetadataReader();
            var mprov = new SigProv(mmd);
            var seen = new HashSet<string>();
            foreach (var rh in mmd.MemberReferences)
            {
                var mr = mmd.GetMemberReference(rh);
                var parent = Resolve(mmd, mr.Parent);
                if (parent.Kind != HandleKind.TypeReference) continue;
                var scopeAsm = ScopeAsm(mmd, mmd.GetTypeReference((TypeReferenceHandle)parent));
                if (!InScope(res, engine, scopeAsm)) continue;
                var tn = TypeRefName(mmd, (TypeReferenceHandle)parent);
                var name = mmd.GetString(mr.Name);
                string key = mr.GetKind() == MemberReferenceKind.Method
                    ? name + "|" + Sig(mr.DecodeMethodSignature(mprov, null))
                    : name + "|F:" + mr.DecodeFieldSignature(mprov, null);
                if (!seen.Add(scopeAsm + "|" + tn + "::" + key)) continue;
                res.Checked++;
                if (engine.Has(scopeAsm, tn, key)) continue;
                Report(res, !(baseline != null && baseline.Assemblies.Contains(scopeAsm) && !baseline.Has(scopeAsm, tn, key)), "", scopeAsm, tn + "::" + key);
            }
            foreach (var th in mmd.TypeReferences)
            {
                var scopeAsm = ScopeAsm(mmd, mmd.GetTypeReference(th));
                if (!InScope(res, engine, scopeAsm)) continue;
                var tn = TypeRefName(mmd, th);
                if (!seen.Add(scopeAsm + "|" + tn)) continue;
                res.Checked++;
                if (engine.HasType(scopeAsm, tn)) continue;
                Report(res, !(baseline != null && baseline.Assemblies.Contains(scopeAsm) && !baseline.HasType(scopeAsm, tn)), "type ", scopeAsm, tn);
            }
        }
        return res;
    }

    // PRE-EXISTING needs the baseline to hold the assembly and also lack the member or type; a baseline
    // without the assembly says nothing, so the miss stays NEW.
    static void Report(CheckResult res, bool isNew, string kind, string asm, string what)
    {
        res.Missing++;
        if (isNew) res.New++;
        res.Lines.Add("MISSING " + (isNew ? "NEW" : "PRE-EXISTING") + " " + kind + "[" + asm + "] " + what);
    }

    static bool InScope(CheckResult res, EngineIndex engine, string scopeAsm)
    {
        if (scopeAsm == null) return false;
        if (engine.Assemblies.Contains(scopeAsm)) return true;
        if (IsEngineName(scopeAsm) && res.Unchecked.Add(scopeAsm))
            res.Lines.Add("UNCHECKED [" + scopeAsm + "] referenced, but no engine directory holds it");
        return false;
    }

    public static string Sig(MethodSignature<string> s)
    {
        return (s.Header.IsInstance ? "inst " : "") + s.ReturnType + "(" + string.Join(",", s.ParameterTypes) + ")"
            + (s.GenericParameterCount > 0 ? "`" + s.GenericParameterCount : "");
    }

    // Null for a ref with no assembly scope, or whose scope row is unreadable (0Harmony.dll carries
    // type refs with an out-of-range scope; they name no engine assembly, so they are skipped).
    public static string ScopeAsm(MetadataReader md, TypeReference tr)
    {
        try
        {
            var scope = tr.ResolutionScope;
            while (scope.Kind == HandleKind.TypeReference)
                scope = md.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
            if (scope.Kind == HandleKind.AssemblyReference)
                return md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
        }
        catch (BadImageFormatException) { }
        return null;
    }

    // A generic instantiation (List<Foo>) resolves to its generic type definition; any other
    // TypeSpecification (array, pointer) yields a nil handle.
    public static EntityHandle Resolve(MetadataReader md, EntityHandle h)
    {
        if (h.Kind != HandleKind.TypeSpecification) return h;
        var br = md.GetBlobReader(md.GetTypeSpecification((TypeSpecificationHandle)h).Signature);
        if (br.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance) return default(EntityHandle);
        br.ReadByte();
        return br.ReadTypeHandle();
    }

    public static string Qualified(MetadataReader md, EntityHandle h, string selfAsm)
    {
        h = Resolve(md, h);
        switch (h.Kind)
        {
            case HandleKind.TypeDefinition: return selfAsm + "|" + TypeDefName(md, (TypeDefinitionHandle)h);
            case HandleKind.TypeReference:
                var tr = md.GetTypeReference((TypeReferenceHandle)h);
                return (ScopeAsm(md, tr) ?? selfAsm) + "|" + TypeRefName(md, (TypeReferenceHandle)h);
            default: return null;
        }
    }

    public static string TypeDefName(MetadataReader md, TypeDefinitionHandle h)
    {
        var td = md.GetTypeDefinition(h);
        var name = md.GetString(td.Name);
        var decl = td.GetDeclaringType();
        if (!decl.IsNil) return TypeDefName(md, decl) + "/" + name;
        var ns = md.GetString(td.Namespace);
        return ns.Length > 0 ? ns + "." + name : name;
    }

    public static string TypeRefName(MetadataReader md, TypeReferenceHandle h)
    {
        var tr = md.GetTypeReference(h);
        var name = md.GetString(tr.Name);
        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            return TypeRefName(md, (TypeReferenceHandle)tr.ResolutionScope) + "/" + name;
        var ns = md.GetString(tr.Namespace);
        return ns.Length > 0 ? ns + "." + name : name;
    }
}

public sealed class SigProv : ISignatureTypeProvider<string, object>
{
    public SigProv(MetadataReader md) { }
    public string GetPrimitiveType(PrimitiveTypeCode t) { return t.ToString(); }
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) { return Refs.TypeDefName(r, h); }
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) { return Refs.TypeRefName(r, h); }
    public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte k) { return r.GetTypeSpecification(h).DecodeSignature(this, c); }
    public string GetSZArrayType(string e) { return e + "[]"; }
    public string GetArrayType(string e, ArrayShape s) { return e + "[" + new string(',', s.Rank - 1) + "]"; }
    public string GetByReferenceType(string e) { return e + "&"; }
    public string GetPointerType(string e) { return e + "*"; }
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) { return g + "<" + string.Join(",", a) + ">"; }
    public string GetGenericTypeParameter(object c, int i) { return "!" + i; }
    public string GetGenericMethodParameter(object c, int i) { return "!!" + i; }
    public string GetFunctionPointerType(MethodSignature<string> s) { return "fnptr"; }
    public string GetModifiedType(string m, string u, bool req) { return u; }
    public string GetPinnedType(string e) { return e; }
}
'@

function Resolve-GameDir {
    $over = $env:BANNERLORD_OVERRIDE_DIR
    if ($over -and (Test-Path (Join-Path $over 'bin\Win64_Shipping_Client\Bannerlord.exe'))) { return $over }
    $game = $env:BANNERLORD_GAME_DIR
    if ($game -and (Test-Path $game)) { return $game }
    return $null
}

function Exit-BadInput($message) {
    [Console]::Error.WriteLine("check_engine_member_refs: $message")
    exit 2
}

function Assert-Directories($label, $dirs) {
    $absent = @($dirs | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Container) })
    if ($absent.Count -gt 0) { Exit-BadInput "$label directory not found: $($absent -join ', ')" }
}

$files = foreach ($d in $Dll) {
    if ($d -match '[*?]') {
        $hits = @(Get-ChildItem -Path $d -File -ErrorAction SilentlyContinue)
        if ($hits.Count -eq 0) { Exit-BadInput "no DLL matches: $d" }
        $hits.FullName
    } else {
        if (-not (Test-Path -LiteralPath $d -PathType Leaf)) { Exit-BadInput "DLL not found: $d" }
        (Resolve-Path -LiteralPath $d).Path
    }
}

if ($EngineDir) {
    Assert-Directories 'engine' $EngineDir
} else {
    if (-not $GameDir) { $GameDir = Resolve-GameDir }
    if (-not $GameDir) { Exit-BadInput 'cannot resolve game dir: set BANNERLORD_GAME_DIR, or pass -GameDir or -EngineDir.' }
    $bin = 'Win64_Shipping_Client'
    $EngineDir = @(Join-Path $GameDir "bin\$bin") + @(
        'Native', 'SandBox', 'SandBoxCore', 'StoryMode', 'CustomBattle', 'BirthAndDeath', 'Multiplayer', 'NavalDLC' |
            ForEach-Object { Join-Path $GameDir "Modules\$_\bin\$bin" })
    $EngineDir = @($EngineDir | Where-Object { Test-Path -LiteralPath $_ -PathType Container })
    if ($EngineDir.Count -eq 0) { Exit-BadInput "no engine directory exists under $GameDir." }
}
if ($PSBoundParameters.ContainsKey('Baseline')) {
    if (-not $Baseline) { Exit-BadInput 'no baseline directory given.' }
    Assert-Directories 'baseline' $Baseline
}

$anyNew = $false
$anyUnchecked = $false
$skipped = 0
try {
    Add-Type -TypeDefinition $csharp -Language CSharp
    $engine = [EngineIndex]::new([string[]]$EngineDir)
    if ($engine.Assemblies.Count -eq 0) { Exit-BadInput "no TaleWorlds/SandBox/StoryMode assembly in: $($EngineDir -join ', ')" }
    $base = $null
    if ($Baseline) {
        $base = [EngineIndex]::new([string[]]$Baseline)
        if ($base.Assemblies.Count -eq 0) { Exit-BadInput "no TaleWorlds/SandBox/StoryMode assembly in the baseline: $($Baseline -join ', ')" }
        if ($engine.GameVersion -and $engine.GameVersion -eq $base.GameVersion) {
            Exit-BadInput "the baseline is the same build as the engine ($($engine.GameVersion)); it must be the previous version."
        }
        Write-Host "engine $($engine.GameVersion), baseline $($base.GameVersion)"
    }
    foreach ($path in $files) {
        $r = [Refs]::Check($path, $engine, $base)
        if ($r.Checked -eq 0 -and $r.Lines.Count -eq 0) { $skipped++; continue }
        Write-Host "== $path"
        foreach ($line in $r.Lines) { Write-Host $line }
        Write-Host "checked $($r.Checked) distinct engine refs, $($r.Missing) unresolved ($($r.New) new)"
        if ($r.New -gt 0) { $anyNew = $true }
        if ($r.Unchecked.Count -gt 0) { $anyUnchecked = $true }
    }
} catch {
    Exit-BadInput "$($_.Exception.Message)"
}
if ($skipped -gt 0) { Write-Host "$skipped DLL(s) skipped: no engine refs" }
if ($anyUnchecked) { Exit-BadInput 'some referenced engine assemblies were not checked (UNCHECKED lines above).' }
if ($anyNew) { exit 1 }
exit 0
