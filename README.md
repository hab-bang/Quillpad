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

Quillpad is signed with my own certificate, which Windows doesn't trust out of the box. That's why step 3 is there. After that, you can update by double-clicking the newer `.msixbundle`.

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

**Credits and license**

Quillpad is based on Notepads, made by Jackie (Jiaqi) Liu and many contributors, and most of the code here is theirs. The original project, with its contributors and translators, is at [0x7c13/Notepads](https://github.com/0x7c13/Notepads). Quillpad keeps the same MIT License: see [LICENSE.txt](LICENSE.txt).
