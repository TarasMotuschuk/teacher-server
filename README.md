# Teacher Server

`Teacher Server` is a .NET 8 classroom administration solution for a transparent, teacher-controlled environment. It includes:

- `StudentAgent`: ASP.NET Core agent running on the student workstation.
- `TeacherClient`: Windows Forms application used by the teacher.
- `TeacherClient.Avalonia`: cross-platform desktop client for macOS, Linux, and Windows.
- `Teacher.Common`: shared DTOs and request contracts.

The current implementation focuses on visible and explicitly authorized administration tasks such as viewing processes and managing files. It does not include stealth monitoring, persistence tricks, hidden startup, or covert control flows.

## Project overview

### StudentAgent

`StudentAgent` exposes a small HTTP API secured with the `X-Teacher-Secret` header.

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
- dual-pane local/remote file browsing;
- richer file listings with folder/file icons, file extensions, file attributes, and human-readable sizes;
- file upload and download;
- bulk distribution of a selected local file or folder to selected students or all online students;
- grouped destination-folder commands for clearing the configured student destination folder on either selected students or all online students;
- group commands for collecting student work folders from either selected students or all online students into teacher-side folders named after each student machine;
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
- browse local and remote file trees in dual panes;
- richer file listings with folder/file icons, file extensions, file attributes, and human-readable sizes;
- upload and download files;
- bulk distribution of a selected local file or folder to selected students or all online students;
- grouped destination-folder commands for clearing the configured student destination folder on either selected students or all online students;
- group commands for collecting student work folders from either selected students or all online students into teacher-side folders named after each student machine;
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

The solution is Windows-oriented. Both `StudentAgent` and `TeacherClient` target `net8.0-windows`.

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

### Start StudentAgent

1. Open the solution in Visual Studio or use the CLI.
2. Review `StudentAgent/appsettings.json`.
3. Change the default shared secret before use.
4. Start the agent.
5. Ensure TCP port `5055` is reachable from the teacher machine.

`StudentAgent` currently targets `net8.0-windows`, so it should be built and run on Windows.

`StudentAgent` also listens for UDP discovery requests on port `5056` by default and responds with machine identity data that `TeacherClient` can use to build its agent list.

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
8. In the `Files` tab, you can still upload to the currently connected agent, or distribute a selected local file/folder to either the selected student agents or all online student agents.
9. Once the work folder settings are saved, the client automatically attempts to create the shared student work folder on reachable student PCs and grant broad write access so students can save their work there.
10. When distributing a folder, the client recreates the selected folder and its full internal directory structure under the configured destination path on every target student machine.
11. Use `Group Commands -> Destination Folder` to clear the configured student destination folder on either the selected agents or all online agents. The folder itself remains in place.
12. Use `Group Commands -> Student Work -> Create work folder on all PCs` to provision the configured student work folder across all reachable student machines.
13. Use `Group Commands -> Student Work -> Collect student work to teacher PC` to gather each student's configured work folder into the current local teacher folder, inside a subfolder named after that student machine.
14. Use `Group Commands -> Student Work -> Clear work folder on all PCs` to empty the configured student work folder on all reachable student machines while leaving the folder itself in place.
15. Use the `Browser lock` checkbox in the agents list to enable or disable browser blocking for a specific online student PC. While enabled, the student agent checks for running browsers every minute, shows a visible warning for 10 seconds, and then force-closes browser processes that are still open.
13. During bulk distribution, bulk clear, and work collection operations, the status area reports the current target agent and progress.

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
11. During bulk distribution, bulk clear, and work collection operations, the status area reports the current target agent and progress.

To test from a Mac, run `StudentAgent` on a reachable Windows machine first, then connect to it from the Avalonia client.

If you want a distributable build:

```bash
dotnet publish TeacherClient.Avalonia/TeacherClient.Avalonia.csproj -c Release -r osx-arm64 --self-contained true
```

For Intel Macs, use `osx-x64` instead of `osx-arm64`.

## Repository structure

```text
TeacherServer.sln
Teacher.Common/
StudentAgent/
TeacherClient/
TeacherClient.Avalonia/
AGENTS.md
CHANGELOG.md
README.md
```

## Development notes

- `GET /health` is intentionally left open for diagnostics.
- Swagger UI is available only in development mode.
- `StudentAgent` currently sets `IsVisibleModeEnabled` to `true` in the info response.
- The repository is a good baseline for adding TLS, access control, audit logging, and folder restrictions.

## Recommended next steps

- add HTTPS and certificate handling;
- replace the shared secret with stronger authentication;
- restrict remote file operations to approved directories;
- add structured audit logs;
- add automated tests for API and client behaviors;
- decide whether `StudentAgent` should remain an app process or become a managed Windows service with a visible presence.
