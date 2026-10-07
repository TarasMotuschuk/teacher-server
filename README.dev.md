# ClassCommander

[![CI](https://github.com/TarasMotuschuk/teacher-server/actions/workflows/ci.yml/badge.svg)](https://github.com/TarasMotuschuk/teacher-server/actions/workflows/ci.yml)
[![Release All](https://github.com/TarasMotuschuk/teacher-server/actions/workflows/release-all.yml/badge.svg)](https://github.com/TarasMotuschuk/teacher-server/actions/workflows/release-all.yml)
[![Latest Release](https://img.shields.io/github/v/release/TarasMotuschuk/teacher-server?display_name=tag)](https://github.com/TarasMotuschuk/teacher-server/releases/latest)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20macOS%20%7C%20Linux-2EA44F)](#)

`ClassCommander` is a .NET 10 classroom administration solution for a transparent, teacher-controlled environment. It includes:

- `StudentAgent.Service`: Windows Service host for privileged agent runtime duties.
- `StudentAgent.UIHost`: Windows Forms session UI process for tray controls, warnings, and visible student overlays.
- `StudentAgent.VncHost`: session-aware VNC host process for visible remote screen viewing and control.
- `TeacherServer.Setup`: WiX-based MSI installer project for Windows deployment.
- `TeacherClient`: legacy maintenance-only Windows Forms application used by the teacher on Windows.
- `TeacherClient.Avalonia`: primary cross-platform desktop client for macOS, Linux, and Windows.
- `TeacherClient.Avalonia.Setup`: macOS packaging project that builds a `.app` bundle and a `.pkg` installer.
- `Teacher.Common`: shared DTOs and request contracts.
- `ClassCommander.TestPlatform` / `ClassCommander.TestEditor` / `ClassCommander.TestRunner` / `ClassCommander.Testing.Core`: classroom testing subsystem (local API, authoring, student runner). Teacher Avalonia **Testing → Tests and results…** uses Test Platform on the teacher PC (auto-started; students get the LAN address). See `TestPlatform/README.md`.

User-facing branding is now `ClassCommander`. Technical repository names such as `TeacherServer`, `TeacherClient`, and `TeacherClient.Avalonia` remain unchanged for compatibility with the existing solution structure, scripts, paths, and persisted settings.

The current implementation focuses on visible and explicitly authorized administration tasks such as viewing processes and managing files. It does not include stealth monitoring, persistence tricks, hidden startup, or covert control flows.

## Project overview

### StudentAgent.Service

`StudentAgent.Service` is the new privileged Windows Service host for the same runtime API and policy engine. It is intended to own always-on background enforcement even when no teacher-visible tray UI is running.

### StudentAgent.UIHost

`StudentAgent.UIHost` is the user-session companion process for visible elements such as the tray icon, browser warnings, fullscreen input-lock overlays, and the compact demonstration-mode banner used when a teacher wants to block input without covering the whole student screen. The service launcher is expected to keep it running inside the active student session. Choosing **Exit** from the tray menu prompts for the same StudentAgent administrator password used to open **Settings** and **Logs** from that menu.

### StudentAgent.VncHost

`StudentAgent.VncHost` is the session-aware host that keeps the classroom remote-management screen visible and controllable in the active student session. It is started and stopped by the student service when teacher-side VNC actions are requested.

Available endpoints:

- `GET /health`: unauthenticated health check.
- `GET /api/info`: machine and runtime information.
- `GET /api/processes`: list running processes.
- `POST /api/processes/kill`: terminate a process by PID.
- `GET /api/files/roots`: list logical drives.
- `GET /api/files/list`: list directory contents.
- `DELETE /api/files`: delete file or directory.
- `POST /api/files/directories`: create directory.
- `POST /api/files/rename`: rename file or directory within the current parent folder.
- `GET /api/files/download`: download file.
- `POST /api/files/upload`: upload file with `multipart/form-data`.
- `POST /api/commands/run`: execute a command script on the student machine.
- `GET /api/commands/frequent-programs/public-desktop`: collect `.lnk` shortcuts from the public desktop for teacher-side frequent program lists.
- `GET /api/desktop-icons/layouts`: list saved desktop icon layouts on the student machine.
- `GET /api/desktop-icons/layout`: read a specific saved desktop icon layout snapshot from the student machine.
- `POST /api/desktop-icons/save`: capture the current desktop icon positions from the active Windows session and save them as a named layout.
- `POST /api/desktop-icons/restore`: restore a previously saved desktop icon layout in the active Windows session.
- `POST /api/desktop-icons/apply`: store a provided desktop icon layout on the student machine and optionally restore it immediately.
- `GET /api/vnc/status`: read the current VNC host state on the student machine.
- `POST /api/vnc/start`: start the student-side VNC host in view-only or control mode.
- `POST /api/vnc/stop`: stop the student-side VNC host.
- `GET /api/registry/keys`: list registry subkeys at a given path; empty path returns the five root hives.
- `GET /api/registry/values`: list registry values at a given path with formatted type and data display.
- `GET /api/registry/export`: export the selected registry key subtree as a `.reg` file.
- `POST /api/registry/import`: import a `.reg` file and apply its key/value changes on the student machine.
- `GET /api/update/status`: read the current student-agent update state.
- `GET /api/update/check`: check the configured update manifest for a newer student-agent version.
- `POST /api/update/start`: download and start installing a newer student-agent version by launching `StudentAgent.Updater`.
- `POST /api/windows-restrictions`: enable or disable a named classroom policy-style restriction on the student PC (`WindowsRestrictionKind`, including Task Manager, Run dialog, Control Panel, lock workstation, change password, and block interface changes).
- `POST /api/windows-restrictions/desktop-wallpaper`: apply an enforced desktop wallpaper (full local path on the student PC, style 0–5) and lock out desktop background changes (Group Policy–equivalent registry values under `Policies\System`, mirrored to loaded user hives where applicable).

### TeacherClient

`TeacherClient` is the legacy Windows Forms desktop UI for the teacher workstation. It remains supported for maintenance work, packaging compatibility, and critical fixes, but new teacher-facing functionality should be implemented in `TeacherClient.Avalonia`. It provides:

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
- local file opening through the operating system's associated app, including double-click support for local files;
- file upload and download;
- local and remote file/folder rename actions from the `Files` tab;
- live transfer progress in the status area during single-file uploads/downloads and bulk file distribution or work collection;
- remote opening of the selected file or folder on the connected student PC;
- a `Remote management` tab with live student-PC screen tiles, a fullscreen viewer, and teacher-controlled VNC start/stop actions;
- bulk distribution of a selected local file or folder to selected students or all online students;
- grouped destination-folder commands for clearing the configured student destination folder on either selected students or all online students;
- grouped remote command execution for either selected students or all online students, with support for multi-line command scripts and a `Current user` or `Administrator` run mode;
- teacher-managed frequent-program actions grouped together with other group `Commands`, including refresh from public desktop shortcuts gathered across online student PCs and manual curation by the teacher;
- group commands for collecting student work folders from either selected students or all online students into teacher-side folders named after each student machine;
- a group browser-lock command for enabling browser blocking across all online student PCs;
- **Group Policies** group commands (Ukrainian UI: **Групові політики**) that push registry-based classroom policies to online student PCs via the agent service: Task Manager, Run dialog, Control Panel and Settings, lock workstation, change password, **block interface changes** (theme, colors, window style, desktop icons, mouse pointers, screen saver), and optional **desktop wallpaper** (upload to `C:\Windows\Web\Wallpaper`, then enforce wallpaper + prevent students from changing the background); hover **tooltips** on group-command menu items summarize each action;
- visible keyboard-and-mouse locking through an `Input lock` toggle per agent, bulk lock/unlock commands for online student PCs, and a demonstration-mode bulk lock that keeps the lock visible through a compact top banner instead of a fullscreen overlay;
- classroom demonstration (preview): start/stop a fullscreen student-side demonstration lock from `TeacherClient.Avalonia` (WebRTC signaling via the student service; teacher-side screen capture to be wired up next);
- teacher-side settings for desktop icon auto-restore interval and browser-lock check interval, with those policy values pushed to all online student PCs after saving and also synced opportunistically on connect;
- grouped power commands for shutting down, restarting, or logging off either selected student PCs or all online student PCs;
- desktop icon layout actions for the current connected student PC, including saving and restoring the student's own desktop icon arrangement;
- group desktop icon actions for restoring layouts on selected or all online student PCs, and for sending the current connected PC's icon layout to other student PCs;
- a splash screen shown during teacher client startup;
- remote directory creation;
- local and remote deletion with confirmation dialogs;
- a read-only remote registry viewer with a lazy-loaded key tree and a value list showing name, type, and data for the selected key.
- export of the selected remote registry key subtree to a `.reg` file and import of `.reg` files back to the connected student machine.
- teacher-side `Check for Updates...` preparation flow with a dedicated progress window, explicit error messages, and a separate `Download update` step before any student PCs are updated.
- teacher-side `Check for Client Updates...` flow that downloads the matching Windows MSI or macOS PKG for the teacher workstation and opens it with the system installer.
- bulk `Update selected PCs` and `Update all online PCs` actions from the group commands menu.
- preferred teacher-hosted update delivery: the teacher workstation caches the update bundle once and serves it over the LAN, with fallback to the configured remote manifest on the student agent.
- per-agent update badges with polling for `Available`, `Downloading`, `Installing`, `Updated`, `Failed`, and `Rolled back` states.

On the student machine, desktop icon auto-restore now runs from `StudentAgent.UIHost` on a timer using the locally saved default layout, mirroring the earlier `DesktopIconSaver` behavior. Browser-lock polling now also uses a configurable teacher-managed interval. A teacher can trigger restore manually, push the current connected PC's layout to other student PCs, and centrally change both timer values from the teacher-side settings window.

### TeacherClient.Avalonia

`TeacherClient.Avalonia` is the primary teacher client and provides the same core workflow in a cross-platform desktop app:

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
- user-selectable interface theme (dark or light), persisted alongside other teacher-side settings;
- browse remote processes and terminate a selected process;
- double-click process details with full metadata plus `Kill` and `Restart` actions;
- browse local and remote file trees in dual panes;
- richer file listings with folder/file icons, file extensions, file attributes, and human-readable sizes;
- drive selectors for switching local and remote roots directly from the file panels;
- upload and download files;
- a `Remote management` tab with live student-PC screen tiles, a fullscreen viewer, and teacher-controlled VNC start/stop actions;
- remote opening of the selected file or folder on the connected student PC;
- bulk distribution of a selected local file or folder to selected students or all online students;
- grouped destination-folder commands for clearing the configured student destination folder on either selected students or all online students;
- grouped remote command execution for either selected students or all online students, with support for multi-line command scripts and a `Current user` or `Administrator` run mode;
- teacher-managed frequent-program actions grouped together with other group `Commands`, including refresh from public desktop shortcuts gathered across online student PCs and manual curation by the teacher;
- group commands for collecting student work folders from either selected students or all online students into teacher-side folders named after each student machine;
- a group browser-lock command for enabling browser blocking across all online student PCs;
- **Group Policies** group commands (Ukrainian UI: **Групові політики**) matching the Windows client: policy restrictions, **block interface changes**, **desktop wallpaper** with background lock, and **tooltips** on group-command menu items;
- visible keyboard-and-mouse locking through an `Input lock` toggle per agent, bulk lock/unlock commands for online student PCs, and a demonstration-mode bulk lock that keeps the lock visible through a compact top banner instead of a fullscreen overlay;
- teacher-side settings for desktop icon auto-restore interval and browser-lock check interval, with those policy values pushed to all online student PCs after saving and also synced opportunistically on connect;
- grouped power commands for shutting down, restarting, or logging off either selected student PCs or all online student PCs;
- Wake-on-LAN **Power On** for selected student PCs or all known PCs that have a MAC address (teacher-side magic packets; does not require the agent to be online);
- desktop icon layout actions for the current connected student PC, including saving and restoring the student's own desktop icon arrangement;
- group desktop icon actions for restoring layouts on selected or all online student PCs, and for sending the current connected PC's icon layout to other student PCs;
- a splash screen shown during teacher client startup;
- delete local and remote entries;
- create remote folders;
- a read-only remote registry viewer with a lazy-loaded key tree and a value list showing name, type, and data for the selected key.
- export of the selected remote registry key subtree to a `.reg` file and import of `.reg` files back to the connected student machine.
- teacher-side `Check for Updates...` preparation flow with a dedicated progress window, explicit error messages, and a separate `Download update` step before any student PCs are updated.
- teacher-side `Check for Client Updates...` flow that downloads the matching Windows MSI or macOS PKG for the teacher workstation and opens it with the system installer.
- bulk `Update selected PCs` and `Update all online PCs` actions from the group commands menu.
- preferred teacher-hosted update delivery with fallback to the configured remote manifest on the student agent.
- per-agent update badges with polling for in-progress and rollback states.

On macOS, quitting the app after using remote-management VNC should tear down sessions safely: `CloseAsync` runs with the UI synchronization context cleared for that call so Avalonia does not deadlock, while VNC close/dispose still runs on the UI thread as required by the VNC client library.

### Teacher.Common

`Teacher.Common` contains record types used by both applications for process, server, and file operations.

## Architecture notes

- Target framework: `.NET 10`
- UI: `Windows Forms`
- Agent style: `ASP.NET Core Minimal API`
- Transport: `HTTP`
- Authentication: shared secret header

The solution is Windows-oriented. `StudentAgent.Service`, `StudentAgent.UIHost`, `StudentAgent.VncHost`, and `TeacherClient` target `net10.0-windows`.

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
- .NET 10 SDK
- Visual Studio 2022 or `dotnet` CLI

### macOS prerequisites for Avalonia

To build and run the Avalonia client on macOS, install:

- `.NET 10 SDK`
- an editor or IDE such as `JetBrains Rider` or `VS Code`
- optionally, Avalonia templates if you want to scaffold new apps yourself:

```bash
dotnet new install Avalonia.Templates
```

For this repository specifically, templates are optional. The checked-in project is enough to restore and build once the .NET SDK is installed.

### Start StudentAgent.Service

1. Open the solution in Visual Studio or use the CLI.
2. Review [StudentAgent.Service/appsettings.json](StudentAgent.Service/appsettings.json) and [StudentAgent.UIHost/appsettings.json](StudentAgent.UIHost/appsettings.json).
3. Change the default shared secret before use.
4. Publish the service bundle and install the service.
5. Ensure TCP port `5055` is reachable from the teacher machine.

`StudentAgent.Service` and `StudentAgent.UIHost` target `net10.0-windows`, so they should be built and run on Windows.

`StudentAgent.Service` listens for UDP discovery requests on port `5056` by default and responds with machine identity data that `TeacherClient` can use to build its agent list.

### Install StudentAgent.Service

1. Run [Publish-ServiceBundle.ps1](StudentAgent.Service/Publish-ServiceBundle.ps1) on Windows to publish both `StudentAgent.Service` and `StudentAgent.UIHost` into a single folder.
2. Confirm that `StudentAgent.Service.exe` and `StudentAgent.UIHost.exe` are sitting beside each other in the publish folder.
3. Run [Install-Service.ps1](StudentAgent.Service/Install-Service.ps1) from an elevated PowerShell window.
4. Optionally pass `-StartAfterInstall` to start the service immediately.
5. Use [Uninstall-Service.ps1](StudentAgent.Service/Uninstall-Service.ps1) to remove it later.

This service bundle is the intended deployment path for Windows student machines.

### Build Windows MSI installer

1. On Windows, open an elevated PowerShell window in [TeacherServer.Setup](TeacherServer.Setup).
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
   - publish a self-contained `TeacherClient.Avalonia` payload;
   - publish self-contained `ClassCommander.TestEditor` and `ClassCommander.TestPlatform` payloads;
   - publish the self-contained student bundle (`StudentAgent.Service`, `StudentAgent.UIHost`, `StudentAgent.VncHost`, `StudentAgent.Updater`, and `ClassCommander.TestRunner`);
   - generate WiX payload fragments;
   - build `ClassCommander.Setup.msi` into [TeacherServer.Setup/dist](TeacherServer.Setup/dist).

5. Run the generated `ClassCommander.Setup.msi` on Windows. The installer is branded as `ClassCommander`, and then choose the desired feature during setup:
   - `Teacher workstation tools`
   - `Student workstation tools`

The installed layout under `C:\Program Files\MTD\TeacherServer` is:

- `TeacherAvalonia\` — the Avalonia teacher client (teacher feature; desktop/Start Menu shortcuts named `ClassCommander`).
- `TestEditor\` — `ClassCommander.TestEditor` test authoring app (teacher feature; Start Menu shortcut `ClassCommander Test Editor`, also launched from the teacher client via **Testing → Test Editor…**).
- `TestPlatform\` — `ClassCommander.TestPlatform` local test server (teacher feature; listens on `http://0.0.0.0:5050` by default, installed with a firewall exception so students can reach it). **Testing → Tests and results…** starts it on the teacher PC automatically.
- `Student\` — student agent binaries (student feature).
- `Student\TestRunner\` — `ClassCommander.TestRunner` student test app (student feature). Teacher-launched tests prefer this installed runner and only fall back to the copy deployed to `C:\Users\Public\ClassCommander\TestRunner`; the **Install student app** action in the Testing window skips PCs that already have the installed runner.

When the `Student workstation tools` feature is selected, the installer deploys `StudentAgent.Service`, `StudentAgent.UIHost`, `StudentAgent.VncHost`, and `ClassCommander.TestRunner` together and registers the Windows service automatically. Because `ClassCommander.TestRunner` ships inside the student bundle, the student-agent auto-update ZIP also carries it and keeps the installed runner current.

Reinstalling the **same** MSI package version upgrades the existing installation in place so *Apps & features* / *Programs and Features* keeps a single ClassCommander entry. On uninstall, stopping the service shuts down the session `StudentAgent.UIHost` and `StudentAgent.VncHost` processes; the installer also attempts to terminate those executables if they are still running so files can be removed cleanly.

Example configuration:

```json
{
  "Agent": {
    "Port": 5055,
    "SharedSecret": "replace-with-a-real-secret",
    "VisibleBannerText": "Teacher monitoring enabled",
    "UpdateManifestUrl": "https://example.com/student-agent/version.json"
  }
}
```

`UpdateManifestUrl` is optional. When configured, `StudentAgent.Service` can check a release manifest, download a ZIP update payload, verify its SHA-256 checksum when present, and hand off installation to `StudentAgent.Updater`.

The teacher update flow is now explicit:

1. Open `Check for Updates...` in either teacher client.
2. Run `Check updates` to validate the manifest source and see any GitHub/network errors directly in the progress window.
3. Run `Download update` to cache the ZIP on the teacher workstation.
4. Only after that, run `Update selected PCs` or `Update all online PCs`.

The teacher workstation serves the prepared ZIP to student agents over the local network. If the teacher workstation has no internet access, you can manually place `student-agent-version.json` plus the matching `student-agent-update-<version>.zip` into the teacher-side manual update folder shown in the preparation window.

For GitHub-based releases, this repository can publish a student-agent update bundle on tag push. The auto-update manifest is emitted as `student-agent-version.json` in the GitHub Release assets and points to the matching `student-agent-update-<version>.zip`.

Teacher workstations can also check for a newer client installer directly from GitHub Release assets:

1. Open `Help -> Check for Client Updates...`.
2. Run `Check updates` to validate that a matching installer asset exists for the current platform.
3. Run `Download installer` to cache the MSI or PKG locally.
4. Run `Install update` to open the downloaded installer with the operating system.

The client update manifest is emitted as `classcommander-client-version.json` in GitHub Release assets and points to `ClassCommander.Setup.msi` and `ClassCommander.Setup.pkg` for the current version.

Tag-based GitHub releases now publish all major install/update assets together:

- `student-agent-update-<version>.zip`
- `student-agent-version.json`
- Windows `.msi`
- macOS `.pkg`
- `classcommander-client-version.json`

### Start ClassCommander on Windows

1. Launch `ClassCommander` from the installed Avalonia shortcut, or run `TeacherClient.exe` directly only when you specifically need the legacy WinForms client.
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
20. Use `Group Commands -> Power` to shut down, restart, or log off either the selected student PCs or all online student PCs. Use **Power on all known PCs on the network** to send Wake-on-LAN to every student PC this teacher has already seen (MAC + last IP are remembered even after the PC powers off). PCs must have WoL enabled in BIOS/NIC. A PC that has never been discovered cannot be woken until it appears online once.
21. Use `Group Commands -> Group Policies` to enable or disable classroom policy-style restrictions (Task Manager, Run, Control Panel, lock workstation, change password), **Block interface changes**, or **Desktop wallpaper** (the client uploads the image to each student PC under `C:\Windows\Web\Wallpaper`, then applies wallpaper + background lock). Hover menu items to read short descriptions.
22. During bulk distribution, bulk clear, work collection, browser-lock, input-lock, power, and group-policy operations, the status area reports the current target agent and progress.

### Start ClassCommander on macOS

1. Install the .NET 10 SDK on the Mac.
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
17. Use `Group Commands -> Power` to shut down, restart, or log off either the selected student PCs or all online student PCs. Use **Power on all known PCs on the network** to send Wake-on-LAN to remembered student PCs (including those currently powered off). WoL must be enabled in BIOS/NIC; a PC must have been seen online at least once so its MAC is stored.
18. Use `Group Commands -> Group Policies` for the same policy, interface-lock, and desktop-wallpaper actions as on Windows (see the Windows quick-start steps above). Hover menu items for tooltips.
19. During bulk distribution, bulk clear, work collection, browser-lock, input-lock, power, and group-policy operations, the status area reports the current target agent and progress.

### Build macOS installer for ClassCommander

1. Open Terminal on macOS.
2. Run:

```bash
cd TeacherClient.Avalonia.Setup
bash ./Build-MacInstaller.sh
```

3. The setup project will:
   - publish a self-contained Avalonia build for `osx-arm64`;
   - publish self-contained `ClassCommander.TestEditor` and `ClassCommander.TestPlatform` builds and place them inside the bundle at `Contents/MacOS/TestEditor` and `Contents/MacOS/TestPlatform`;
   - assemble `ClassCommander.app`;
   - build a macOS installer package.

   The teacher client launches the bundled Test Editor via **Testing → Test Editor…**. **Testing → Tests and results…** starts the bundled Test Platform automatically (`Contents/MacOS/TestPlatform`; listens on `http://0.0.0.0:5050` by default).

   **Demonstration (WebRTC) codec note**:
   - On **macOS**, the teacher client encodes demo video as **H.264 via VideoToolbox** (system framework; no extra `vpxmd.dylib` bundling).
   - Student rendering on **Windows** decodes **H.264 via Media Foundation**.
   - The teacher captures and encodes the screen **once per session**; the encoded stream is fanned out to every connected student peer, and students connect in parallel. A keyframe is forced whenever a new student peer connects.

4. The outputs are:
   - app bundle: [TeacherClient.Avalonia.Setup/artifacts/ClassCommander.app](TeacherClient.Avalonia.Setup/artifacts/ClassCommander.app)
   - installer: [TeacherClient.Avalonia.Setup/dist/ClassCommander.Setup.pkg](TeacherClient.Avalonia.Setup/dist/ClassCommander.Setup.pkg)

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


## Classroom testing workflow and verification

Use the updated teacher client **and updated student TestRunner** for this workflow. In **Testing → Tests and results…**, import a test, choose **Assign test**, select the resulting assignment in the selector above the tabs, then choose **Launch → Choose students and start…**. The dialog lists online PCs with checkboxes and a selected count. **Launch → Start on all online** opens the same confirmation list with every online PC selected. The Testing window’s **Tools** menu contains **Install student app…** and **Teacher exit codes…**. The local Test Platform starts without a Windows console (`UseShellExecute=false`, `CreateNoWindow=true`); its connection status remains visible in the teacher UI.

Every student launch opens a light-themed sign-in screen with empty surname and first-name fields. Legacy `--auto-continue`, machine-name identity, and saved identity never bypass this screen. After identity confirmation, `--assignment-id` opens the intended assignment directly and filters other assignments out of the fallback list.

Each launch generates a six-digit teacher exit code. **Teacher exit codes…** shows the assignment, launch time, PCs and code, including after reopening the client. Codes are stored in the teacher user's local app data at `TeacherServer/TeacherClient.Avalonia/test-exit-codes.json`; keep this file accessible only to the teacher. Only the SHA-256 verifier is sent with `--exit-code-hash` to TestRunner. This is a classroom focus control, not an OS security or exam-integrity boundary.

Once an attempt begins, the updated runner shows a visible fullscreen restriction banner and **Exit test…**. Closing or Alt+F4 opens the teacher-code prompt. A correct code saves current answers before exiting; if the server cannot save them, the teacher explicitly confirms exit without the latest changes. Successful submission releases fullscreen, topmost, and keyboard restrictions. Native Windows hooks block Win, Alt+Tab, Alt+Esc, Ctrl+Esc and Ctrl+Shift+Esc while the attempt is active; ordinary answer-entry shortcuts keep working. Ctrl+Alt+Del, OS shutdown and administrative recovery remain available. No system policies or persistent locks are installed. Old launches without an exit-code verifier retain normal window behavior; native shortcut filtering is Windows-only.

Run the headless UI regression checks with:

```sh
dotnet run --project tests/ClassCommander.TestRunner.SmokeTests
```

The checks cover English/Ukrainian sign-in rendering, readable light styling, legacy auto-start prevention, assignment isolation, and (on non-Windows test hosts) the close/PIN and completion cleanup paths. They save screenshots under the temporary directory `classcommander-testing-ux-smoke`. Native Windows hooks are deliberately excluded from headless execution.

Windows classroom verification still required: dark/light Windows themes at 100–200% DPI; one-PC and multi-PC launches; absence of a server console; correct student names in results; Alt+Tab/Win/minimize/Alt+F4 during a test; correct/incorrect exit code; successful submit; disconnected-server early exit; keyboard restoration after exit, process termination and OS sign-out. Multiple-monitor focus behavior should be checked on actual classroom hardware.


### Viewing classroom test results

**Testing → Tests and results… → Monitoring** follows the launch assignment automatically and opens after a successful launch. It fetches attempts and results immediately and every three seconds while the tab is visible. The monitor selector also includes closed assignments, independently of the launch selector. Result rows include student names; select a row and use **Attempt details** for the answer breakdown. Empty counts mean no records were returned; refresh failures display a retry message without discarding the last successful data. Closing the window stops polling. Regression checks exercise this workflow against a local HTTP fixture in the headless smoke suite.

### Remote commands and frequent programs

In the Avalonia teacher client, **Group Commands → Commands → Run command on selected PCs** uses the checkboxes in the **Select** column and excludes offline PCs. **Run command on all online PCs** uses all discovered online PCs. Empty target lists display an explanatory dialog. Launch failures include the agent's error message; success means the script process started, not that every command completed successfully.

**Manage frequent programs** supports add, edit (also double-click), delete and confirmed clear. Editing preserves the entry ID and changes the name, command and run mode. Labels are localized. Changes are saved when the manager closes, including via the window close button. Refreshing desktop shortcuts preserves existing command entries and their customized run mode.

`Current user` starts in the signed-in console user's context. `Administrator` on the updated Windows agent duplicates the installed service's own **LocalSystem** token, assigns the active console session and launches on `winsta0\default` with `CreateProcessAsUser`. This uses existing service privileges; it does not disable UAC, request student credentials or change UAC policy. GUI programs are visible in that session. It requires the service to run as LocalSystem and a signed-in console user. The SYSTEM profile/environment differs from the student's profile, including HKCU and network access. The command wrapper has no console window; UTF-8 command text is selected explicitly.

Command files are stored separately in `%ProgramData%\TeacherServer.RemoteCommands`, with a protected DACL: SYSTEM/Administrators can write; local users can read/execute and delete individual files for wrapper cleanup but cannot modify scripts or create replacement files. The directory cannot be a reparse point. Failed launches remove their scripts. Agent logging records launch mode and script path. Manual Windows verification is still required for visible application launch in both modes, absence of UAC in service mode, standard-user ACL denial, Ukrainian paths, locked/unlocked sessions and handle cleanup. The automated headless suite covers checkbox target selection, offline filtering, editing/run mode, preservation of entry identity and cancel/confirm clearing.

### MyTestX 10.2 migration

Test Editor → File → **Batch convert MyTestX…** accepts multiple XML exports and a destination folder. Each successful input becomes a `.cctest` with its source XML embedded; existing packages receive a numbered suffix instead of being overwritten. Conversion runs outside the UI thread, supports cancellation between/within packaging operations, and saves a text report in the destination folder. Completed packages remain after cancellation. Temporary workspaces are isolated per input and cleaned on completion/failure. The currently open editor test is unaffected by batch conversion; a failed single-file import also preserves it.

Single-file import and batch conversion read unprotected MyTestX **10.2.0.2** `.mtf` files natively in .NET on Windows, macOS and Linux. No Mtf2Xml executable, Wine, Rosetta or Python runtime is required, and import performs no downloads. The implementation ships with the editor in normal MSI/macOS packaging. The original MTF is embedded in the resulting package. Other MTF versions and password-protected tests are rejected with XML-export guidance.

The native reader decrypts the XCrypt feedback stream, validates the exact header/version, inflates zlib with a 256 MiB limit and reads the binary metadata, question records and embedded asset table sequentially. Counts, strings, references and complete consumption of the body are checked. Numeric ranges and regex keys are rejected when they cannot be represented faithfully. Format research attribution and its MIT license are in `THIRD-PARTY-NOTICES.md`. Compatibility validation uses the supplied real **402-question, 44-group, 70-image-reference** MTF and an independently generated official XML export; Windows UI runtime verification remains pending.

The importer honors declared XML encoding (including Windows-1251), recovers plain question/answer text from RTF, supports separate matching columns and MyTestX numeric/text answer nodes, and preserves image-region coordinates. XML input is limited to 512 MiB; DTDs/external entities are prohibited. Empty/unrecognized structures, unknown question types, numeric tolerances, regular-expression text keys and missing/ambiguous answer keys are rejected when they cannot be represented faithfully. Batch conversion refuses inputs with importer warnings instead of publishing a possibly incomplete package. Single-file XML import displays warnings for review. Rich formatting, instructions, passwords, grading scales and special testing modes are not fully transferred. Review imported questions and configure grading/assignment policy before use.


### Editing image-area questions

In Test Editor, choose **Image area**, use **Choose image…** (PNG, JPEG, WebP or BMP), then hold the left mouse button and drag a rectangle around the correct element, such as a toolbar tab. Every student click inside the blue rectangle, including its boundary, is accepted. A new drag replaces the previous area; a click without a rectangle preserves it. Coordinates use original image pixels, independent of display scaling. **Point with allowed deviation** retains the earlier point workflow; **Allowed deviation (pixels)** sets the square around that point. Existing imported polygon keys remain unchanged until the teacher draws a new area, selects a new point or changes the point tolerance. Save embeds the image in `.cctest` without resizing it. Questions without an image or answer region must be completed before saving.

The updated student runner downloads images from `GET /api/tests/v1/student/attempts/{attemptId}/assets/{assetId}` using `X-Attempt-Token`. The endpoint only serves image assets from that attempt’s test version. Students select their answer directly on the image; navigation and saved progress preserve the selected point. Scoring includes polygon boundaries and supports older single-point keys. This workflow requires updated Test Platform and TestRunner components.

### Protection against locking the teacher PC

Both teacher clients exclude their own computer from individual and group **input locking** (fullscreen overlay or demonstration banner) and **browser locking**. Detection uses loopback, local interface addresses (including IPv4-mapped IPv6), the computer name reported by discovered agents, and DNS resolution of manually configured hostnames. The confirmation and completion text report excluded recipients. The API clients repeat the endpoint check before sending an enabling request; disabling requests remain allowed so an existing lock can be removed. Other commands, such as power actions and Windows policy restrictions, are outside this protection.

### Test Editor workspace

The editor uses a compact menu/toolbar and resizable question list on the left. Questions have human-readable numbers instead of technical IDs. The **Main** tab contains the prompt; **Additional** contains the hint, score and required flag. Answers appear below, with multiline choice fields that use the available width. **Questions** provides add, duplicate, delete, move, save and reset actions; adding an answer preserves current edits. Selection changes apply edits automatically; **Reset changes** restores the last applied question.

**Test parameters → Title and description…** edits title, description, author and email. **Group editor…** adds, renames and deletes groups; deleting a group transfers its questions to the first remaining group. The last group cannot be deleted. Use the group selector above the answer area to move a question. Ctrl/Cmd+N, O and S invoke new/open/save; F2 opens title and description. MyTestX-style testing modes, rich-text formatting and extra restrictions are not implemented by this workspace update.

### Student testing flow

- Teacher launches with an assignment ID load `GET /api/tests/v1/student/assignments/{assignmentId}/overview` before sign-in. This returns only published/available assignment metadata, counts, time limit and result policy; it never creates an attempt or exposes question text/answer keys. Manual launches still sign in first and show the overview of the selected assignment.
- The runner asks for a fresh surname and first name, then starts/resumes the chosen attempt. Questions have fixed navigation, full-width choice rows, skip/back actions, answered progress, elapsed/remaining time and text/image zoom. The timer uses the server-provided original attempt start time. At the configured deadline the runner disables answer editing and attempts submission, retrying after connection failure. Deadline submission is client-side; server-side deadline enforcement is not introduced by this UI change.
- A result screen replaces the answer controls only after a successful submit response. It shows delivery confirmation and duration; scores, supplied grades and per-question correctness remain subject to `ResultPolicyDto`. The scoring engine currently returns no grade unless one is supplied; no default grading scale is invented. Done clears student identity. Failed submissions keep the current answers and offer normal submission retry; fullscreen restrictions remain active until confirmation or teacher-authorized exit.
- Progress and submit requests accept optional `replaceAnswers` (default `false` for older clients). The new runner sends `true` to persist its complete answer snapshot, including intentional removal of a previously saved answer.
- UI smoke coverage: `dotnet run --project tests/ClassCommander.TestRunner.SmokeTests` covers preflight/identity ordering without attempt creation, actual row clicks, navigation, zoom, timer, failed submission/retry, result policy and cleanup in both languages. Windows focus/keyboard restrictions still need runtime verification on Windows.
