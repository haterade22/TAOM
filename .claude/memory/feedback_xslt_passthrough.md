---
name: XSLT must pass through all vanilla attributes
description: Never filter out vanilla XML attributes in XSLT transforms — use apply-templates for @* and unmodified child elements
type: feedback
---

XSLT transforms must copy ALL vanilla attributes and elements, then override only what changes.

**Why:** Critical attributes like `is_main_culture`, `can_have_settlement`, `faction_banner_key` are silently dropped if not passed through, causing hard-to-diagnose runtime issues.

**How to apply:** Always include `<xsl:apply-templates select="@*"/>` and `<xsl:apply-templates select="*[not(...)]"/>` in XSLT templates. Only exclude elements you're explicitly replacing.
