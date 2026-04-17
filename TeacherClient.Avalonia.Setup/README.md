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

Set `SIGNING_MODE=apple-development` and provide the exact certificate name in `APP_SIGN_IDENTITY` if you want a signed build. If you also want the installer signed, provide `PKG_SIGN_IDENTITY`. Without an explicit signing mode, the script now leaves the build unsigned.

The workflow imports the certificate into a temporary keychain, but the build now uses the identity you pass explicitly instead of auto-detecting it from the keychain.
