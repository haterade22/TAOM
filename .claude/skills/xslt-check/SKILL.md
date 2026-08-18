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

   **Scope limit, know this before you trust a PASS.** That path is the repo's ModuleData, which
   holds 8 of TAOM's 16 XSLT files. The other 8 live in the game install and this skill does not
   reach them: `TAOM_Map/ModuleData/settlements.xslt`, and the Armory's `action_sets.xslt`,
   `action_types.xslt`, `Animations/action_sets.xslt`, `crafting_templates.xslt`,
   `monster_usage_sets.xslt`, `MonsterUsage/LOTR/lotr_monster_usage_spider.xslt`,
   `weapon_descriptions.xslt`. The CI `validate-xml` job globs the same repo path, and it cannot be
   extended to cover them because those modules are not in the checkout. To check one of the eight,
   pass an absolute path under
   `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\<module>\ModuleData\` and apply
   the same steps by hand. See
   [moduledata-validation.md](../../../docs/features/moduledata-validation.md) "Module coverage at a
   glance" for what else those two modules do and do not get.

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
