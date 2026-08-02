# Security

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

Use GitHub's private vulnerability reporting on this repository: go to the **Security** tab
and choose **Report a vulnerability**. That gives us a private thread, and it needs no email
address from either of us.

## What is supported

Only the latest version. This project has not made a release yet, so today that means the tip
of `main`.

## What to expect

**This is a one-person project.** Saying so is more useful than a promise nobody would keep:
expect a first reply within about a week, and expect it from one person who may be busy.

If a report is valid, the fix and the credit both happen in the open once the problem is
closed. If a report describes something already known and deliberate, you will get the
reasoning rather than silence - see the scope below.

## Scope

This tool reads the Windows service control manager and changes services through a plan. It
runs with whatever rights the person running it has, which on a server is usually
administrative.

**In scope**, and worth reporting:

- Anything that makes the tool change a service nobody asked it to change.
- Anything that makes it write outside the file the operator named.
- Anything that makes it reach off the machine it is running on.
- Anything that makes it execute a program, load code, or interpret data as code.
- A crash, a hang, or a wrong answer caused by a malformed snapshot file, a query, or a
  service registered on the machine with an unusual configuration.
- A reading reported as complete when part of it was refused. A tool that hides a refusal is
  worse than one that fails.

**Out of scope**, deliberately, and each of these is a decision rather than an oversight:

- **Anything that requires administrative rights on the machine to begin with.** Somebody who
  can already replace this executable does not need a vulnerability in it.
- **The tool doing what it was told.** Stopping a service is the point of the program.
- **Signature verification as a trust decision.** The tool reports what Windows says about a
  binary's signature. A signed binary can still be hostile, and this tool does not claim
  otherwise.
- **Memory use and start-up time.** Those are budgets, tracked as such, not security issues.
- **The `--follow-network` switch reaching a network path when explicitly asked.** It is off
  by default for exactly this reason, and turning it on is a deliberate act.

A fuller version of the same boundary, including what the project deliberately does not
protect, is kept with the project's own documentation.
