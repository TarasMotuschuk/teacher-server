# AGENTS.md

## Purpose

This repository contains a Windows-oriented classroom administration solution currently transitioning from .NET 8 to .NET 10. Agents working in this repo should preserve the project's explicit safety boundary: visible, authorized administration only.

## Repository map

- `Teacher.Common/`: shared contracts and DTOs.
- `StudentAgent.Shared/`: shared student-side runtime, UI, and hosting source files used by the Windows service and UI host.
- `StudentAgent.Service/`: privileged Windows Service host for the student machine.
- `StudentAgent.UIHost/`: session-aware Windows Forms UI host for tray controls, warnings, and visible overlays.
- `TeacherServer.Setup/`: WiX-based Windows installer project and MSI build scripts (`Build-Msi.ps1` publishes both `TeacherClient` and `TeacherClient.Avalonia` into the installer).
- `TeacherClient/`: Windows Forms client for the teacher machine.
- `TeacherClient.Avalonia/`: cross-platform Avalonia client for teacher workstations on macOS, Linux, and Windows.
- `TeacherClient.Avalonia.Setup/`: macOS packaging project for the Avalonia teacher client (`.app` + `.pkg`).
- `TeacherServer.sln`: main solution file.

## Working agreements

- Keep the product transparent and classroom-safe. Do not add stealth behavior, hidden persistence, covert surveillance, or evasion features.
- Preserve compatibility with the current Windows-oriented app model. When the active task is the ongoing framework migration, prefer moving changed projects and shared dependencies forward to `.NET 10` together instead of mixing `.NET 8` and `.NET 10` targets unnecessarily.
- Prefer small, reviewable changes that keep `Teacher.Common` contracts aligned with both server and client.
- Unless the task explicitly calls for a different naming scheme, new git branches may use `feature/*` or `fix/*` prefixes by default.
- When changing API shapes, update the server implementation and shared contracts so the active teacher client and server stay aligned.
- `TeacherClient` (Windows Forms) is in maintenance-only mode. Prefer implementing all new teacher-facing features in `TeacherClient.Avalonia`.
- Only mirror changes into `TeacherClient` when the task explicitly calls for WinForms support, or when the change is a critical bug fix, security fix, packaging fix, or other maintenance-only work needed to keep the legacy client functional.
- Treat security improvements as welcome defaults: TLS, stronger auth, audit logging, and path restrictions are in scope. Covert control capabilities are not.

## Branding

- User-facing product branding is `ClassCommander`.
- Internal repository, project, directory, namespace, and persisted path names such as `TeacherServer`, `TeacherClient`, and `TeacherClient.Avalonia` should remain unchanged unless the task explicitly calls for a technical rename/migration.
- When updating UI, installer copy, splash screens, About dialogs, or documentation, prefer `ClassCommander` for the visible product name.

## Code style

- Follow the existing C# style with file-scoped namespaces, records for DTOs, and concise minimal API handlers.
- Keep UI code in `TeacherClient` practical and maintainable for maintenance work; avoid investing in large new WinForms abstractions or feature work unless explicitly requested.
- Add comments sparingly and only where the logic is non-obvious.
- **Edits in one pass:** When changing a file, apply repo conventions immediately so CI does not fail on a follow-up fix. For C#, that includes correct **`using` order** (e.g. `System.*` first, then other namespaces alphabetically—`dotnet format` aligns with this) and matching existing patterns in the same file. Default to `TeacherClient.Avalonia` as the primary teacher UI. Update both teacher clients only when the task explicitly requires parity or when a maintenance-only WinForms fix is needed.

## Validation

- Prefer validating changes with `dotnet build TeacherServer.sln`.
- If a change affects runtime behavior, mention what was validated and what still needs manual testing on Windows.
- Before pushing, fix formatting failures that break builds (e.g. `IDE0055`, **IMPORTS** / `using` ordering) by running `dotnet format TeacherServer.sln` (or formatting the touched projects) and rebuilding.

## Release workflow

- For the standard Windows release flow, use `powershell -ExecutionPolicy Bypass -File .\scripts\Build-All-Windows.ps1 -Version <x.y.z> -ReleaseSummary "<item 1>; <item 2>; <item 3>"`.
- The `build all windows` command must perform these steps in order: bump the Windows installer version, run a clean Windows build, skip `TeacherClient.Avalonia` Windows builds, append a new release entry to `CHANGELOG.md`, create a git commit, create an annotated git tag `v<x.y.z>`, then push both the commit and the tag to `origin`.
- The `build all windows` command is Windows-only. It does not build the macOS Avalonia installer from `TeacherClient.Avalonia.Setup/`.
- For a lightweight release without the Windows-only script, use a separate `release only` flow: bump `Directory.Build.props` to the confirmed version, move the current `Unreleased` notes into a dated `CHANGELOG.md` release entry, create a git commit, create an annotated tag `v<x.y.z>`, and push both the commit and the tag to `origin`.
- The `release only` flow must not run local MSI or `.pkg` packaging steps when the repository is already configured to build installers in GitHub Actions.
- When a tag `v<x.y.z>` is pushed, prefer relying on GitHub Actions to publish release assets for auto-update consumers, including the student-agent update ZIP, checksum, and JSON manifest.
- Before running the Windows release flow, the agent must propose both the next version and the `ReleaseSummary` to the user.
- The proposed `ReleaseSummary` should be formed by the agent from the current unreleased work, or from the concrete changes made in the current task if there is no dedicated unreleased section yet.
- The user must explicitly confirm the proposed version and `ReleaseSummary`, or provide corrections, before the agent runs the release workflow.
- After confirmation, the agent should continue the selected release workflow without asking for the same release details again unless the scope changes.

## Documentation

- Keep documentation accurate when capabilities, setup steps, configuration UX, or operational assumptions change.
- Maintain these three documentation files together:
  - `README.md`: short, SEO-friendly product overview for users and GitHub visitors.
  - `README.dev.md`: full technical reference (endpoints, updates, packaging, scripts, dev notes).
  - `CHANGELOG.md`: chronological log of user-visible changes by version.
- When making functional or UX changes, update `README.md` and `README.dev.md` as appropriate, and append user-visible milestones to `CHANGELOG.md`.

## Saved commands

- `migrate agent settings to registry`:
  - Replace `StudentAgent.Shared/Services/AgentSettingsStore.cs` file-backed runtime settings with a Windows Registry-backed store under `HKLM\Software\TeacherServer\StudentAgent`.
  - Keep non-secret machine settings in normal registry values, but protect secrets such as `SharedSecret` and `VncPassword` with DPAPI `ProtectedData` using `DataProtectionScope.LocalMachine`.
  - Do not use a legacy `agentsettings.json` file or filesystem fallbacks for runtime settings; store machine settings in `HKLM\Software\TeacherServer\StudentAgent` only (session hosts without HKLM write use localhost HTTP to the service).
  - Remove or narrow the broad `Builtin Users` modify ACL logic in `StudentAgent.Shared/Services/StudentAgentPathHelper.cs` for settings storage; keep filesystem use only for logs, updates, and desktop-layout files.
  - Preserve backward compatibility for existing installed student agents and avoid losing custom `SharedSecret`, VNC settings, or admin-password data during migration.
  - Validate with `dotnet build` for at least `StudentAgent.Service`, `StudentAgent.UIHost`, and `StudentAgent.VncHost`, then note that Windows runtime verification is still required for DPAPI, ACLs, and first-run migration behavior.
