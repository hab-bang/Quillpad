# Build and run the Quillpad dev app from the terminal, without the Visual Studio IDE.
# Requirements are listed at the top of build.ps1.
#
#   make build      Compile, but only if a file under src/ changed since the last build
#   make run        Same as build, then install and open the dev app
#   make dev        Always run MSBuild (it is incremental itself), then install and open the dev app
#   make uninstall  Remove the installed dev app
#   make test       Run the unit tests in tests/Quillpad.Tests and tools/SmellCheck.Tests (needs the .NET 8 SDK or later)
#   make check      Count code smells in src/ and fail if a count differs from smell-baseline.json
#   make baseline   Write the current counts to smell-baseline.json
#
# The dev app installs as "Quillpad-Dev", next to (not over) an installed Quillpad or Notepads.

BUILD_SCRIPT := powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
BUILD_STAMP  := src/Notepads/bin/x64/Debug/.make-build-stamp
MARK_BUILT   := powershell -NoProfile -Command "Set-Content -Path '$(BUILD_STAMP)' -Value built"
SMELL_CHECK  := dotnet run --project tools/SmellCheck -c Release --

# Every file under src/ that git tracks or would track. $(wildcard) drops deleted files, and also
# the halves of the few paths that contain spaces, which make cannot use as prerequisites.
SOURCES := $(wildcard $(shell git ls-files --cached --others --exclude-standard src))

.PHONY: build run dev uninstall test check baseline

build: $(BUILD_STAMP)

$(BUILD_STAMP): $(SOURCES)
	$(BUILD_SCRIPT) build
	$(MARK_BUILT)

run: build
	$(BUILD_SCRIPT) launch

dev:
	$(BUILD_SCRIPT) build
	$(MARK_BUILT)
	$(BUILD_SCRIPT) launch

uninstall:
	$(BUILD_SCRIPT) uninstall

test:
	dotnet test tests/Quillpad.Tests --nologo
	dotnet test tools/SmellCheck.Tests --nologo

check:
	$(SMELL_CHECK)

baseline:
	$(SMELL_CHECK) --update
