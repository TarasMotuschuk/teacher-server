# ClassCommander Test Platform (design notes)

This folder holds design documents and prototypes for the classroom testing subsystem.

## Runtime projects

- `ClassCommander.TestPlatform` — local HTTP test server (SQLite + MyTest XML import + assignments/attempts/scoring)
- `ClassCommander.TestEditor` — Avalonia authoring app (create/open/save `.cctest`, import MyTest XML, browse/preview questions, EN/UA UI)
- `ClassCommander.TestRunner` — Avalonia student app (sign-in, list assignments, answer all 9 question types, save progress, submit)
- `ClassCommander.Testing.Core` — shared MyTest importer, `.cctest` packaging, JSON helpers
- `Teacher.Common/Contracts/Testing` — shared canonical DTOs

## Run the test editor

```bash
dotnet run --project ClassCommander.TestEditor/ClassCommander.TestEditor.csproj
```

Authoring UI: create a new test, add/edit/delete all 9 question types with answer keys, import MyTest XML, open/save `.cctest`. From teacher Avalonia: **Configuration → Test Editor…**.

## Run the student test runner

```bash
dotnet run --project ClassCommander.TestRunner/ClassCommander.TestRunner.csproj
```

Point the runner at a running TestPlatform base URL (default `http://127.0.0.1:5050`; on macOS port `5000` is often taken by AirPlay Receiver).

## Teacher Avalonia Testing UI

In `TeacherClient.Avalonia` open **Configuration → Testing…** (after starting TestPlatform). From there you can:

- connect to the Test Platform URL (also configurable under **Basic Settings**)
- list tests and import MyTest XML or `.cctest` packages from TestEditor
- create class-scoped assignments
- deploy TestRunner to student PCs and start tests over the network (selected / all online)
- close assignments and monitor attempts/results (including student answer details)

Students should not enter the TestPlatform URL — the teacher launch passes `--server-url` with the teacher LAN address.

## Run the local server

```bash
ASPNETCORE_URLS=http://127.0.0.1:5050 dotnet run --project ClassCommander.TestPlatform/ClassCommander.TestPlatform.csproj
```

Default data root (override with `TestPlatform__DataRoot`):

```text
LocalApplicationData/ClassCommander/TestPlatform
```

Useful endpoints:

- `GET /health`
- `GET /api/tests/v1/capabilities`
- `GET /api/tests/v1/test-definitions`
- `POST /api/tests/v1/imports/mytest-xml` (`multipart/form-data`, field `file`)
- `POST /api/tests/v1/imports/cctest` (`multipart/form-data`, field `file`)
- `POST /api/tests/v1/assignments`
- `POST /api/tests/v1/student/resolve`
- `POST /api/tests/v1/student/attempts`
- `PUT /api/tests/v1/student/attempts/{id}/progress` (header `X-Attempt-Token`)
- `POST /api/tests/v1/student/attempts/{id}/submit` (header `X-Attempt-Token`)

## Documents

- [TestPlatformRoadmap.md](./TestPlatformRoadmap.md)
- [TestPlatformSchema.md](./TestPlatformSchema.md)
- [TestPlatformApi.md](./TestPlatformApi.md)
- [TestPlatformStorage.md](./TestPlatformStorage.md)
- [TestPlatformPackageFormat.md](./TestPlatformPackageFormat.md)
- [TestPlatformDtos.md](./TestPlatformDtos.md)

## Prototypes

- `convert_mytest_xml.py` — early Python MyTest → JSON viewer pipeline (superseded for server import by the C# importer)
- `viewer.html` — browser preview for the Python JSON output

### Python converter usage

```bash
python3 TestPlatform/convert_mytest_xml.py \
  "/path/to/test1.xml" \
  "/path/to/test2.xml" \
  --output-dir TestPlatform/out
```
