#requires -Version 7.0
<#
.SYNOPSIS
    Test helper: compiles tiny C# assemblies with the Roslyn that pwsh 7 bundles, so the
    check_engine_member_refs tests can build 'engine' and 'mod' fixtures without a game install.

.PARAMETER Jobs
    Path to a JSON array of { "name", "out", "src", "refs" } objects, built in order. "src" is a
    C# source file, "refs" a list of assembly paths (earlier outputs allowed).
#>
param([Parameter(Mandatory)][string]$Jobs)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
$rt = [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$base = @([object].Assembly.Location, (Join-Path $rt 'System.Runtime.dll'))
foreach ($job in (Get-Content -Raw -LiteralPath $Jobs | ConvertFrom-Json)) {
    $paths = $base + @($job.refs | Where-Object { $_ })
    $mrefs = [Microsoft.CodeAnalysis.MetadataReference[]]@($paths | ForEach-Object { [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($_) })
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText((Get-Content -Raw -LiteralPath $job.src))
    $opts = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary)
    $comp = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create($job.name, [Microsoft.CodeAnalysis.SyntaxTree[]]@($tree), $mrefs, $opts)
    $ms = [IO.MemoryStream]::new()
    $res = $comp.Emit($ms)
    if (-not $res.Success) { $res.Diagnostics | ForEach-Object { [Console]::Error.WriteLine($_.ToString()) }; exit 1 }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $job.out) | Out-Null
    [IO.File]::WriteAllBytes($job.out, $ms.ToArray())
}
