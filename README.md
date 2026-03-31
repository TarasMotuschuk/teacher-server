# Teacher Server

`Teacher Server` is a .NET 8 classroom administration solution for a transparent, teacher-controlled environment. It includes:

- `StudentAgent.Service`: Windows Service host for privileged agent runtime duties.
- `StudentAgent.UIHost`: Windows Forms session UI process for tray controls, warnings, and visible student overlays.
- `TeacherServer.Setup`: WiX-based MSI installer project for Windows deployment.
- `TeacherClient`: Windows Forms application used by the teacher.
- `TeacherClient.Avalonia`: cross-platform desktop client for macOS, Linux, and Windows.
- `TeacherClient.Avalonia.Setup`: macOS packaging project that builds a `.app` bundle and a `.pkg` installer.
- `Teacher.Common`: shared DTOs and request contracts.

The current implementation focuses on visible and explicitly authorized administration tasks such as viewing processes and managing files. It does not include stealth monitoring, persistence tricks, hidden startup, or covert control flows.

## Project overview

### StudentAgent.Service

`StudentAgent.Service` is the new privileged Windows Service host for the same runtime API and policy engine. It is intended to own always-on background enforcement even when no teacher-visible tray UI is running.

### StudentAgent.UIHost

`StudentAgent.UIHost` is the user-session companion process for visible elements such as the tray icon, browser warnings, and fullscreen input-lock overlays. The service launcher is expected to keep it running inside the active student session.

Available endpoints:

- `GET /health`: unauthenticated health check.
- `GET /api/info`: machine and runtime information.
- `GET /api/processes`: list running processes.
- `POST /api/processes/kill`: terminate a process by PID.
- `GET /api/files/roots`: list logical drives.
- `GET /api/files/list`: list directory contents.
- `DELETE /api/files`: delete file or directory.
- `POST /api/files/directories`: create directory.
- `GET /api/files/download`: download file.
- `POST /api/files/upload`: upload file with `multipart/form-data`.
- `POST /api/commands/run`: execute a command script on the student machine.
- `GET /api/commands/frequent-programs/public-desktop`: collect `.lnk` shortcuts from the public desktop for teacher-side frequent program lists.

### TeacherClient

`TeacherClient` is a desktop UI for the teacher workstation. It provides:

- auto-discovery of agents on the local network over UDP;
- a combined `Agents` list with auto-discovered and manual entries;
- agent status tracking with `Online`, `Offline`, and `Unknown` states;
- agent filtering by search text, status, and group/class;
- auto-reconnect to the last connected agent when it becomes reachable again;
- manual agent definitions with saved IP, port, group/class, MAC address, and notes;
- connection to the student agent from the `Agents` list;
- persisted teacher-side settings with the shared secret stored outside the main window;
- a teacher-configured destination folder path used for bulk distribution on student PCs;
- teacher-configured student work folder settings with automatic shared-folder provisioning on reachable student PCs;
- user-selectable UI language with English and Ukrainian options;
- process list refresh and remote process termination;
- double-click process details with full metadata plus `Kill` and `Restart` actions;
- dual-pane local/remote file browsing;
- richer file listings with folder/file icons, file extensions, file attributes, and human-readable sizes;
- drive selectors for switching local and remote roots directly from the file panels;
- file upload and download;
- remote opening of the selected file or folder on the connected student PC;
- bulk distribution of a selected local file or folder to selected students or all online students;
- grouped destination-folder commands for clearing the configured student destination folder on either selected students or all online students;
- grouped remote command execution for either selected students or all online students, with support for multi-line command scripts and a `Current user` or `Administrator` run mode;
- a teacher-managed frequent programs list that can be refreshed from public desktop shortcuts gathered across online student PCs and then curated manually by the teacher;
- group commands for collecting student work folders from either selected students or all online students into teacher-side folders named after each student machine;
- a group browser-lock command for enabling browser blocking across all online student PCs;
- visible keyboard-and-mouse locking through an `Input lock` toggle per agent and bulk lock/unlock commands for online student PCs;
- grouped power commands for shutting down, restarting, or logging off either selected student PCs or all online student PCs;
- a splash screen shown during teacher client startup;
- remote directory creation;
- local and remote deletion with confirmation dialogs.

### TeacherClient.Avalonia

`TeacherClient.Avalonia` provides the same core workflow in a cross-platform desktop app:

- auto-discovery of agents on the local network over UDP;
- a combined `Agents` list with auto-discovered and manual entries;
- manual agent definitions with saved IP, port, group/class, MAC address, and notes;
- agent status tracking with `Online`, `Offline`, and `Unknown` states;
- filtering by search text, status, and group/class;
- auto-reconnect to the last connected agent;
- connect to the same `StudentAgent` endpoint from the `Agents` list;
- persisted teacher-side settings with the shared secret stored outside the main window;
- a teacher-configured destination folder path used for bulk distribution on student PCs;
- teacher-configured student work folder settings with automatic shared-folder provisioning on reachable student PCs;
- user-selectable UI language with English and Ukrainian options;
- browse remote processes and terminate a selected process;
- double-click process details with full metadata plus `Kill` and `Restart` actions;
- browse local and remote file trees in dual panes;
- richer file listings with folder/file icons, file extensions, file attributes, and human-readable sizes;
- drive selectors for switching local and remote roots directly from the file panels;
- upload and download files;
- remote opening of the selected file or folder on the connected student PC;
- bulk distribution of a selected local file or folder to selected students or all online students;
- grouped destination-folder commands for clearing the configured student destination folder on either selected students or all online students;
- grouped remote command execution for either selected students or all online students, with support for multi-line command scripts and a `Current user` or `Administrator` run mode;
- a teacher-managed frequent programs list that can be refreshed from public desktop shortcuts gathered across online student PCs and then curated manually by the teacher;
- group commands for collecting student work folders from either selected students or all online students into teacher-side folders named after each student machine;
- a group browser-lock command for enabling browser blocking across all online student PCs;
- visible keyboard-and-mouse locking through an `Input lock` toggle per agent and bulk lock/unlock commands for online student PCs;
- grouped power commands for shutting down, restarting, or logging off either selected student PCs or all online student PCs;
- a splash screen shown during teacher client startup;
- delete local and remote entries;
- create remote folders.

### Teacher.Common

`Teacher.Common` contains record types used by both applications for process, server, and file operations.

## Architecture notes

- Target framework: `.NET 8`
- UI: `Windows Forms`
- Agent style: `ASP.NET Core Minimal API`
- Transport: `HTTP`
- Authentication: shared secret header

The solution is Windows-oriented. `StudentAgent.Service`, `StudentAgent.UIHost`, and `TeacherClient` target `net8.0-windows`.

## Security and operational boundaries

This repository should be used only in an authorized classroom or lab environment.

Current constraints and risks:

- authentication is based on a shared secret, not per-user identity;
- transport is plain HTTP by default, so TLS should be added before wider use;
- file operations are not restricted to an approved directory subtree;
- there is no audit trail for teacher actions;
- there is no role model, session model, or credential rotation flow.

## Running the solution

### Prerequisites

- Windows machine for `TeacherClient`
- .NET 8 SDK
- Visual Studio 2022 or `dotnet` CLI

### macOS prerequisites for Avalonia

To build and run the Avalonia client on macOS, install:

- `.NET 8 SDK`
- an editor or IDE such as `JetBrains Rider` or `VS Code`
- optionally, Avalonia templates if you want to scaffold new apps yourself:

```bash
dotnet new install Avalonia.Templates
```

For this repository specifically, templates are optional. The checked-in project is enough to restore and build once the .NET SDK is installed.

### Start StudentAgent.Service

1. Open the solution in Visual Studio or use the CLI.
2. Review [StudentAgent.Service/appsettings.json](/Users/taras/Projects/OWN-GITHUB/teacher-server/StudentAgent.Service/appsettings.json) and [StudentAgent.UIHost/appsettings.json](/Users/taras/Projects/OWN-GITHUB/teacher-server/StudentAgent.UIHost/appsettings.json).
3. Change the default shared secret before use.
4. Publish the service bundle and install the service.
5. Ensure TCP port `5055` is reachable from the teacher machine.

`StudentAgent.Service` and `StudentAgent.UIHost` target `net8.0-windows`, so they should be built and run on Windows.

`StudentAgent.Service` listens for UDP discovery requests on port `5056` by default and responds with machine identity data that `TeacherClient` can use to build its agent list.

### Install StudentAgent.Service

1. Run [Publish-ServiceBundle.ps1](/Users/taras/Projects/OWN-GITHUB/teacher-server/StudentAgent.Service/Publish-ServiceBundle.ps1) on Windows to publish both `StudentAgent.Service` and `StudentAgent.UIHost` into a single folder.
2. Confirm that `StudentAgent.Service.exe` and `StudentAgent.UIHost.exe` are sitting beside each other in the publish folder.
3. Run [Install-Service.ps1](/Users/taras/Projects/OWN-GITHUB/teacher-server/StudentAgent.Service/Install-Service.ps1) from an elevated PowerShell window.
4. Optionally pass `-StartAfterInstall` to start the service immediately.
5. Use [Uninstall-Service.ps1](/Users/taras/Projects/OWN-GITHUB/teacher-server/StudentAgent.Service/Uninstall-Service.ps1) to remove it later.

This service bundle is the intended deployment path for Windows student machines.

### Build Windows MSI installer

1. On Windows, open an elevated PowerShell window in [TeacherServer.Setup](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherServer.Setup).
2. If PowerShell blocks script execution for the current session, run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
```

3. Build the MSI:

```powershell
.\Build-Msi.ps1
```

Or use the wrapper:

```cmd
Build-Msi.cmd
```

4. The script will:
   - publish a self-contained `TeacherClient` payload;
   - publish a self-contained `StudentAgent.Service` + `StudentAgent.UIHost` payload;
   - generate WiX payload fragments;
   - build an MSI into [TeacherServer.Setup/dist](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherServer.Setup/dist).

5. Run the generated `.msi` on Windows and choose the desired feature during setup:
   - `Teacher workstation tools`
   - `Student workstation tools`

When the `Student workstation tools` feature is selected, the installer deploys `StudentAgent.Service` and `StudentAgent.UIHost` together and registers the Windows service automatically.

Example configuration:

```json
{
  "Agent": {
    "Port": 5055,
    "SharedSecret": "replace-with-a-real-secret",
    "VisibleBannerText": "Teacher monitoring enabled"
  }
}
```

### Start TeacherClient

1. Launch `TeacherClient`.
2. Use the `Agents` tab to auto-discover agents or define manual entries.
3. Optionally assign manual agents to a `Group` such as a classroom, lab row, or lesson cohort.
4. Filter the list by search text, `Status`, or `Group`.
5. Leave `Auto-reconnect` enabled if you want the client to recover the last active connection automatically.
6. Open `Connection -> Settings` and choose the preferred UI language, the shared secret, the destination folder path for bulk distribution, and the student work base path plus work folder name.
7. Connect to a selected agent from the `Agents` list.
8. On the `Processes` tab, double-click a process to inspect full details and optionally `Kill` or `Restart` it.
9. In the `Files` tab, you can upload to the currently connected agent, download from it, or remotely open the selected file or folder on the student PC.
10. Once the work folder settings are saved, the client automatically attempts to create the shared student work folder on reachable student PCs and grant broad write access so students can save their work there.
11. When distributing a folder, the client recreates the selected folder and its full internal directory structure under the configured destination path on every target student machine.
12. Use `Group Commands -> Destination Folder` to clear the configured student destination folder on either the selected agents or all online agents. The folder itself remains in place.
13. Use `Group Commands -> Student Work -> Create work folder on all PCs` to provision the configured student work folder across all reachable student machines.
14. Use `Group Commands -> Student Work -> Collect student work to teacher PC` to gather each student's configured work folder into the current local teacher folder, inside a subfolder named after that student machine.
15. Use `Group Commands -> Student Work -> Clear work folder on all PCs` to empty the configured student work folder on all reachable student machines while leaving the folder itself in place.
16. Use the `Browser lock` checkbox in the agents list to enable or disable browser blocking for a specific online student PC. While enabled, the student agent checks for running browsers every minute, shows a visible warning for 10 seconds, and then force-closes browser processes that are still open.
17. Use `Group Commands -> Browser -> Lock browser on all online student PCs` to enable browser blocking on every reachable student machine at once.
18. Use the `Input lock` checkbox in the agents list to visibly lock or unlock the student's keyboard and mouse. While enabled, the student sees a fullscreen topmost message until the teacher removes the lock.
19. Use `Group Commands -> Keyboard and Mouse` to lock or unlock input on every reachable student machine at once.
20. Use `Group Commands -> Power` to shut down, restart, or log off either the selected student PCs or all online student PCs.
21. During bulk distribution, bulk clear, work collection, browser-lock, input-lock, and power operations, the status area reports the current target agent and progress.

### Start TeacherClient.Avalonia on macOS

1. Install the .NET 8 SDK on the Mac.
2. Restore packages:

```bash
dotnet restore TeacherClient.Avalonia/TeacherClient.Avalonia.csproj
```

3. Run the Avalonia client:

```bash
dotnet run --project TeacherClient.Avalonia/TeacherClient.Avalonia.csproj
```

4. In the app, open `Connection -> Settings` and choose the UI language, the shared secret, the destination folder path for bulk distribution, and the student work base path plus work folder name.
5. Use the `Agents` tab to discover students automatically, assign manual entries to groups, and connect from the filtered list.
6. Once the work folder settings are saved, the client automatically attempts to create the shared student work folder on reachable student PCs and grant broad write access so students can save their work there.
7. In the `Files` tab, select a local file or folder and either send it to the selected student agents or to all online student agents.
8. Folder distribution recreates the selected folder and its full internal structure under the configured destination path on each target student machine.
9. Use `Group Commands -> Destination Folder` to clear the configured student destination folder on either the selected agents or all online agents. The folder itself remains in place.
10. Use `Group Commands -> Student Work -> Create work folder on all PCs` to provision the configured student work folder across all reachable student machines.
11. Use `Group Commands -> Student Work -> Collect student work to teacher PC` to gather each student's configured work folder into the current local teacher folder, inside a subfolder named after that student machine.
12. Use `Group Commands -> Student Work -> Clear work folder on all PCs` to empty the configured student work folder on all reachable student machines while leaving the folder itself in place.
13. Use the `Browser lock` checkbox in the agents list to enable or disable browser blocking for a specific online student PC. While enabled, the student agent checks for running browsers every minute, shows a visible warning for 10 seconds, and then force-closes browser processes that are still open.
14. Use `Group Commands -> Browser -> Lock browser on all online student PCs` to enable browser blocking on every reachable student machine at once.
15. Use the `Input lock` checkbox in the agents list to visibly lock or unlock the student's keyboard and mouse. While enabled, the student sees a fullscreen topmost message until the teacher removes the lock.
16. Use `Group Commands -> Keyboard and Mouse` to lock or unlock input on every reachable student machine at once.
17. Use `Group Commands -> Power` to shut down, restart, or log off either the selected student PCs or all online student PCs.
18. During bulk distribution, bulk clear, work collection, browser-lock, input-lock, and power operations, the status area reports the current target agent and progress.

### Build macOS installer for TeacherClient.Avalonia

1. Open Terminal on macOS.
2. Run:

```bash
cd TeacherClient.Avalonia.Setup
bash ./Build-MacInstaller.sh
```

3. The setup project will:
   - publish a self-contained Avalonia build for `osx-arm64`;
   - assemble `Teacher Classroom Client.app`;
   - build a macOS installer package.

4. The outputs are:
   - app bundle: [TeacherClient.Avalonia.Setup/artifacts/Teacher Classroom Client.app](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia.Setup/artifacts/Teacher%20Classroom%20Client.app)
   - installer: [TeacherClient.Avalonia.Setup/dist/TeacherClassroomClient-macos.pkg](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia.Setup/dist/TeacherClassroomClient-macos.pkg)

5. Install the app by opening the generated `.pkg`.

To test from a Mac, make sure `StudentAgent.Service` is installed and running on a reachable Windows machine, then connect to it from the Avalonia client.

If you want a distributable build:

```bash
dotnet publish TeacherClient.Avalonia/TeacherClient.Avalonia.csproj -c Release -r osx-arm64 --self-contained true
```

For Intel Macs, use `osx-x64` instead of `osx-arm64`.

## Repository structure

```text
TeacherServer.sln
Teacher.Common/
StudentAgent.Shared/
StudentAgent.Service/
StudentAgent.UIHost/
TeacherServer.Setup/
TeacherClient/
TeacherClient.Avalonia/
TeacherClient.Avalonia.Setup/
AGENTS.md
CHANGELOG.md
README.md
```

## Development notes

- `GET /health` is intentionally left open for diagnostics.
- Swagger UI is available only in development mode.
- The student-side runtime currently sets `IsVisibleModeEnabled` to `true` in the info response.
- The repository is a good baseline for adding TLS, access control, audit logging, and folder restrictions.

## Recommended next steps

- add HTTPS and certificate handling;
- replace the shared secret with stronger authentication;
- restrict remote file operations to approved directories;
- add structured audit logs;
- add automated tests for API and client behaviors;
- tighten and polish the `StudentAgent.Service` + `StudentAgent.UIHost` deployment path after broader Windows classroom testing.
