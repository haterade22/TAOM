#!/usr/bin/env pwsh
#requires -Version 7.0
<#
.SYNOPSIS
    Resolves every engine MemberReference of one or more DLLs against the installed Bannerlord
    assemblies, to catch a shipped binary that calls a member the engine update removed.

.DESCRIPTION
    A fresh compile binds whatever overload the new engine offers, so it hides a member that a
    previously shipped TAOM.dll still calls. v1.5.4 removed a TooltipProperty constructor and the
    v2.0.33 build threw MissingMethodException at runtime while every source and binding gate stayed
    green. This tool reads each DLL's metadata (System.Reflection.Metadata, no assembly loading),
    takes every MemberRef whose declaring type lives in a TaleWorlds/SandBox/StoryMode assembly, and
    looks it up by declaring type, name and full signature in the engine assemblies, walking the base
    type chain for members referenced through a derived type.

    The C# is compiled in memory with Add-Type; nothing is written to disk.

    Output per DLL: one line per unresolved ref,
        MISSING NEW|PRE-EXISTING [assembly] Type::member|sig
    then
        checked N distinct engine member refs, M unresolved (K new)

    Exit codes: 1 if any NEW miss in any DLL (any miss at all when -Baseline is not given),
    0 otherwise, 2 on bad input (missing DLL, no engine directory).

    LIMITS: refs whose parent is a TypeSpecification (a generic instantiation such as
    List<Foo>::Add) are skipped, so a clean run is not proof for members of generic types. Only
    refs into assemblies named TaleWorlds*, SandBox* or StoryMode* are checked.

.PARAMETER Dll
    One or more DLLs to check. Under `pwsh -File` pass several as one comma-joined string.

.PARAMETER GameDir
    Install root. Default mirrors Directory.Build.props: BANNERLORD_OVERRIDE_DIR (if its bin holds
    Bannerlord.exe) else BANNERLORD_GAME_DIR.

.PARAMETER EngineDir
    Overrides the engine directories (default: the game bin folder plus the Native, SandBox,
    SandBoxCore, StoryMode, CustomBattle, BirthAndDeath and Multiplayer module bin folders that
    exist). Lets a test point at a reference-assembly folder.

.PARAMETER Baseline
    Directories of OLD engine assemblies (for example the previous version's NuGet
    bannerlord.referenceassemblies.*\<build>\ref\net472 folders). A miss that also misses against
    the baseline is reported PRE-EXISTING (it was already broken before the update), otherwise NEW.

.EXAMPLE
    pwsh tools/check_engine_member_refs.ps1 -Dll E:\LOTRAOM_Releases\testing\Modules\TAOM\bin\Win64_Shipping_Client\TAOM.dll

.EXAMPLE
    pwsh tools/check_engine_member_refs.ps1 -Dll (Get-ChildItem E:\LOTRAOM_Releases\testing\Modules\TAOM.Dependencies\bin\Win64_Shipping_Client\*.dll)

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
    public readonly Dictionary<string, HashSet<string>> Defs = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    public readonly Dictionary<string, string> Bases = new Dictionary<string, string>(StringComparer.Ordinal);
    public readonly HashSet<string> Assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public EngineIndex(string[] dirs)
    {
        foreach (var dir in dirs)
        foreach (var f in Directory.GetFiles(dir, "*.dll"))
        {
            var an = Path.GetFileNameWithoutExtension(f);
            if (!(an.StartsWith("TaleWorlds") || an.StartsWith("SandBox") || an.StartsWith("StoryMode"))) continue;
            try
            {
                using (var pe = new PEReader(File.OpenRead(f)))
                {
                    if (!pe.HasMetadata) continue;
                    var md = pe.GetMetadataReader();
                    Assemblies.Add(md.GetString(md.GetAssemblyDefinition().Name));
                    var prov = new SigProv(md);
                    foreach (var th in md.TypeDefinitions)
                    {
                        var td = md.GetTypeDefinition(th);
                        var tn = Refs.TypeDefName(md, th);
                        HashSet<string> set;
                        if (!Defs.TryGetValue(tn, out set)) Defs[tn] = set = new HashSet<string>();
                        if (!td.BaseType.IsNil) { try { Bases[tn] = Refs.HandleName(md, td.BaseType); } catch { } }
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

    public bool Has(string typeName, string key)
    {
        var t = typeName; int guard = 0;
        while (t != null && guard++ < 20)
        {
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
    public List<string> Lines = new List<string>();
}

public static class Refs
{
    public static CheckResult Check(string modPath, EngineIndex engine, EngineIndex baseline)
    {
        var res = new CheckResult();
        using (var mpe = new PEReader(File.OpenRead(modPath)))
        {
            if (!mpe.HasMetadata) return res;
            var mmd = mpe.GetMetadataReader();
            var mprov = new SigProv(mmd);
            var seen = new HashSet<string>();
            foreach (var rh in mmd.MemberReferences)
            {
                var mr = mmd.GetMemberReference(rh);
                if (mr.Parent.Kind != HandleKind.TypeReference) continue;
                var tr = mmd.GetTypeReference((TypeReferenceHandle)mr.Parent);
                var scopeAsm = ScopeAsm(mmd, tr);
                if (scopeAsm == null || !engine.Assemblies.Contains(scopeAsm)) continue;
                var tn = HandleName(mmd, mr.Parent);
                var name = mmd.GetString(mr.Name);
                string key = mr.GetKind() == MemberReferenceKind.Method
                    ? name + "|" + Sig(mr.DecodeMethodSignature(mprov, null))
                    : name + "|F:" + mr.DecodeFieldSignature(mprov, null);
                if (!seen.Add(tn + "::" + key)) continue;
                res.Checked++;
                if (engine.Has(tn, key)) continue;
                res.Missing++;
                bool preExisting = baseline != null && !baseline.Has(tn, key);
                if (!preExisting) res.New++;
                res.Lines.Add("MISSING " + (preExisting ? "PRE-EXISTING" : "NEW") + " [" + scopeAsm + "] " + tn + "::" + key);
            }
        }
        return res;
    }

    public static string Sig(MethodSignature<string> s)
    {
        return (s.Header.IsInstance ? "inst " : "") + s.ReturnType + "(" + string.Join(",", s.ParameterTypes) + ")"
            + (s.GenericParameterCount > 0 ? "`" + s.GenericParameterCount : "");
    }

    static string ScopeAsm(MetadataReader md, TypeReference tr)
    {
        var scope = tr.ResolutionScope;
        while (scope.Kind == HandleKind.TypeReference)
            scope = md.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
        if (scope.Kind == HandleKind.AssemblyReference)
            return md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
        return null;
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

    public static string HandleName(MetadataReader md, EntityHandle h)
    {
        switch (h.Kind)
        {
            case HandleKind.TypeDefinition: return TypeDefName(md, (TypeDefinitionHandle)h);
            case HandleKind.TypeReference: return TypeRefName(md, (TypeReferenceHandle)h);
            case HandleKind.TypeSpecification: return "spec";
            default: return "?";
        }
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

foreach ($d in $Dll) {
    if (-not (Test-Path -LiteralPath $d -PathType Leaf)) { Exit-BadInput "DLL not found: $d" }
}

if (-not $EngineDir) {
    if (-not $GameDir) { $GameDir = Resolve-GameDir }
    if (-not $GameDir) { Exit-BadInput 'cannot resolve game dir: set BANNERLORD_GAME_DIR, or pass -GameDir or -EngineDir.' }
    $bin = 'Win64_Shipping_Client'
    $EngineDir = @(Join-Path $GameDir "bin\$bin") + @(
        'Native', 'SandBox', 'SandBoxCore', 'StoryMode', 'CustomBattle', 'BirthAndDeath', 'Multiplayer' |
            ForEach-Object { Join-Path $GameDir "Modules\$_\bin\$bin" })
}
$EngineDir = @($EngineDir | Where-Object { Test-Path -LiteralPath $_ -PathType Container })
if ($EngineDir.Count -eq 0) { Exit-BadInput 'no engine directory exists.' }
if ($Baseline) { $Baseline = @($Baseline | Where-Object { Test-Path -LiteralPath $_ -PathType Container }) }
if ($PSBoundParameters.ContainsKey('Baseline') -and (-not $Baseline -or $Baseline.Count -eq 0)) {
    Exit-BadInput 'no baseline directory exists.'
}

Add-Type -TypeDefinition $csharp -Language CSharp

$engine = [EngineIndex]::new([string[]]$EngineDir)
$base = if ($Baseline) { [EngineIndex]::new([string[]]$Baseline) } else { $null }

$anyNew = $false
foreach ($d in $Dll) {
    $path = (Resolve-Path -LiteralPath $d).Path
    Write-Host "== $path"
    $r = [Refs]::Check($path, $engine, $base)
    foreach ($line in $r.Lines) { Write-Host $line }
    Write-Host "checked $($r.Checked) distinct engine member refs, $($r.Missing) unresolved ($($r.New) new)"
    if ($r.New -gt 0) { $anyNew = $true }
}
if ($anyNew) { exit 1 }
exit 0
