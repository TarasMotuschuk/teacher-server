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
- Installer: `dist/ClassCommander.Setup.pkg`

The `.pkg` installs the bundle into `/Applications` (see `Build-MacInstaller.sh`). Optional environment variables include `APP_NAME`, `PRODUCT_NAME`, `BUNDLE_ID`, `VERSION`, `CONFIGURATION`, and `RUNTIME` (defaults match CI: `Release`, `osx-arm64`).

For macOS signing in GitHub Actions, export your Apple Development certificate as a `.p12`, store it as a base64 secret, and add these repository secrets:

- `MACOS_APP_CERT_BASE64`
- `MACOS_APP_CERT_PASSWORD`

The workflow imports that certificate into a temporary keychain and sets `SIGNING_MODE=auto`, so the build uses Apple Development signing when the secret is present and falls back to ad-hoc signing for local dev builds when it is not.
