# Compile cost against transform cost for TAOM's XSLTs (Windows PowerShell 5.1; scratch, read-only).
param([string]$Dir = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\ModuleData")
$ErrorActionPreference = "Stop"
$cs = @"
using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Xsl;
public static class XsltCost {
    public static string Measure(string path) {
        var sw = Stopwatch.StartNew();
        var t = new XslCompiledTransform();
        using (var sr = new StreamReader(path)) { t.Load(XmlReader.Create(sr)); }
        double compile = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        var t2 = new XslCompiledTransform();
        using (var sr = new StreamReader(path)) { t2.Load(XmlReader.Create(sr)); }
        double compile2 = sw.Elapsed.TotalMilliseconds;
        return string.Format("{0}: {1:N0} bytes, compile {2:N0} ms, compile again {3:N0} ms",
            Path.GetFileName(path), new FileInfo(path).Length, compile, compile2);
    }
}
"@
Add-Type -TypeDefinition $cs -ReferencedAssemblies System.Xml
Get-ChildItem -LiteralPath $Dir -Filter *.xslt | Sort-Object Length -Descending | ForEach-Object { [XsltCost]::Measure($_.FullName) }
