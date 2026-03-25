# AGENTS.md

## Purpose

This repository contains a Windows-oriented classroom administration solution built with .NET 8. Agents working in this repo should preserve the project's explicit safety boundary: visible, authorized administration only.

## Repository map

- `Teacher.Common/`: shared contracts and DTOs.
- `StudentAgent/`: ASP.NET Core minimal API for the student machine.
- `TeacherClient/`: Windows Forms client for the teacher machine.
- `TeacherClient.Avalonia/`: cross-platform Avalonia client for teacher workstations on macOS, Linux, and Windows.
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

## Documentation

- Keep `README.md` accurate when capabilities, setup steps, or security assumptions change.
- Append user-visible milestones to `CHANGELOG.md`.
- When making functional or UX changes, explicitly describe those changes in documentation and update `README.md` if user-visible behavior, setup, configuration, or workflows changed.
