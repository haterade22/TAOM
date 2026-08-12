---
name: xslt-check
description: Validate XSLT transformations against SandBoxCore vanilla XML to ensure correct passthrough
argument-hint: [xslt-filename]
---

# XSLT Validation Check

Validate XSLT file against SandBoxCore source data.

## Target: `$ARGUMENTS`

## Validation Steps

1. **Read the XSLT file** from `Main/_Module/ModuleData/$ARGUMENTS`

2. **Read the corresponding SandBoxCore vanilla XML** from:
   `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\`

   File mapping:
   - `spkingdoms.xslt` → `spkingdoms.xml`
   - `spcultures.xslt` → `spcultures.xml`
   - `spclans.xslt` → `spclans.xml`
   - `lords.xslt` → `lords.xml`
   - `heroes.xslt` → `heroes.xml`
   - `module_strings.xslt` → `module_strings.xml`

3. **Check passthrough rules** (CRITICAL):
   - XSLT MUST use `<xsl:apply-templates select="@*"/>` to pass through ALL vanilla attributes
   - XSLT MUST use `<xsl:apply-templates select="*[not(...)]"/>` to pass through child elements
   - Never filter out vanilla attributes — critical ones like `is_main_culture`, `can_have_settlement`, `faction_banner_key` will be silently dropped
   - Reference: SandBoxCore is authoritative (NOT SandBox)
   - **Every child element the block EMITS must also appear in that block's `not(self::...)` filter.**
     Child elements union rather than replace (`CultureObject.Deserialize` calls `.Add(...)` in a loop
     over every matching child), so emitting without excluding leaves the culture holding both copies.
     The filters differ per block, so check each one against what that block actually emits.

3b. **Transform it, do not just read it** (this is the step that catches what reading misses):
   ```
   python -c "from lxml import etree; import sys; \
     out=etree.XSLT(etree.parse(r'Main/_Module/ModuleData/$ARGUMENTS'))(etree.parse(r'<vanilla path>')); \
     print(etree.tostring(out, pretty_print=True).decode()[:4000])"
   ```
   Then diff the emitted element against vanilla's and flag **every attribute whose value still carries
   a vanilla id**. An attribute the block never names is not "unchanged", it is inherited, and reading
   the markup cannot see something that is not in the markup. This skill reported clean on all four
   instances of that bug (Dale, Rohan, Khand, settlement patrols) for exactly that reason.

3c. **For `spcultures.xslt` specifically, run the executable gate:**
   `dotnet test TAOM.Tests --filter FullyQualifiedName~CulturePartyTemplate -p:DisableModuleCopy=true -p:ModuleId=`
   It does 3b automatically against a sentinel stub. Contract:
   [`docs/features/culture-playability-wiring.md`](../../../docs/features/culture-playability-wiring.md).

4. **Verify element names match engine expectations**:
   - SandBoxCore uses `<notable_templates>` (engine reads this)
   - SandBox uses `<notable_and_wanderer_templates>` (engine ignores this)
   - Always match SandBoxCore element naming

5. **Check for common XSLT errors**:
   - Missing identity transform template
   - Overly broad `xsl:template match` that catches unintended elements
   - Hardcoded attribute values that should be passed through from vanilla
   - Missing `xsl:output` declaration

6. **Report findings** with specific line numbers and recommended fixes.
