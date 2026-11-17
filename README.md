**Quillpad**

A lightweight text editor for Windows, for writing. Quillpad is a fork of [Notepads](https://github.com/0x7c13/Notepads) by Jackie (Jiaqi) Liu.

I use Quillpad mostly for my own writing. So it changes a lot, and the changes follow my own needs. There's no release or installer yet, but if it sounds useful to you, you can build it and run it yourself. The steps are below.

**What's different from Notepads**

- Text width presets (Full, 3/4, 1/2) in the status bar, so long prose reads like a page.
- No analytics. Quillpad doesn't collect or send anything.
- It installs under its own name, so it runs next to Notepads.

**What you need**

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
