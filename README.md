**Quillpad**

A lightweight text editor for Windows, for writing. Quillpad is a fork of [Notepads](https://github.com/0x7c13/Notepads) by Jackie (Jiaqi) Liu.

I use Quillpad mostly for my own writing. So it changes a lot, and the changes follow my own needs. But if it sounds useful to you, you can install a release or build it yourself. The steps are below.

**What's different from Notepads**

- Text width presets (Full, 3/4, 1/2) in the status bar, so long prose reads like a page.
- No analytics. Quillpad doesn't collect or send anything.
- It installs under its own name, so it runs next to Notepads.

**Install a release**

1. Download `Quillpad_<version>_install.zip` from [Releases](https://github.com/hab-bang/Quillpad/releases) and unzip it.
2. Right-click `Install.ps1` and choose **Run with PowerShell**.
3. Say yes when it asks to install the certificate. That needs admin rights, but only the first time.

Quillpad is signed with my own certificate, which Windows doesn't trust out of the box. That's why step 3 is there.

**Updates**

Open Settings > About and click **Check for updates**. If there's a newer release, **Install update** downloads it from GitHub, closes Quillpad and installs it. Then open Quillpad again. Close any other Quillpad windows first. Your open tabs and workspace are kept, but if session backup is off, save your files first.

After the first update this way, Windows also knows where Quillpad's updates come from. It checks when you open Quillpad and in the background, and offers or installs new versions by itself. You can also set this up directly: download `Quillpad.appinstaller` from the latest release and double-click it.

Quillpad v0.3.1 and older have no update button. To move off one of those, double-click `Quillpad.appinstaller` from a newer release once.

Releases after v0.3.0 come with a GitHub attestation for each file. If you have the [GitHub CLI](https://cli.github.com/) and are signed in to it, you can check that a file was built by the release workflow from a release tag before you trust it:

```powershell
gh attestation verify Quillpad_<version>_install.zip --repo hab-bang/Quillpad --cert-identity-regex '^https://github\.com/hab-bang/Quillpad/\.github/workflows/release\.yml@refs/tags/v'
```

Quillpad also needs two system parts, VCLibs and .NET Native, which almost every Windows PC already has. If the install says it "depends on a framework that could not be found", double-click the `.msixbundle` instead. App Installer then gets the missing part from the Microsoft Store.

**Build it yourself: what you need**

- Windows 10 or 11, with Developer Mode on. In Settings, search for "developer settings".
- Build Tools for Visual Studio 2022, with the UWP build tools and the Windows 11 SDK (10.0.22621):

  ```
  winget install --id Microsoft.VisualStudio.2022.BuildTools --override "--passive --wait --add Microsoft.VisualStudio.Workload.UniversalBuildTools --add Microsoft.VisualStudio.Component.Windows11SDK.22621 --includeRecommended"
  ```

- Git and GNU Make:

  ```
  winget install --id Git.Git
  winget install --id ezwinports.make
  ```

**Build and run**

```
git clone https://github.com/hab-bang/Quillpad.git
cd Quillpad
make run
```

The first build takes a few minutes. After that, `make run` only builds again when a file in `src/` changed. The app shows up in the Start menu as Quillpad-Dev.

| Command | What it does |
|---|---|
| `make run` | Builds if needed, then installs and opens the app |
| `make dev` | Always builds, then installs and opens the app |
| `make build` | Builds only |
| `make uninstall` | Removes the app |

**Code quality check**

`make check` counts some code smells in `src/` and compares the counts with `smell-baseline.json`. CI runs it too, on Linux, in the `smell-check` job. It needs the .NET 8 SDK or later, but not MSBuild: it reads each `.cs` file as a syntax tree and doesn't build anything. `src/Notepads.Controls` is left out, because it's mostly vendored Toolkit code.

The counts are per file. The check fails when a count goes up. It also fails when a count goes down, until the lower count is in the baseline. So the numbers can only go down, and code can't move to another file to pass.

| Check | What counts |
|---|---|
| `nesting` | A statement nested deeper than 4 levels (`if`, loops, `try`, `switch`, `using`, `lock`). Lambdas, initializers and `else if` don't add a level. |
| `method-length` | A method, constructor, accessor or local function over 60 lines. |
| `parameters` | More than 5 parameters, or more than 1 `bool` parameter. |
| `file-length` | A file over 500 lines. The baseline holds its line count, so it may not grow. |
| `duplicate` | A method body (3 or more statements) or a run of 5 or more statements that is the same as code elsewhere after variable names and literals are normalized. A variable is a local, a parameter, or another name that starts with a lower-case letter or `_`. Member, type and method names stay. |
| `catch-all` | `catch (Exception)` or a bare `catch` without a `when` filter. |
| `empty-catch` | A `catch` block with no statements. A comment alone doesn't count as one. |
| `comment-run` | 3 or more `//` comment lines in a row. `///` docs and the license header don't count. |
| `delay` | `Task.Delay` and `Thread.Sleep`. |
| `suppression` | `#pragma warning disable`, `[SuppressMessage]` or `<NoWarn>` without a reason on the same line: a `//` comment, a `Justification`, or an `<!-- -->` comment. |

The baseline also holds a hash of `src/.editorconfig`. If a rule changes there, the check fails until the baseline is updated.

When the check fails, it names the file, the member and the line of each finding of that kind in the file. Fix the code if you can. If a new count is right, or a count went down, update the baseline and say why in the commit:

```
make baseline
```

`dotnet run --project tools/SmellCheck -- --list` prints every finding, old and new.

The CI job also writes some reports to the job summary. They never fail the build:

- For a PR titled `fix:`, the lines it adds and removes, with a warning when it adds more than 5 for each one it removes.
- Changed `.cs` files that `tests/Quillpad.Tests` doesn't compile, so no unit test can reach them.
- Tests with no `Assert` in them.

**Credits and license**

Quillpad is based on Notepads, made by Jackie (Jiaqi) Liu and many contributors, and most of the code here is theirs. The original project, with its contributors and translators, is at [0x7c13/Notepads](https://github.com/0x7c13/Notepads). Quillpad keeps the same MIT License: see [LICENSE.txt](LICENSE.txt).
