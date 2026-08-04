# Better Windows Services

A replacement for `services.msc`, for people who administer Windows machines. Free software,
Windows only, and version 1 works on the local machine.

**What makes it different is auditing and drift, not a prettier list of services.** Take a
snapshot of every service on a machine, compare two of them, or compare one against the machine
as it is now, and see exactly what changed - with configuration reported apart from running
state, because a service that started since yesterday is not drift and a start type that changed
by itself is.

**Status: early.** Version `0.1.0`. The command line does the work listed below. The window shows
and filters the same data and cannot yet change anything.

```
bws list --query "start:auto !status:running"     what should be up and is not
bws list --query "file:missing"                   services whose binary is gone
bws stop Spooler --dry-run --dependents           what stopping it would take down
bws snapshot create before.json                   freeze the machine before a change
bws snapshot diff before.json --live --exit-code  what has changed since
```

Every operation that writes builds a plan first. The plan can be previewed with `--dry-run`, and
what the preview shows is what the execution does - the same steps, in the same order, with the
same reasons beside them.

## Building

.NET 10 SDK, Windows, 64-bit.

```
dotnet build BetterWindowsServices.slnx -c Release
dotnet test BetterWindowsServices.slnx -c Release
```

Some tests read the real service control manager on the machine they run on, and compare what
this tool says against what `sc.exe` says. They are read-only.

## Licence

GNU General Public License version 3 - see [LICENSE](LICENSE).

Other people's code is redistributed with the program, and what it is and what its licences
require is written down in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Security reports go through the channel described in [SECURITY.md](SECURITY.md).

## A note on the comments, so they are not confusing

The comments in this code refer often to design documents - `docs/01`, `ADR-14`, "the
specification", a backlog item by number. **Those documents are not in this repository.** They
are kept and versioned separately by the project owner, and the same goes for `tools/`, which
holds the measuring and probing scripts several comments mention.

This is worth knowing rather than worth apologising for: those references are the reason a
comment can say *why* something is the way it is instead of repeating *what* the line does. What
a reader here loses is the long-form argument behind a decision. What is not lost is the
decision itself, because the comments carry it - the reasoning in this code is written for
somebody who will meet the same problem, not as a pointer to somewhere else.

If you need the reasoning behind a particular decision to make a change, open an issue and ask.
That is a better answer than guessing.
