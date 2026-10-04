# Offline benchmark of the engine's module-XML merge (Windows PowerShell 5.1, .NET Framework; scratch, read-only).
#
# Loads the installed TaleWorlds.ObjectSystem.dll and calls MBObjectManager.CreateMergedXmlFile with the file and
# XSLT lists the engine builds for one type (GetMergedXmlForManaged's rules, game-type filter applied), times it,
# then times a prototype that keeps one XDocument across files (the engine's own private MergeElements and
# CreateDocumentFromXmlFile through reflection, XSLTs compiled once), and compares the two results byte for byte.
#
# Usage: powershell.exe -NoProfile -File merge_bench.ps1 -Type NPCCharacters [-GameType Campaign] [-Runs 1]
param(
    [string]$Type = "NPCCharacters",
    [string]$GameType = "Campaign",
    [int]$Runs = 1,
    [string]$Out = "E:\repos\taom-perf\scratch\xmlmerge"
)
$ErrorActionPreference = "Stop"
$game = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
$bin = Join-Path $game "bin\Win64_Shipping_Client"
$modules = @("TAOM.Dependencies", "Native", "SandBoxCore", "CustomBattle", "Sandbox", "StoryMode", "BirthAndDeath",
             "FastMode", "LOTRLOME_Armory", "TAOM_Map", "TAOM")

[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $e)
    $n = (New-Object Reflection.AssemblyName $e.Name).Name
    $p = Join-Path $bin "$n.dll"
    if (Test-Path -LiteralPath $p) { return [Reflection.Assembly]::LoadFrom($p) }
    return $null
})
Add-Type -AssemblyName System.Xml.Linq
$null = [Reflection.Assembly]::LoadFrom((Join-Path $bin "TaleWorlds.Library.dll"))
$objAsm = [Reflection.Assembly]::LoadFrom((Join-Path $bin "TaleWorlds.ObjectSystem.dll"))
$mbom = $objAsm.GetType("TaleWorlds.ObjectSystem.MBObjectManager", $true)
$flags = [Reflection.BindingFlags]"Static, Public, NonPublic"
$miCreateMerged = $mbom.GetMethod("CreateMergedXmlFile", $flags)
$miCreateDoc = $mbom.GetMethod("CreateDocumentFromXmlFile", $flags)
$miMergeElements = $mbom.GetMethod("MergeElements", $flags)
$miToX = $mbom.GetMethod("ToXDocument", $flags)
$miToXml = $mbom.GetMethod("ToXmlDocument", $flags)
foreach ($m in @($miCreateMerged, $miCreateDoc, $miMergeElements, $miToX, $miToXml)) {
    if ($null -eq $m) { throw "a required MBObjectManager method was not found" }
}

# --- the engine's list (GetMergedXmlForManaged, ModuleHelper paths) ---
$list = [System.Collections.Generic.List[System.Tuple[string,string]]]::new()
$xslt = [System.Collections.Generic.List[string]]::new()
$defaultXsd = (Join-Path $game "XmlSchemas\$Type.xsd")
function Add-XsltFor([string]$xslPath) {
    if (Test-Path -LiteralPath $xslPath) { $xslt.Add($xslPath) }
    elseif (Test-Path -LiteralPath ($xslPath + "t")) { $xslt.Add($xslPath + "t") }
    else { $xslt.Add("") }
}
foreach ($m in $modules) {
    $mdir = Join-Path $game "Modules\$m"
    $sub = Join-Path $mdir "SubModule.xml"
    if (-not (Test-Path -LiteralPath $sub)) { continue }
    $x = New-Object Xml.XmlDocument
    $x.Load($sub)
    foreach ($node in $x.SelectNodes("/Module/Xmls/XmlNode")) {
        $nameNode = $node.SelectSingleNode("XmlName")
        if ($null -eq $nameNode) { continue }
        $id = $nameNode.GetAttribute("id"); $name = $nameNode.GetAttribute("path")
        if ($id -ne $Type) { continue }
        $gts = @($node.SelectNodes("IncludedGameTypes/GameType") | ForEach-Object { $_.GetAttribute("value") })
        if ($gts.Count -gt 0 -and -not ($gts -contains $GameType)) { continue }
        $modXsd = Join-Path $mdir "ModuleData\XmlSchemas\$id.xsd"
        $xsd = if (Test-Path -LiteralPath $modXsd) { $modXsd } else { $defaultXsd }
        $xmlPath = Join-Path $mdir "ModuleData\$name.xml"
        if (Test-Path -LiteralPath $xmlPath -PathType Leaf) {
            $list.Add([Tuple]::Create($xmlPath, $xsd)); Add-XsltFor (Join-Path $mdir "ModuleData\$name.xsl"); continue
        }
        $folder = $xmlPath.Substring(0, $xmlPath.Length - 4)
        if (Test-Path -LiteralPath $folder -PathType Container) {
            foreach ($f in (New-Object IO.DirectoryInfo $folder).GetFiles("*.xml")) {
                $p = $folder + "/" + $f.Name
                $list.Add([Tuple]::Create($p, $xsd)); Add-XsltFor ($p.Replace(".xml", ".xsl"))
            }
        } else {
            $list.Add([Tuple]::Create("", "")); Add-XsltFor (Join-Path $mdir "ModuleData\$name.xsl")
        }
    }
}
$nX = @($xslt | Where-Object { $_ -ne "" }).Count
"type=$Type gameType=$GameType entries=$($list.Count) xslts=$nX"
$list | ForEach-Object { $_.Item1 } | Set-Content -LiteralPath (Join-Path $Out "list-$Type.txt") -Encoding UTF8
$xslt | Set-Content -LiteralPath (Join-Path $Out "xslt-$Type.txt") -Encoding UTF8

# --- the schema tables the game builds at startup (XmlResource.ReadXsdFileAndExtractInformation) ---
$xrType = $objAsm.GetType("TaleWorlds.ObjectSystem.XmlResource", $true)
$miReadXsd = $xrType.GetMethod("ReadXsdFileAndExtractInformation", $flags)
$xsdSeen = @{}
foreach ($e in $list) {
    $xp = [string]$e.Item2
    if ($xp -ne "" -and -not $xsdSeen.ContainsKey($xp)) {
        $xsdSeen[$xp] = $true
        $a1 = [object[]]::new(1); $a1[0] = $xp
        $null = $miReadXsd.Invoke($null, $a1)
    }
}
"schemas read: $($xsdSeen.Count)"

# --- the engine's merge, timed ---
$engineXml = $null
for ($r = 1; $r -le $Runs; $r++) {
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $argv = [object[]]::new(3); $argv[0] = $list; $argv[1] = $xslt; $argv[2] = $false
    $doc = $miCreateMerged.Invoke($null, $argv)
    $sw.Stop()
    "engine run $r : $([math]::Round($sw.Elapsed.TotalMilliseconds)) ms"
    $engineXml = $doc.OuterXml
}

# --- the prototype: one XDocument across files, XSLTs compiled once (C# helper) ---
$cs = @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
public static class FastMerge {
    public static double MsCreate, MsToX, MsMerge, MsXslt, MsFinal;
    public static XmlDocument Run(List<Tuple<string,string>> list, List<string> xslt, MethodInfo createDoc,
                                  MethodInfo mergeElements, MethodInfo toX, MethodInfo toXml) {
        var cache = new Dictionary<string, XslCompiledTransform>();
        MsCreate = MsToX = MsMerge = MsXslt = MsFinal = 0;
        var sw = new System.Diagnostics.Stopwatch();
        var first = (XmlDocument)createDoc.Invoke(null, new object[] { list[0].Item1, list[0].Item2, false });
        var acc = (XDocument)toX.Invoke(null, new object[] { first });
        for (int i = 1; i < list.Count; i++) {
            if (xslt[i] != "") {
                sw.Restart();
                XslCompiledTransform t;
                if (!cache.TryGetValue(xslt[i], out t)) {
                    t = new XslCompiledTransform();
                    using (var sr = new StreamReader(xslt[i])) { t.Load(XmlReader.Create(sr)); }
                    cache[xslt[i]] = t;
                }
                var mid = (XmlDocument)toXml.Invoke(null, new object[] { acc });
                var outDoc = new XmlDocument(mid.CreateNavigator().NameTable);
                using (XmlWriter w = outDoc.CreateNavigator().AppendChild()) {
                    t.Transform(new XmlNodeReader(mid), w);
                }
                acc = (XDocument)toX.Invoke(null, new object[] { outDoc });
                MsXslt += sw.Elapsed.TotalMilliseconds;
            }
            if (list[i].Item1 != "") {
                sw.Restart();
                var d2 = (XmlDocument)createDoc.Invoke(null, new object[] { list[i].Item1, list[i].Item2, false });
                MsCreate += sw.Elapsed.TotalMilliseconds; sw.Restart();
                var x2 = (XDocument)toX.Invoke(null, new object[] { d2 });
                MsToX += sw.Elapsed.TotalMilliseconds; sw.Restart();
                if (list[i].Item2 == "") { acc.Root.Add(x2.Root.Elements()); }
                else { mergeElements.Invoke(null, new object[] { acc.Root, x2.Root, list[i].Item2 }); }
                MsMerge += sw.Elapsed.TotalMilliseconds;
            }
        }
        sw.Restart();
        var res = (XmlDocument)toXml.Invoke(null, new object[] { acc });
        MsFinal = sw.Elapsed.TotalMilliseconds;
        return res;
    }
}
"@
Add-Type -TypeDefinition $cs -ReferencedAssemblies System.Xml, System.Xml.Linq, System.Core
$protoXml = $null
for ($r = 1; $r -le $Runs; $r++) {
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $final = [FastMerge]::Run($list, $xslt, $miCreateDoc, $miMergeElements, $miToX, $miToXml)
    $sw.Stop()
    "prototype run $r : $([math]::Round($sw.Elapsed.TotalMilliseconds)) ms (create+validate $([math]::Round([FastMerge]::MsCreate)) ms, toXDocument $([math]::Round([FastMerge]::MsToX)) ms, MergeElements $([math]::Round([FastMerge]::MsMerge)) ms, xslt $([math]::Round([FastMerge]::MsXslt)) ms, final $([math]::Round([FastMerge]::MsFinal)) ms)"
    $protoXml = $final.OuterXml
}

$same = [string]::Equals($engineXml, $protoXml, [StringComparison]::Ordinal)
"identical=$same engineChars=$($engineXml.Length) prototypeChars=$($protoXml.Length)"
if (-not $same) {
    $n = [Math]::Min($engineXml.Length, $protoXml.Length); $k = 0
    while ($k -lt $n -and $engineXml[$k] -eq $protoXml[$k]) { $k++ }
    "first difference at char $k"
    "engine:    " + $engineXml.Substring([Math]::Max(0, $k - 120), [Math]::Min(300, $engineXml.Length - [Math]::Max(0, $k - 120)))
    "prototype: " + $protoXml.Substring([Math]::Max(0, $k - 120), [Math]::Min(300, $protoXml.Length - [Math]::Max(0, $k - 120)))
}
