---
paths:
  - "**/*.xslt"
  - "Main/_Module/ModuleData/*.xslt"
---

# XSLT Transformation Rules

## Authoritative Source
- **SandBoxCore/ModuleData/** is the authoritative reference for vanilla XML structure
- NEVER use SandBox/ModuleData/ — it has different element names the engine ignores
- Example: SandBoxCore uses `<notable_templates>` (engine reads), SandBox uses `<notable_and_wanderer_templates>` (engine ignores)

## Passthrough Requirements (CRITICAL)
- Always pass through ALL vanilla attributes: `<xsl:apply-templates select="@*"/>`
- Always pass through unmodified child elements: `<xsl:apply-templates select="*[not(...)]"/>`
- Never filter out vanilla attributes — critical ones like `is_main_culture`, `can_have_settlement`, `faction_banner_key` will be silently dropped
- Only override the specific attributes/elements you intend to change

## Passthrough cuts both ways (the bug that has now shipped four times)

The rules above cover one direction: passthrough protects you from DROPPING a vanilla attribute.
The other direction is the one that keeps shipping. **An attribute your block never names is not
"unchanged", it is INHERITED, and what it inherits is Calradia.** Nothing in the file looks wrong,
because the wrong value is not in the file.

Instances: Dale 2026-05-26, Rohan, Khand 2026-08-04 (#374), settlement patrols 2026-08-12. The last
one shipped nine town-owning cultures fielding vanilla patrols, villagers, militia, rebels and
caravans, four of them because the block simply never mentioned `settlement_patrol_template_level_*`.

**Before editing ANY `Culture[@id=...]` template, including a one-attribute repoint:** enumerate
every attribute the deserializer reads (`pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CultureObject`,
then read `Deserialize`), and classify each BIND / PASSTHROUGH / N-A with the decision in a comment.
Then prove it mechanically rather than by eye: transform over installed
`SandBoxCore/ModuleData/spcultures.xml` with lxml and flag every emitted attribute whose value still
carries a vanilla culture id. That is a ten-line script and it is what found the last seven.

## Overriding a CHILD element takes two edits, not one

Attributes replace. Child elements **union**. `CultureObject.Deserialize` (v1.4.8,
`CultureObject.cs:485-497`) does `mBList10.Add(...)` inside a loop over EVERY child named
`caravan_party_templates`, so emitting your own does not displace vanilla's:

- Emit the TAOM child element, **and**
- add it to that block's `not(self::...)` passthrough filter.

Do only the first and the culture carries both, so it rolls Calradian roughly half the time. That is
worse than a clean miss, because it is nondeterministic and a single play session can look fine.
The six filters in `spcultures.xslt` are NOT identical (they exclude 8, 7 or 2 names depending on the
block), so edit each in place rather than pasting one over another.

**Watch for the attribute that is never read at all.** `caravan_party_template` and
`elite_caravan_party_template` look bindable and are pure dead markup: the deserializer takes caravans
only from the plural child elements. Four blocks carried them for months, two pointing at vanilla ids,
which made the blocks look handled. Check `Deserialize` before binding anything.

## The gate

`TAOM.Tests/Core/CulturePartyTemplateTests.cs` runs `spcultures.xslt` over a synthetic vanilla
document whose every party-template binding is a unique `PartyTemplate.SENTINEL_*` value, then fails
on attribute-absent, sentinel-survived (unbound, so vanilla would supply it) or bound-to-a-non-TAOM-id.
Reading the `<xsl:attribute>` markup textually cannot do this: it cannot see an exclusion filter, and
it cannot see an attribute that is not there. If you extend the stylesheet to bind something new,
extend that test's attribute list too. Contract:
[`docs/features/culture-playability-wiring.md`](../../docs/features/culture-playability-wiring.md).

## Identity Transform
Every XSLT file must include the identity transform template to copy unmatched nodes:
```xml
<xsl:template match="@*|node()">
  <xsl:copy>
    <xsl:apply-templates select="@*|node()"/>
  </xsl:copy>
</xsl:template>
```

## Common Mistakes to Avoid
- Overly broad `xsl:template match` that catches unintended elements
- Hardcoding attribute values that should be passed through from vanilla
- Missing `xsl:output` declaration
- Forgetting to handle child elements when overriding a parent
