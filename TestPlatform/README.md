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

## Run the student test runner

```bash
dotnet run --project ClassCommander.TestRunner/ClassCommander.TestRunner.csproj
```

Point the runner at a running TestPlatform base URL (default `http://127.0.0.1:5000`).

## Run the local server

```bash
dotnet run --project ClassCommander.TestPlatform/ClassCommander.TestPlatform.csproj
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
