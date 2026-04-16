## `vpxmd.dylib` (macOS VP8 native dependency)

The demo WebRTC VP8 path uses `SIPSorceryMedia.Encoders`, which P/Invokes into a native library named `vpxmd`.

For macOS packaging we ship it inside the `.app` bundle as:

- `ClassCommander.app/Contents/MacOS/vpxmd.dylib`

### Where to put the binaries in this repo

Place a prebuilt `vpxmd.dylib` here (one per RID):

- `TeacherClient.Avalonia.Setup/Resources/vpxmd/osx-arm64/vpxmd.dylib`
- `TeacherClient.Avalonia.Setup/Resources/vpxmd/osx-x64/vpxmd.dylib`

The packaging script `TeacherClient.Avalonia.Setup/Build-MacInstaller.sh` will automatically stage the file matching `RUNTIME`.

### Notes

- This file is a native library dependency (libvpx wrapper / libvpx build) and must match the target architecture.
- If you don't want to commit native binaries, you can instead provide:
  - `CLASSCOMMANDER_VPXMD_MACOS_DYLIB=/absolute/path/to/vpxmd.dylib`, or
  - `CLASSCOMMANDER_LIBVPX_MACOS_DYLIB=/absolute/path/to/libvpx.dylib` (it will be staged as `vpxmd.dylib`).
