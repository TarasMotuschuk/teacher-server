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

> **Note:** `Apple Development` / `Apple Distribution` certificates are intended for local debugging on devices registered in your developer account. A `.pkg` signed with them installs fine but the `.app` will be killed on launch by Gatekeeper on any other Mac. For public distribution you need a `Developer ID Application` + `Developer ID Installer` pair, plus notarization. That flow is not wired up yet.

## Testing Screen Recording / Microphone with an ad-hoc build

The default `SIGNING_MODE=none` / `adhoc` flow is useful for quick local verification on the teacher's own Mac, but it has known limitations caused by macOS TCC (Transparency, Consent, Control):

- TCC identifies apps by their code signature's *designated requirement*. Ad-hoc signatures have no Team ID, so TCC falls back to the binary's `cdhash`.
- Any content change (new build, new `.pkg`) changes the `cdhash`, so macOS treats the new build as a different app and re-prompts for Screen Recording / Microphone / Camera, even if the toggle in System Settings is still on for the old entry.
- Screen Recording grants only take effect after the app is fully relaunched (`Cmd+Q` + start again), not just on the next window open.

If you see the "grant permission → app keeps asking → nothing works" loop during a local screen-sharing demo, do this on the teacher Mac:

1. Build and install the `.pkg` once so the app lives in `/Applications/ClassCommander.app`. TCC treats apps in `/Applications` more reliably than copies in `~/Downloads`, `/tmp`, or the build output folder.
2. Reset any stale TCC entries for the bundle ID (the default is `com.tarasmotuschuk.teacherclient.avalonia`):

    ```bash
    tccutil reset ScreenCapture com.tarasmotuschuk.teacherclient.avalonia
    tccutil reset Microphone    com.tarasmotuschuk.teacherclient.avalonia
    tccutil reset Camera        com.tarasmotuschuk.teacherclient.avalonia
    killall tccd || true
    ```

3. Launch `ClassCommander` from `/Applications`, trigger the screen-share / audio capture action so macOS shows the TCC prompt, and enable the toggle in `System Settings → Privacy & Security → Screen Recording` (and `Microphone` if prompted).
4. **Fully quit** the app with `Cmd+Q` (not just close the window) and start it again. Screen Recording grants do not apply to the already-running process.
5. Re-test the demonstration. If step 2 was done and the app was launched from `/Applications`, the grant should stick for the current build.

Known limits of this ad-hoc workflow:

- Every new build invalidates the grant and forces steps 2–4 again.
- Distribution to other Macs requires Developer ID signing + notarization; an ad-hoc `.pkg` installed on another machine will not satisfy Gatekeeper.
- `NSMicrophoneUsageDescription` and `NSCameraUsageDescription` are now set in `Resources/Info.plist.template`. Without them the process is killed the first time it touches the mic or camera, which used to look like "permissions never apply".
