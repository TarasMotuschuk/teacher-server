# AGENTS.md

## Purpose

This repository contains a Windows-oriented classroom administration solution built with .NET 8. Agents working in this repo should preserve the project's explicit safety boundary: visible, authorized administration only.

## Repository map

- `Teacher.Common/`: shared contracts and DTOs.
- `StudentAgent.Shared/`: shared student-side runtime, UI, and hosting source files used by the Windows service and UI host.
- `StudentAgent.Service/`: privileged Windows Service host for the student machine.
- `StudentAgent.UIHost/`: session-aware Windows Forms UI host for tray controls, warnings, and visible overlays.
- `TeacherServer.Setup/`: WiX-based Windows installer project and MSI build scripts.
- `TeacherClient/`: Windows Forms client for the teacher machine.
- `TeacherClient.Avalonia/`: cross-platform Avalonia client for teacher workstations on macOS, Linux, and Windows.
- `TeacherClient.Avalonia.Setup/`: macOS packaging project for the Avalonia teacher client (`.app` + `.pkg`).
- `TeacherServer.sln`: main solution file.

## Working agreements

- Keep the product transparent and classroom-safe. Do not add stealth behavior, hidden persistence, covert surveillance, or evasion features.
- Preserve compatibility with `.NET 8` and the current Windows-oriented app model unless a task explicitly changes that direction.
- Prefer small, reviewable changes that keep `Teacher.Common` contracts aligned with both server and client.
- When changing API shapes, update the server implementation and both teacher clients together.
- Functional changes in `TeacherClient` should be mirrored in `TeacherClient.Avalonia` unless the task explicitly calls for platform-specific behavior.
- Treat security improvements as welcome defaults: TLS, stronger auth, audit logging, and path restrictions are in scope. Covert control capabilities are not.

## Code style

- Follow the existing C# style with file-scoped namespaces, records for DTOs, and concise minimal API handlers.
- Keep UI code in `TeacherClient` practical and maintainable; avoid large hidden abstractions unless they clearly improve readability.
- Add comments sparingly and only where the logic is non-obvious.

## Validation

- Prefer validating changes with `dotnet build TeacherServer.sln`.
- If a change affects runtime behavior, mention what was validated and what still needs manual testing on Windows.

## Release workflow

- For the standard Windows release flow, use `powershell -ExecutionPolicy Bypass -File .\scripts\Build-All-Windows.ps1 -Version <x.y.z> -ReleaseSummary "<item 1>; <item 2>; <item 3>"`.
- The `build all windows` command must perform these steps in order: bump the Windows installer version, run a clean Windows build, skip `TeacherClient.Avalonia` Windows builds, append a new release entry to `CHANGELOG.md`, create a git commit, create an annotated git tag `v<x.y.z>`, then push both the commit and the tag to `origin`.
- The `build all windows` command is Windows-only. It does not build the macOS Avalonia installer from `TeacherClient.Avalonia.Setup/`.
- Before running the Windows release flow, the agent must propose both the next version and the `ReleaseSummary` to the user.
- The proposed `ReleaseSummary` should be formed by the agent from the current unreleased work, or from the concrete changes made in the current task if there is no dedicated unreleased section yet.
- The user must explicitly confirm the proposed version and `ReleaseSummary`, or provide corrections, before the agent runs the release workflow.
- After confirmation, the agent should continue the workflow without asking for the same release details again unless the scope changes.

## Documentation

- Keep `README.md` accurate when capabilities, setup steps, or security assumptions change.
- Append user-visible milestones to `CHANGELOG.md`.
- When making functional or UX changes, explicitly describe those changes in documentation and update `README.md` if user-visible behavior, setup, configuration, or workflows changed.
