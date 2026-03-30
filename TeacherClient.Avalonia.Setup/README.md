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

- App bundle: `artifacts/Teacher Classroom Client.app`
- Installer: `dist/TeacherClassroomClient-macos.pkg`
