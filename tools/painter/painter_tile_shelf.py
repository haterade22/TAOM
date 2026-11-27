"""Painter-side script (run through painter_run.py): list the materials of one shelf.

A resource's identifier().context is its shelf name: "starter_assets" for the
shipped ones, "your_assets" for the user's own. Downloads from Substance 3D
Assets land in "your_assets" (the shelf name is a Painter setting).

ARGS: shelf (default "your_assets"), usage (default "u:material").
RESULT: {"contexts": {context: count}, "items": [{name, type, context, url}]}
"""
import substance_painter.resource as res

shelf = ARGS.get("shelf", "your_assets")  # noqa: F821  (injected by painter_run.py)
usage = ARGS.get("usage", "u:material")  # noqa: F821
found = res.search(usage)
contexts = {}
items = []
for r in found:
    ident = r.identifier()
    contexts[ident.context] = contexts.get(ident.context, 0) + 1
    if ident.context == shelf:
        items.append({"name": ident.name, "type": r.type().name, "context": ident.context,
                      "url": r.gui_name() if hasattr(r, "gui_name") else "", "id": str(ident)})
items.sort(key=lambda i: i["name"].lower())
RESULT = {"contexts": contexts, "count": len(items), "items": items}
