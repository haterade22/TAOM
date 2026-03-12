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
