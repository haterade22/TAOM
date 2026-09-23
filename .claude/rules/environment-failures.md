---
description: Environment failures are reported, not fixed. The agent works within infra; the user controls infra.
---

# Environment failures: report, don't fix

When something outside the repo's tracked files breaks (a missing tool, a broken path, permissions,
an MCP server down, Bannerlord install drift, GitHub auth, the network), **report it and stop; don't
fix it.** Infra fixes are hard to reverse from here, the user knows which install, feed or server is
intended, and self-healing hides the real problem until it breaks differently.

- **The line:** anything in the repo's tracked files is yours to fix: a build error, a failing test,
  a broken XSLT, a hook script that throws. Tools on PATH, env vars, the Steam install, MCP servers
  and OS config are the user's.
- **Examples:** `ilspycmd` missing: ask before `dotnet tool install -g ilspycmd`. `gh auth status`
  fails: "run `gh auth login` when convenient". Game DLL paths in `Directory.Build.props` don't
  resolve: confirm the install path rather than editing the file.
- **Check the machine first** (orientation.md "Two machines"): never edit the repo to quiet the
  laptop's broken references.
- **Report** what you tried, the exact error, a one-line suspicion, and the smallest next step for
  the user. State facts; drift is normal, and nobody's machine is at fault.
