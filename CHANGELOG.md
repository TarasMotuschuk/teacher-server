# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog, and this project currently starts with an initial baseline release.

## [0.1.0] - 2026-03-25

### Added

- Initial Visual Studio solution with `Teacher.Common`, a student-side runtime, and `TeacherClient`.
- Cross-platform `TeacherClient.Avalonia` desktop client for macOS, Linux, and Windows.
- Shared DTO contracts for process, file, and server operations.
- Student-side HTTP API for process inspection, process termination, file browsing, upload, download, deletion, and remote directory creation.
- Teacher-side Windows Forms UI for connecting to the student agent and performing supported remote actions.
- Avalonia desktop UI with process management and dual-pane local/remote file operations.
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

### Notes

- Command transport remains HTTP-based.
- Auto-discovery is now UDP-based and intended for local network environments.
- Authorization is still based on a shared secret plus a local password for protected tray actions.
- File operations are still not restricted to a sandbox directory.
