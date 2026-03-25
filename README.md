# Teacher Server

`Teacher Server` is a .NET 8 classroom administration solution for a transparent, teacher-controlled environment. It includes:

- `StudentAgent`: ASP.NET Core agent running on the student workstation.
- `TeacherClient`: Windows Forms application used by the teacher.
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

- connection to the student agent by URL and shared secret;
- process list refresh and remote process termination;
- dual-pane local/remote file browsing;
- file upload and download;
- remote directory creation;
- local and remote deletion with confirmation dialogs.

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

### Start StudentAgent

1. Open the solution in Visual Studio or use the CLI.
2. Review `StudentAgent/appsettings.json`.
3. Change the default shared secret before use.
4. Start the agent.
5. Ensure TCP port `5055` is reachable from the teacher machine.

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
2. Enter the student agent URL, for example `http://192.168.1.50:5055`.
3. Enter the same shared secret as configured on the student side.
4. Press `Connect`.

## Repository structure

```text
TeacherServer.sln
Teacher.Common/
StudentAgent/
TeacherClient/
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
