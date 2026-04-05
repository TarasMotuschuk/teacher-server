# TeacherClient.Avalonia.Setup

This setup project packages the self-contained macOS Avalonia teacher client as:

- a `.app` bundle
- a `.pkg` installer that installs the app into `/Applications`

## Build

```bash
cd TeacherClient.Avalonia.Setup
bash ./Build-MacInstaller.sh
```

Or:

```bash
cd TeacherClient.Avalonia.Setup
./Build-MacInstaller.command
```

## Output

- App bundle: `artifacts/ClassCommander.app`
- Installer: `dist/ClassCommander-macos.pkg`

The `.pkg` installs the bundle into `/Applications` (see `Build-MacInstaller.sh`). Optional environment variables include `APP_NAME`, `PRODUCT_NAME`, `BUNDLE_ID`, `VERSION`, `CONFIGURATION`, and `RUNTIME` (defaults match CI: `Release`, `osx-arm64`).
