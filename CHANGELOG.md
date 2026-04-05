# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog, and this project currently starts with an initial baseline release.

## [Unreleased]

### Added

- `StudentAgent.UIHost`: tray menu **Exit** now prompts for the same StudentAgent administrator password as **Settings** and **Logs**
- Teacher-side remote management tab in both clients with live student-PC screen tiles, a fullscreen VNC viewer, and visible start/stop actions for the selected student PC
- Student-side VNC hosting support through `StudentAgent.VncHost` plus teacher-controlled VNC status/start/stop endpoints
- Desktop icon layout integration from the student desktop session: the student service now exposes save/restore endpoints that capture the current Windows desktop icon arrangement and restore it later through `StudentAgent.UIHost`
- Teacher clients now offer `Save desktop icon layout` and `Restore desktop icon layout` actions for the current connected student PC
- Teacher clients now offer group desktop-icon actions to restore layouts on selected or all online student PCs
- Teacher clients can now capture the current connected student PC's icon layout and apply that layout to other student PCs
- Teacher-side settings now include desktop icon auto-restore and browser-lock check intervals, and those policy values are pushed to all online student PCs after saving

### Changed

- `StudentAgent.UIHost` now performs mandatory silent desktop-icon auto-restore on a timer using the locally saved default layout, matching the old `DesktopIconSaver` model
- Browser-lock enforcement timers on the student side now use the centrally managed teacher-side interval instead of a hardcoded one-minute check

### Fixed

- `TeacherClient.Avalonia` (macOS): closing VNC sessions no longer runs synchronous async waits on the UI thread, avoiding hangs or watchdog termination when quitting after remote-management viewing
- Windows MSI: `MajorUpgrade` with same-version upgrades so reinstalling the same package version replaces the existing entry in *Apps & features* instead of creating duplicate listings
- Windows: stopping `StudentAgent.Service` now terminates session `StudentAgent.UIHost` and `StudentAgent.VncHost` processes (they are not child processes of the service); the MSI also registers util `CloseApplication` for both EXEs as a fallback during uninstall/repair so files are not locked by orphan processes
- Student-agent updater now stops the session `StudentAgent.UIHost` and retries locked file copies so updates no longer fail just because `Accessibility.dll` or another UI-hosted file is still in use
- Group and single `Log Off` commands now target the active Windows student session correctly instead of relying on the service-session `shutdown.exe /l` path

## [1.0.11] - 2026-04-04

### Added

- Teacher-client update flow in both WinForms and Avalonia: check for the latest installer, download the matching MSI or PKG for the current platform, and open it via the system installer
- Unified tag-based GitHub release workflow that publishes student-agent update assets together with the Windows MSI, macOS PKG, and a dedicated `classcommander-client-version.json` manifest for teacher-client updates
- Centralized `Branding/` graphics map with a dedicated `GRAPHICS_MAP.md` that documents the shared app icon, splash, toolbar icon, and background asset paths

### Changed

- Update-related actions in both teacher clients are now grouped more clearly under program update flows, and the main `Agents` tab is now presented as `Учнівські ПК` / `Student PCs`
- WinForms, Avalonia, student tray UI, MSI, and macOS packaging now read core branding assets from the shared `Branding/` structure instead of scattered per-project locations
- Teacher and student About windows, student lock forms, and both teacher-client splash screens now use the centralized branding graphics layout

## [1.0.10] - 2026-04-03

### Changed

- Teacher-side student-agent updates are now an explicit two-step flow in both teacher clients: first check and prepare the update in a dedicated progress window, then separately start deployment to selected or all online student PCs
- Update preparation progress in WinForms and Avalonia now uses a cleaner progress bar with compact transfer details instead of flooding the log with per-chunk download messages

### Added

- New teacher-side update preparation windows in WinForms and Avalonia with visible progress, download status, and direct error messages for manifest/download failures
- Manual offline preparation guidance in the update preparation window, including the local folder path where a teacher can place `student-agent-version.json` and a ZIP package by hand

## [1.0.9] - 2026-04-02

### Added

- Local file entries in both teacher clients can now be opened with the operating system's associated application directly from the `Files` tab
- Local and remote file/folder rename actions are now available in both teacher clients
- `POST /api/files/rename` endpoint on StudentAgent.Service for remote file and folder renaming
- File upload, download, distribution, and work-collection flows in both teacher clients now show live transfer progress in the status area
- StudentAgent.Service now exposes drive free-space information so both teacher clients can display remaining space for the currently selected student drive

### Fixed

- Registry `.reg` import now tolerates UTF-16 files without BOM and strips embedded NUL characters more safely
- Registry import errors now surface the actual agent-side message in both teacher clients instead of a generic `400 Bad Request`
- Agent tables in both teacher clients now avoid showing the machine-account form like `PCNAME$` as the connected user, and status summaries now include the currently connected machine name
- The `Files` tab in both teacher clients now shows an `↑ Вгору` navigation label and current free disk space in the drive/path row for both local and remote panels

## [1.0.8] - 2026-04-02

### Changed

- Rebranded the user-facing product name from `Teacher Server` / `Teacher Classroom Client` to `ClassCommander` while keeping the existing technical project, solution, and path names unchanged for compatibility
- WinForms and Avalonia splash screens now display the new `ClassCommander` branded splash image instead of the previous generated startup layout
- WinForms, Avalonia, macOS packaging, and Windows installer assets now use the new `ClassCommander` application icon

### Fixed

- macOS downloads from student PCs now correctly strip Windows path segments and save using only the file name
- StudentAgent installer/publish dependency versions are aligned so the Windows service no longer fails to start because of mixed `System.Diagnostics.EventLog` / `ServiceController` runtime assemblies
- Avalonia file toolbar layout is more compact and no longer consumes as much vertical space on the `Files` tab

## [1.0.7] - 2026-04-01

### Added

- Phase 1 student-agent update pipeline: `StudentAgent.Updater`, update status/check/start endpoints, and manual teacher-side update commands for a selected online agent
- Phase 2 teacher-side bulk update actions: start agent updates on selected student PCs or all online student PCs from both teacher clients
- `StudentAgent.Service` can now read a JSON update manifest, download a ZIP payload, verify SHA-256 when provided, and launch the updater against the installed service directory
- `Publish-ServiceBundle.ps1` now publishes `StudentAgent.Updater` beside the service and UIHost binaries so installed student bundles are update-ready
- Preferred teacher-hosted update delivery in both teacher clients: the teacher workstation now caches the student update ZIP once and serves it to student agents over the local network, with fallback to the configured remote manifest when needed
- Per-agent update status polling in both teacher clients, including update badges for `Available`, `Downloading`, `Installing`, `Updated`, `Failed`, and `Rolled back`

### Changed

- Student-agent updater now writes persisted update status snapshots so post-restart success and rollback states are visible to teacher workstations

## [1.0.6] - 2026-04-01

### Added

- Remote registry viewer tab in both TeacherClient (WinForms) and TeacherClient.Avalonia: browse the full registry tree of a connected student machine with lazy-loaded subkeys and a value list showing Name, Type, and Data columns
- Registry editing support: create, edit, and delete registry values (REG_SZ, REG_DWORD, REG_QWORD, REG_EXPAND_SZ, REG_MULTI_SZ, REG_BINARY) and create/delete registry keys in both teacher clients
- Registry `.reg` export and import support in both teacher clients for the selected remote key
- `GET /api/registry/keys`, `GET /api/registry/values`, `POST /api/registry/values`, `DELETE /api/registry/values`, `POST /api/registry/keys`, `DELETE /api/registry/keys` endpoints on StudentAgent.Service
- `GET /api/registry/export` and `POST /api/registry/import` endpoints on StudentAgent.Service
- `RegistryService` on the agent side supports all five root hives (HKLM, HKCU, HKCR, HKU, HKCC) with formatted value display and full write support
- `Directory.Build.props` as single source of truth for the assembly version; all projects pick up the version automatically at build time
- Agent now reports its version via `GET /api/info` (`AgentVersion` field in `ServerInfoDto`); both teacher clients display the connected agent version in the status bar
- MSI now declaratively creates and sets `BUILTIN\Users` Modify-equivalent permissions on `%ProgramData%\TeacherServer\StudentAgent` via `util:PermissionEx`; permissions are applied on install, reinstall, upgrade, and repair — no longer relying solely on runtime code

### Fixed

- Avalonia registry tree now renders reliably with visible text on the light registry panel
- Both teacher clients now edit registry values from raw registry data instead of formatted display text, preventing corrupted writes for binary, DWORD, QWORD, multi-string, and expandable-string values

### Notes

- Release tagging flow only.
- Windows MSI and macOS `.pkg` builds are produced by GitHub Actions for this release.

## [1.0.5] - 2026-03-31

### Changed

- StudentAgent shared system data directory now grants standard users access so StudentAgent.UIHost can start and work from ProgramData
- shared path helper permissions were expanded for the UIHost runtime
- Windows student-side hosts now use the updated shared data access model

### Notes

- Windows release build only.
- TeacherClient.Avalonia is not built for Windows by this command.
## [1.0.4] - 2026-03-31

### Changed

- StudentAgent service and UIHost now share one system data path helper instead of duplicating path logic
- service logs and settings path resolution were centralized into shared runtime code
- remote command and startup path handling were simplified across the Windows student-side hosts

### Notes

- Windows release build only.
- TeacherClient.Avalonia is not built for Windows by this command.
## [1.0.3] - 2026-03-31

### Changed

- Service and UIHost runtime settings now stay synchronized so browser lock and input lock changes propagate correctly
- Remote command execution now launches hidden cmd processes instead of flashing a console window
- The Windows teacher file toolbar now shows an explicit Open on Student PC button instead of only an icon

### Notes

- Windows release build only.
- TeacherClient.Avalonia is not built for Windows by this command.
## [1.0.2] - 2026-03-31

### Changed

- Teacher Client now handles connection timeouts without crashing and shows a regular error instead of a JIT dialog
- Windows MSI now adds a firewall exception for StudentAgent Service during installation
- StudentAgent.Service startup source was corrected after the latest sync
- Service and UIHost runtime settings now stay synchronized so browser lock and input lock changes propagate correctly
- StudentAgent shared system data directory now grants standard users access so `StudentAgent.UIHost` can start and work from `ProgramData`
- Remote command execution now launches hidden `cmd` processes instead of flashing a console window
- The Windows teacher file toolbar now shows an explicit `Open on Student PC` button instead of only an icon

### Notes

- Windows release build only.
- TeacherClient.Avalonia is not built for Windows by this command.
## [1.0.1] - 2026-03-31

### Changed

- Windows MSI now installs into C:\Program Files\MTD\TeacherServer
- Teacher workstation tools are disabled by default in the installer feature tree
- Teacher Client installation now adds Desktop and Start Menu shortcuts

### Notes

- Windows release build only.
- TeacherClient.Avalonia is not built for Windows by this command.
## [0.1.0] - 2026-03-25

### Added

- Initial Visual Studio solution with `Teacher.Common`, a student-side runtime, and `TeacherClient`.
- Cross-platform `TeacherClient.Avalonia` desktop client for macOS, Linux, and Windows.
- Shared DTO contracts for process, file, and server operations.
- Shared DTO contracts for remote command execution and teacher-side frequent program collection.
- Student-side HTTP API for process inspection, process termination, file browsing, upload, download, deletion, and remote directory creation.
- Student-side process details and process restart endpoints for richer teacher-side process management.
- Teacher-side Windows Forms UI for connecting to the student agent and performing supported remote actions.
- Avalonia desktop UI with process management and dual-pane local/remote file operations.
- Double-click process details dialogs in both teacher clients, with full process metadata and `Kill` / `Restart` actions.
- Remote file-manager action for opening the selected file or folder directly on the connected student PC from both teacher clients.
- Group command support for executing multi-line remote command scripts on either selected student PCs or all online student PCs, with a choice between current-user and administrator execution.
- Teacher-managed frequent programs lists in both teacher clients, with refresh from `C:\Users\Public\Desktop` shortcuts gathered across online student PCs and manual add/remove curation.
- Shared-secret middleware using the `X-Teacher-Secret` request header.
- Student-side tray application flow with protected `Settings`, `Logs`, `About`, and administrator-gated `Exit`.
- WinForms designer-friendly forms for `StudentAgent` and `TeacherClient` dialogs.
- Local runtime persistence for student-agent settings and logs under `%LocalAppData%`.
- Global HTTP exception logging for student-agent requests.
- UDP-based agent discovery with an `Agents` tab in `TeacherClient`.
- Saved manual agent entries in `TeacherClient` with IP, port, MAC address, and notes.
- About dialogs/windows in `TeacherClient`, `StudentAgent.UIHost`, and `TeacherClient.Avalonia`.
- Project documentation in `README.md`.
- Contributor guidance in `AGENTS.md`.
- Agent status filtering, agent grouping, and auto-reconnect behavior in `TeacherClient`.
- Matching agent discovery, grouping, filtering, and auto-reconnect flow in `TeacherClient.Avalonia`.
- Shared `UiLanguage` model and language-aware settings across `TeacherClient`, `TeacherClient.Avalonia`, and the student-side components.
- Bulk distribution actions for sending a selected local file or folder to either selected student agents or all online student agents.
- Teacher-side destination path setting for student file distribution in both teacher clients.
- Live status progress during bulk distribution, including the current target agent and file position.
- Group commands in both teacher clients for clearing the configured student destination folder on either selected agents or all online agents.
- Teacher-side student work folder settings with automatic shared-folder provisioning on reachable student PCs.
- Group commands in both teacher clients for collecting student work folders from either selected agents or all online agents into teacher-side folders named after each student machine.
- Initial `StudentAgent.Service` Windows Service host plus install/uninstall scripts for privileged runtime hosting.
- Background browser-lock enforcement in the service host so browser blocking can continue even without a tray UI process.
- Initial `StudentAgent.UIHost` companion app plus a service-side launcher foundation for running visible tray and overlay UX inside the active student session.
- Added a `Publish-ServiceBundle.ps1` workflow so the Windows service and session UI host can be published into one deployment folder for installation.
- Removed the deprecated monolithic `StudentAgent` project from the solution so `StudentAgent.Service` plus `StudentAgent.UIHost` is now the supported Windows deployment path.
- Added a WiX-based `TeacherServer.Setup` MSI workflow that publishes payloads and builds a single Windows installer with feature selection for either the teacher workstation tools or the student workstation tools.
- Added a dedicated `TeacherClient.Avalonia.Setup` packaging project for macOS that produces a self-contained `.app` bundle and a `.pkg` installer for the Avalonia teacher client.

### Changed

- The student-side UI host now runs as a tray-oriented Windows app instead of relying on a monolithic host project.
- WinForms control sizing was increased to improve readability on real displays and high DPI environments.
- Minimal API request binding was corrected for body/service parameters.
- `TeacherClient` now includes a top menu and discovery-based agent selection flow.
- Manual agent records now also store a group/class value and participate in filtered agent management.
- `TeacherClient` desktop layout was refreshed for Windows with maximized startup, tab toolbars, and more usable control sizing.
- `TeacherClient` dialogs were restyled to match the refreshed Windows desktop layout.
- Both teacher clients now keep the shared secret in a dedicated settings dialog instead of the main window header.
- Both teacher clients now also keep the student destination path for bulk distribution in their settings dialogs.
- Both teacher clients now treat the `Agents` list as the primary connection entry point instead of manual URL entry.
- Bulk folder distribution now recreates the selected folder and its full internal directory structure on each target student machine.
- Both teacher clients now expose a dedicated `Group Commands` menu for teacher-side multi-agent actions against configured student folders, plus a nested `Student Work` submenu for creating, collecting, and clearing the configured student work folder on reachable student machines.
- Both teacher clients now also group destination-folder cleanup commands into a dedicated nested submenu under `Group Commands`.
- Both teacher clients now expose a `Browser lock` checkbox in the agents list. Toggling it enables or disables browser blocking on that student PC, and the student agent now shows a visible 10-second warning before force-closing browser processes that remain open.
- Both teacher clients now also expose an `Input lock` checkbox in the agents list plus bulk `Keyboard and Mouse` commands for locking or unlocking input on online student PCs.
- `StudentAgent.UIHost` now shows a visible fullscreen topmost lock screen while input lock is enabled, making the restriction clear and difficult to bypass for standard student accounts.
- Both teacher clients now expose grouped `Power` commands for shutting down, restarting, or logging off either selected student PCs or all online student PCs.
- Both teacher clients now use the current local teacher folder as the destination root when collecting student work, creating one subfolder per student machine.
- `TeacherClient` toolbar buttons were enlarged and simplified to icon-first actions with hover tooltips.
- `TeacherClient`, `TeacherClient.Avalonia`, and the student-side UI now expose Ukrainian and English UI flows through their settings dialogs and localized runtime text.
- Local and remote file grids in both teacher clients now show folder/file icons next to names, separate extension and file-attribute columns, and human-readable file sizes.
- File panels in both teacher clients now include direct drive selectors for switching local and remote roots, and the file grid columns are weighted so the name column uses the majority of the width.
- `TeacherClient` no longer shows a wait cursor during background agent auto-refresh, reducing the appearance of random UI stalls.
- Both teacher clients now expose a group browser command for enabling browser blocking across all online student PCs at once.
- Both teacher clients now show a branded splash screen during startup.
- Windows deployment now supports an MSI installer flow that packages the self-contained `TeacherClient` and the `StudentAgent.Service` + `StudentAgent.UIHost` pair into selectable install features.
- macOS deployment for `TeacherClient.Avalonia` now has a dedicated packaging flow that builds an installable `.pkg` for `/Applications`.

### Notes

- Command transport remains HTTP-based.
- Auto-discovery is now UDP-based and intended for local network environments.
- Authorization is still based on a shared secret plus a local password for protected tray actions.
- File operations are still not restricted to a sandbox directory.
