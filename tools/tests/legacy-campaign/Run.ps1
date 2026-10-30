# Requires PowerShell 7 (uses its bundled Roslyn compiler). Does not deploy.
param([string]$RepoRoot = (Join-Path $PSScriptRoot '../../..'))
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'BoundaryHarness.cs.txt') -Raw
$source += Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SupplyHarness.cs.txt') -Raw
$targets = @(
    @('Main/Features/AlignmentDesertion/Hooks/AlignmentDesertionBehavior.cs', 'AlignmentDesertionBehavior', @('OnDailyTickParty', 'OnDailyTickSettlement')),
    @('Main/Features/SpecialResources/SpecialResourcesBehavior.cs', 'SpecialResourcesBehavior', @('RegisterEvents', 'UnsubscribeScreenManager')),
    @('Main/Features/TroopWeight/Diagnostics/TroopCountDiagnosticsBehavior.cs', 'TroopCountDiagnosticsBehavior', @('RegisterEvents', 'Unsubscribe')),
    @('Main/Features/SupplyLines/SupplyCaravanService.cs', 'SupplyCaravanService', @('ComputeNavPathPoints')),
    @('Main/Features/SupplyLines/SupplyRouteVisualService.cs', 'SupplyRouteVisualService', @('SyncArrows'))
)
foreach ($target in $targets) {
    $text = Get-Content -LiteralPath (Join-Path $RepoRoot $target[0]) -Raw
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($text)
    $nodes = $tree.GetRoot().DescendantNodes()
    foreach ($name in $target[2]) {
        $methods = @($nodes | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax] -and $_.Identifier.ValueText -eq $name })
        if ($methods.Count -ne 1 -or $null -eq $methods[0].Body) { throw "Expected one method body: $($target[0])::$name" }
        $source = $source.Replace("/*__$($target[1]).$($name)__*/", $methods[0].Body.ToFullString())
    }
    if ($target[1] -in @('SpecialResourcesBehavior', 'TroopCountDiagnosticsBehavior')) {
        $fields = @($nodes | Where-Object {
            $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax] -and
            @($_.Declaration.Variables | Where-Object { $_.Identifier.ValueText -in @('_screenManagerSubscribed', '_screenSubscriber', '_subscribed') }).Count -gt 0
        })
        $source = $source.Replace("/*__Fields:$($target[1])__*/", (($fields | ForEach-Object { $_.ToFullString() }) -join "`n"))
    }
}
if ($source.Contains('/*__')) { throw 'Unexpanded source placeholder.' }
Add-Type -TypeDefinition $source -IgnoreWarnings -WarningAction SilentlyContinue
$result = [Program]::Main()
[Program]::Messages | Write-Output
[xml]$troops = Get-Content -LiteralPath (Join-Path $RepoRoot 'Main/_Module/ModuleData/troops/troops_isengard.xml') -Raw
foreach ($id in @('urukhai_champion', 'urukhai_berserker')) {
    $items = @($troops.SelectNodes("//NPCCharacter[@id='$id']//equipment[@id='Item.sk_uruk_hai_skirt_a1']"))
    $valid = $items.Count -gt 0 -and @($items | Where-Object { $_.slot -ne 'Body' }).Count -eq 0
    if ($valid) { Write-Output "PASS $id equips the BodyArmor item in Body" }
    else { Write-Output "FAIL $id must equip the BodyArmor item in Body"; $result = 1 }
}
exit $result
