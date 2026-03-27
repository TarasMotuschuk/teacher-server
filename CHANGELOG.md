# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog, and this project currently starts with an initial baseline release.

## [0.1.0] - 2026-03-25

### Added

- Initial Visual Studio solution with `Teacher.Common`, `StudentAgent`, and `TeacherClient`.
- Cross-platform `TeacherClient.Avalonia` desktop client for macOS, Linux, and Windows.
- Shared DTO contracts for process, file, and server operations.
- Student-side HTTP API for process inspection, process termination, file browsing, upload, download, deletion, and remote directory creation.
- Teacher-side Windows Forms UI for connecting to the student agent and performing supported remote actions.
- Avalonia desktop UI with process management and dual-pane local/remote file operations.
- Shared-secret middleware using the `X-Teacher-Secret` request header.
- `StudentAgent` tray application flow with protected `Settings`, `Logs`, `About`, and administrator-gated `Exit`.
- WinForms designer-friendly forms for `StudentAgent` and `TeacherClient` dialogs.
- Local runtime persistence for `StudentAgent` settings and logs under `%LocalAppData%`.
- Global HTTP exception logging for `StudentAgent` requests.
- UDP-based agent discovery with an `Agents` tab in `TeacherClient`.
- Saved manual agent entries in `TeacherClient` with IP, port, MAC address, and notes.
- About dialogs/windows in `TeacherClient`, `StudentAgent`, and `TeacherClient.Avalonia`.
- Project documentation in `README.md`.
- Contributor guidance in `AGENTS.md`.
- Agent status filtering, agent grouping, and auto-reconnect behavior in `TeacherClient`.
- Matching agent discovery, grouping, filtering, and auto-reconnect flow in `TeacherClient.Avalonia`.
- Shared `UiLanguage` model and language-aware settings across `TeacherClient`, `TeacherClient.Avalonia`, and `StudentAgent`.
- Bulk distribution actions for sending a selected local file or folder to either selected student agents or all online student agents.
- Teacher-side destination path setting for student file distribution in both teacher clients.
- Live status progress during bulk distribution, including the current target agent and file position.
- Group commands in both teacher clients for clearing the contents of a selected remote student folder on either selected agents or all online agents.

### Changed

- `StudentAgent` now runs as a tray-oriented Windows app instead of a plain console-style host.
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
- Both teacher clients now expose a dedicated `Group Commands` menu for teacher-side multi-agent actions against the currently selected student folder.
- `TeacherClient` toolbar buttons were enlarged and simplified to icon-first actions with hover tooltips.
- `TeacherClient`, `TeacherClient.Avalonia`, and `StudentAgent` now expose Ukrainian and English UI flows through their settings dialogs and localized runtime text.

### Notes

- Command transport remains HTTP-based.
- Auto-discovery is now UDP-based and intended for local network environments.
- Authorization is still based on a shared secret plus a local password for protected tray actions.
- File operations are still not restricted to a sandbox directory.
