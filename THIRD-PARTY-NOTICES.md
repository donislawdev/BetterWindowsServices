# Third-party notices

Better Windows Services is licensed under the GNU General Public License version 3 - see
[LICENSE](LICENSE). This file covers software written by other people that is redistributed
with it, and it exists because those licences ask for it: the MIT licence requires its
copyright notice and permission notice to travel with every copy of the software it covers.

Every licence below was read from the file inside the package on disk, not from a label on a
package listing. Where a claim is about what is or is not inside a compiled assembly, it was
checked in the assembly rather than assumed - the one that mattered is noted where it applies.

Last checked against the versions named here on 2026-08-04, except the three Windows SDK
metadata packages at the end of the build-time table, which were read on 2026-09-22 when they
were added. **Two dates rather than one, because one would be a claim nobody made:** changing
the line above to the later date would say the whole file was re-read that day, and it was not.

---

## Redistributed with the program

These are copied into the build output and go out with any release.

### WPF-UI 4.3.0 and WPF-UI.Abstractions 4.3.0

`Wpf.Ui.dll`, `Wpf.Ui.Abstractions.dll` - <https://github.com/lepoco/wpfui>

> MIT License
>
> Copyright (c) 2021-2025 Leszek Pomianowski and WPF UI Contributors. https://lepo.co/
>
> Permission is hereby granted, free of charge, to any person obtaining a copy
> of this software and associated documentation files (the "Software"), to deal
> in the Software without restriction, including without limitation the rights
> to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
> copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all
> copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
> SOFTWARE.

#### Components incorporated inside WPF-UI

WPF-UI ships its own `ThirdPartyNotices.txt` naming five components it incorporates. Four are
MIT and are therefore redistributed with it, and so with us:

| Component | Copyright | Licence |
|---|---|---|
| [sbaeumlisberger/VirtualizingWrapPanel](https://github.com/sbaeumlisberger/VirtualizingWrapPanel) 2.0.6 | (c) 2019 S. Bäumlisberger | MIT |
| [microsoft/fluentui-system-icons](https://github.com/microsoft/fluentui-system-icons) 1.1.242 | (c) 2020 Microsoft Corporation | MIT |
| [dotnet/wpf](https://github.com/dotnet/wpf) 8.0 | (c) .NET Foundation and Contributors | MIT |
| [microsoft/microsoft-ui-xaml](https://github.com/microsoft/microsoft-ui-xaml) 3.0 | (c) Microsoft Corporation | MIT |

**The fifth is Segoe Fluent Icons, and it is referenced rather than redistributed.** Its licence
says plainly that it does not grant the right to distribute or sublicense the font to a third
party, so whether it is inside the binary is a question worth answering rather than assuming.

Checked in `Wpf.Ui.dll` itself on 2026-08-04: the embedded font resources are
`FluentSystemIcons-Filled` and `FluentSystemIcons-Regular`, both from the MIT-licensed
fluentui-system-icons, reachable at `pack://application:,,,/Wpf.Ui;component/Resources/Fonts/`.
The string `Segoe Fluent Icons` occurs **once**, as a font family name in a fallback chain,
which is a reference to a font installed on the machine running the program. No Segoe font file
is carried.

### Windows SDK projection for .NET

`Microsoft.Windows.SDK.NET.dll`, `WinRT.Runtime.dll`, from `Microsoft.Windows.SDK.NET.Ref`.

These arrive automatically from targeting `net10.0-windows10.0.17763.0` and are how managed code
reaches the Windows API. **Their licence is not an open source one.** The package declares
`© Microsoft Corporation. All rights reserved.`, requires licence acceptance, and points at the
Windows SDK licence at <https://aka.ms/WinSDKLicenseURL>. They are redistributable as part of a
program that runs on Windows, under those terms.

This is the standard arrangement for a .NET program on Windows rather than anything unusual
here, and GPLv3 section 1 excludes System Libraries - the components that come with the
operating system and serve only to let a program use it - from the source a distributor has to
provide. **That reading is not legal advice and nobody with a licence to give it has been
asked.** It is recorded so that whoever needs to be sure knows exactly which files raise the
question: these two, and nothing else in the build output.

### The .NET runtime

`Microsoft.NETCore.App.Runtime.win-x64` - <https://github.com/dotnet/runtime>

A self-contained publish bundles the .NET runtime and libraries into the executable. MIT
licensed, © Microsoft Corporation and the .NET Foundation and Contributors. **Counted rather
than estimated on 2026-09-23**: 187 assemblies inside `bws.exe` and 186 inside
`BetterWindowsServices.exe`.

### The .NET desktop runtime

`Microsoft.WindowsDesktop.App.Runtime.win-x64` - <https://github.com/dotnet/wpf>

**This is WPF itself, and until 2026-09-23 this file did not name it.** The section above said
"the .NET runtime and libraries" and that sentence covered, without saying so, the 53 further
assemblies that draw every window this program opens. MIT licensed, © Microsoft Corporation and
the .NET Foundation and Contributors. Only `BetterWindowsServices.exe` carries it: the command
line targets `net10.0-windows` for the Windows API surface, not for a user interface.

### Which version of those two

Whatever .NET built the file, rather than a number written here. The exact version is in the
SPDX document published beside each archive on the releases page, and `bws license --components`
prints the one the running program is on. A number in this file would be true on the machine that
wrote it and false on the next build, which is the failure this file already paid for once with
`log 0.4.33` in another project of the same owner.

---

## Used to build and test, never redistributed

None of these end up in the program. They are listed because a reader auditing the dependency
list should be able to tell at a glance which side of the line each one falls on.

| Package | Licence | What it does |
|---|---|---|
| Microsoft.Windows.CsWin32 0.3.298 | MIT | Generates the P/Invoke declarations at build time. Marked `PrivateAssets="all"`, so the generator itself does not ship - the code it generates is compiled into ours |
| Meziantou.Analyzer 3.0.138 | MIT, (c) Gérald Barré | Extra analyser rules |
| xunit 2.9.3, xunit.runner.visualstudio 3.1.4 | Apache-2.0 | Test framework |
| Microsoft.NET.Test.Sdk 17.14.1 | MIT | Test host |
| coverlet.collector 6.0.4 | MIT | Coverage collection |
| CsCheck 4.7.0 | Apache-2.0 | Property-based testing |
| Microsoft.Windows.SDK.Win32Metadata 70.0.11-preview | Windows SDK licence terms | The machine-readable description of the Win32 API that CsWin32 reads to generate from. Arrives as its dependency rather than being asked for |
| Microsoft.Windows.WDK.Win32Metadata 0.13.25-experimental | Windows SDK licence terms | The same thing for the driver-facing half of the API |
| Microsoft.Windows.SDK.Win32Docs 0.1.42-alpha | Windows SDK licence terms | The documentation text CsWin32 copies into the generated declarations, so that hovering a generated method shows what Microsoft says about it |

**The last three rows are not open source and that is worth stating rather than leaving to be
assumed.** Read from the packages on disk on 2026-09-22 rather than from a listing: two of them
carry `sdk_license.txt`, which is `MICROSOFT SOFTWARE LICENSE TERMS - MICROSOFT WINDOWS SOFTWARE
DEVELOPMENT KIT (SDK) FOR WINDOWS 10`, and the third points at the same terms through
<https://aka.ms/WinSDKLicenseURL>. Those terms license the use of the SDK for building software
for Windows. They do not license redistributing the SDK, and this project does not redistribute
it: all three contribute build-time inputs only, their package entries carry the empty
placeholder `_._` where an assembly would be, and a publish of either program puts none of them
anywhere. The same question for the two files that DO ship is answered in the section above.

**They are here because the gate asked.** `.github/scripts/dependency_gate.py` blocked them on
2026-09-22, the first time it ever saw this repository's full dependency graph, and nothing in
this file said which side of the line they fell on. That was a fair question and this table is
the answer to it.

---

## Keeping this true

**Since 2026-09-23 this file has a machine-readable twin.** `packaging/components.json` is the
curated register of the same set: name, version, SPDX licence identifier and where each one came
from. The SPDX document attached to every release is generated from it, `bws license
--components` prints it from inside the executable for a machine with no internet, and this file
is the third rendering - the one that carries the legal texts, which neither of the other two
can. `ComponentRegisterGuards` fails the build when the register names something this file does
not, in both directions, and when a pinned binary stops hashing to what the register pins.

**Why a register at all, when a scanner could read the release.** Both programs publish as a
single self-contained file, so everything named above is inside an executable with no package
metadata left anywhere. A scan of what a user downloads would find two files and assign a licence
to neither.

`LicenceNoticeGuards` in `tests/Bws.Architecture.Tests` fails the build when a package is
referenced by a shipped project and is not named in this file. A notice file that quietly stops
matching the dependency list is worse than none, because it reads like a completed check.

What that guard cannot do, said plainly so a green build is not read as more than it is: it
compares names, not terms. Whether a licence still says what it said when somebody read it, and
whether a new version of a package changed what it incorporates, is a question for a person.
