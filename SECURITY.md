# Security policy

TAOM is a game mod. A security problem here is, for example, code in the repo or a release that runs
commands, reads files outside the game folders, or sends data to a network host without a clear reason.

## Report a problem

Do not open a public issue for a security problem. Use GitHub private vulnerability reporting:
the **Security** tab of this repository, then **Report a vulnerability**.

Include the file or release, the steps to reproduce, and what the code does that it should not do.

## What happens next

The maintainer reads the report, replies in the report thread, and fixes confirmed problems on the
default branch. A fix for a released version goes into the next release.

## Pull requests

Outside pull requests get a security review of the full diff before anything is built. The rules are
in [CONTRIBUTING.md](CONTRIBUTING.md#what-happens-after-you-open-a-pr).
