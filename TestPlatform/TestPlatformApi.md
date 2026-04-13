# ClassCommander Test Platform API

## Purpose

This document defines the first draft of the HTTP API for the ClassCommander testing platform.

It is based on:

- [TestPlatformSchema.md](./TestPlatformSchema.md)
- [TestPlatformRoadmap.md](./TestPlatformRoadmap.md)

The API is designed to support:

- teacher-side test management
- assignment publishing
- student-side test delivery
- autosave and progress sync
- attempt submission and result retrieval
- future Drupal and WordPress integrations

## API Design Principles

- Use the canonical schema objects directly whenever practical.
- Separate teacher/admin endpoints from student runtime endpoints.
- Keep endpoints explicit and easy to reason about.
- Make progress sync idempotent and retry-safe.
- Let the server own scoring.
- Support local classroom delivery first, with later expansion for web integrations.

## API Versioning

Recommended base path:

```text
/api/tests/v1
```

Examples:

- `/api/tests/v1/test-definitions`
- `/api/tests/v1/assignments`
- `/api/tests/v1/student/active-assignment`

## Roles

Version 1 should assume at least two API roles:

- `teacher-admin`
- `student-runner`

CMS integrations may later use a third role:

- `external-platform`

## Authentication Model

For local MVP, the API can initially reuse the existing ClassCommander shared-secret model, with later expansion to stronger auth.

Recommended direction:

- teacher/admin endpoints require teacher-side authenticated access
- student endpoints require an assignment/session token or server-issued attempt token
- CMS integrations use server-issued integration credentials later

## Core Resources

The API revolves around these resources:

- `test-definition`
- `assignment`
- `attempt`
- `result`
- `student-session`

## High-Level Workflow

Teacher flow:

1. Import or create a test definition
2. Publish an assignment
3. Monitor live progress
4. Review results

Student flow:

1. Identify student
2. Resolve active assignment
3. Start attempt
4. Load test
5. Save progress repeatedly
6. Submit attempt
7. View result according to assignment policy

## Teacher/Admin Endpoints

## 1. Test Definitions

### `GET /api/tests/v1/test-definitions`

List test definitions.

### Query parameters

- `search`
- `grade`
- `subject`
- `tag`
- `status`

### Response

```json
{
  "items": [
    {
      "id": "test_algorithms_5_grade",
      "version": 3,
      "title": "Тема 5. Алгоритми та програми",
      "description": "Практикум",
      "grade": "5",
      "subjects": ["informatics"],
      "questionCount": 107,
      "updatedAtUtc": "2026-04-13T09:00:00Z"
    }
  ],
  "total": 1
}
```

### `GET /api/tests/v1/test-definitions/{testId}`

Return the full canonical `test-definition`.

### `POST /api/tests/v1/test-definitions`

Create a new test definition.

### Request body

Canonical `test-definition`.

### Response

- `201 Created`
- full canonical `test-definition`

### `PUT /api/tests/v1/test-definitions/{testId}`

Replace the current draft version of a test definition.

### `PATCH /api/tests/v1/test-definitions/{testId}`

Apply partial updates for metadata or editor-side changes.

### `DELETE /api/tests/v1/test-definitions/{testId}`

Archive or delete a test definition according to server policy.

Recommended behavior:

- do not hard-delete definitions already used by assignments or attempts
- prefer `archived` state

## 2. Test Import

### `POST /api/tests/v1/imports/mytest-xml`

Upload a MyTest XML file and convert it to a canonical `test-definition`.

### Request

- `multipart/form-data`
- file field: `file`

### Response

```json
{
  "importId": "import_001",
  "status": "completed",
  "testDefinition": {
    "id": "test_algorithms_5_grade",
    "version": 1,
    "title": "Тема 5. Алгоритми та програми"
  },
  "warnings": []
}
```

### Notes

Version 1 may implement import synchronously. If large imports become expensive, this endpoint can later become async.

## 3. Assignments

### `GET /api/tests/v1/assignments`

List assignments.

### Query parameters

- `status`
- `testId`
- `classId`
- `studentId`
- `fromUtc`
- `toUtc`

### `POST /api/tests/v1/assignments`

Create a new assignment.

### Request body

```json
{
  "testId": "test_algorithms_5_grade",
  "testVersion": 3,
  "title": "Практикум: Тема 5",
  "audience": {
    "type": "class",
    "classId": "5-A"
  },
  "availability": {
    "startUtc": "2026-04-13T08:00:00Z",
    "endUtc": "2026-04-13T10:00:00Z"
  },
  "attemptPolicy": {
    "maxAttempts": 1,
    "timeLimitSeconds": 1800
  },
  "resultPolicy": {
    "showScore": true,
    "showCorrectAnswers": false,
    "showPerQuestionFeedback": false
  }
}
```

### Response

- `201 Created`
- canonical `assignment`

### `GET /api/tests/v1/assignments/{assignmentId}`

Return one assignment.

### `PATCH /api/tests/v1/assignments/{assignmentId}`

Update mutable assignment fields:

- title
- availability
- attempt policy
- result policy
- status

### `POST /api/tests/v1/assignments/{assignmentId}/publish`

Move assignment to `published`.

### `POST /api/tests/v1/assignments/{assignmentId}/close`

Close assignment and stop new attempts.

## 4. Teacher Monitoring

### `GET /api/tests/v1/assignments/{assignmentId}/attempts`

List attempts for an assignment.

### Query parameters

- `status`
- `studentName`
- `className`

### Response

```json
{
  "items": [
    {
      "attemptId": "attempt_001",
      "student": {
        "lastName": "Іваненко",
        "firstName": "Олена",
        "className": "5-А"
      },
      "status": "in-progress",
      "startedAtUtc": "2026-04-13T08:15:00Z",
      "lastSavedAtUtc": "2026-04-13T08:22:00Z",
      "answeredCount": 6,
      "questionCount": 10
    }
  ],
  "total": 1
}
```

### `GET /api/tests/v1/attempts/{attemptId}`

Return one attempt with answers.

### `GET /api/tests/v1/attempts/{attemptId}/result`

Return the scored `result` if the attempt is completed.

### `GET /api/tests/v1/assignments/{assignmentId}/results`

Return result summary for a teacher report view.

## Student Runtime Endpoints

These endpoints are intended for the student testing module.

## 1. Student Identity Resolution

### `POST /api/tests/v1/student/resolve`

Resolve the student identity and active assignment context.

### Request body

```json
{
  "lastName": "Іваненко",
  "firstName": "Олена",
  "className": "5-А",
  "deviceId": "pc-05"
}
```

### Response

```json
{
  "student": {
    "lastName": "Іваненко",
    "firstName": "Олена",
    "className": "5-А",
    "deviceId": "pc-05"
  },
  "activeAssignments": [
    {
      "assignmentId": "assignment_2026_04_13_001",
      "title": "Практикум: Тема 5",
      "startUtc": "2026-04-13T08:00:00Z",
      "endUtc": "2026-04-13T10:00:00Z"
    }
  ]
}
```

### Notes

Version 1 may allow multiple matching assignments and let the student choose if policy allows.

## 2. Active Assignment Discovery

### `GET /api/tests/v1/student/active-assignment`

Return the currently active assignment for the identified student/session.

### Query parameters

- `lastName`
- `firstName`
- `className`
- `deviceId`

This can be replaced later by a session token-based flow after `student/resolve`.

## 3. Attempt Start

### `POST /api/tests/v1/student/attempts`

Create or resume an attempt for an assignment.

### Request body

```json
{
  "assignmentId": "assignment_2026_04_13_001",
  "student": {
    "lastName": "Іваненко",
    "firstName": "Олена",
    "className": "5-А",
    "deviceId": "pc-05"
  }
}
```

### Response

```json
{
  "attemptId": "attempt_001",
  "status": "in-progress",
  "startedAtUtc": "2026-04-13T08:15:00Z",
  "lastSavedAtUtc": null,
  "attemptToken": "opaque-token",
  "assignment": {
    "id": "assignment_2026_04_13_001",
    "title": "Практикум: Тема 5"
  },
  "testDefinition": {
    "schemaVersion": 1,
    "type": "test-definition",
    "id": "test_algorithms_5_grade",
    "version": 3,
    "title": "Тема 5. Алгоритми та програми"
  },
  "savedAnswers": []
}
```

### Recommended behavior

- return existing unfinished attempt if one is resumable
- enforce assignment attempt limit
- issue an attempt token for future student-side operations

## 4. Progress Sync

### `PUT /api/tests/v1/student/attempts/{attemptId}/progress`

Replace the known draft state for the attempt.

This is preferred over patching individual answers because it is simpler and retry-safe.

### Request body

```json
{
  "answers": [
    {
      "questionId": "q_single_1",
      "type": "single-choice",
      "value": {
        "selectedOptionIds": ["opt_1"]
      },
      "isFinal": false,
      "savedAtUtc": "2026-04-13T08:18:00Z"
    },
    {
      "questionId": "q_image_1",
      "type": "image-point",
      "value": {
        "x": 104,
        "y": 22
      },
      "isFinal": true,
      "savedAtUtc": "2026-04-13T08:19:10Z"
    }
  ],
  "clientProgress": {
    "answeredCount": 6,
    "questionCount": 10,
    "currentQuestionId": "q_image_1"
  }
}
```

### Response

```json
{
  "attemptId": "attempt_001",
  "status": "in-progress",
  "lastSavedAtUtc": "2026-04-13T08:19:10Z"
}
```

### Notes

Version 1 should support frequent autosave from the student module.

## 5. Submit Attempt

### `POST /api/tests/v1/student/attempts/{attemptId}/submit`

Mark the attempt as completed and trigger scoring.

### Request body

```json
{
  "answers": [
    {
      "questionId": "q_single_1",
      "type": "single-choice",
      "value": {
        "selectedOptionIds": ["opt_1"]
      },
      "isFinal": true,
      "savedAtUtc": "2026-04-13T08:27:00Z"
    }
  ]
}
```

### Response

```json
{
  "attemptId": "attempt_001",
  "status": "submitted",
  "submittedAtUtc": "2026-04-13T08:28:00Z",
  "result": {
    "attemptId": "attempt_001",
    "scoreEarned": 8,
    "scoreMax": 10,
    "percent": 80,
    "grade": 9,
    "completedAtUtc": "2026-04-13T08:28:00Z"
  },
  "resultView": {
    "showScore": true,
    "showCorrectAnswers": false,
    "showPerQuestionFeedback": false
  }
}
```

## 6. Resume Attempt

### `GET /api/tests/v1/student/attempts/{attemptId}`

Return the current attempt state for a resumable session.

### Response

- canonical attempt metadata
- saved answers
- assignment policy flags if needed

## 7. Student Result View

### `GET /api/tests/v1/student/attempts/{attemptId}/result`

Return the student-visible result payload after submission.

### Response rules

The response must follow assignment result policy:

- score only
- score + per-question feedback
- score + correct answers
- hidden until teacher release if needed later

## Suggested Status Values

## Assignment Status

- `draft`
- `published`
- `closed`
- `archived`

## Attempt Status

- `not-started`
- `in-progress`
- `submitted`
- `scored`
- `expired`
- `cancelled`

## Error Model

Version 1 should use a consistent JSON error envelope.

```json
{
  "error": {
    "code": "assignment_not_active",
    "message": "The assignment is not active for this student.",
    "details": null
  }
}
```

## Suggested Error Codes

- `validation_error`
- `not_found`
- `forbidden`
- `assignment_not_active`
- `attempt_limit_reached`
- `attempt_not_resumable`
- `attempt_already_submitted`
- `scoring_failed`
- `unsupported_question_type`

## Minimal Endpoint Set For MVP

If we want the smallest useful local classroom API, MVP can start with these endpoints:

- `GET /api/tests/v1/test-definitions`
- `GET /api/tests/v1/test-definitions/{testId}`
- `POST /api/tests/v1/imports/mytest-xml`
- `POST /api/tests/v1/assignments`
- `GET /api/tests/v1/assignments/{assignmentId}/attempts`
- `POST /api/tests/v1/student/resolve`
- `POST /api/tests/v1/student/attempts`
- `PUT /api/tests/v1/student/attempts/{attemptId}/progress`
- `POST /api/tests/v1/student/attempts/{attemptId}/submit`
- `GET /api/tests/v1/attempts/{attemptId}/result`

## Recommended Storage Ownership

Server-side ownership should be:

- test definitions stored by server
- assignments stored by server
- attempts stored by server
- scoring performed by server
- result snapshots stored by server

Clients should not be the source of truth for scoring.

## CMS Integration Direction

Drupal and WordPress should use the same resource model and as much of the same endpoint set as possible.

Recommended direction:

- CMS integrations consume `test-definition`, `assignment`, `attempt`, and `result`
- CMS integrations authenticate through separate integration credentials later
- CMS integrations do not invent their own scoring rules

## Open Questions

These decisions should be made before freezing the first implementation:

1. Whether student identity stays free-text in MVP or gets a stronger roster model immediately
2. Whether attempt tokens are enough for local classroom sessions or whether server-side session objects are also needed
3. Whether submit should return a scored result synchronously or allow async scoring
4. Whether teacher monitoring should later support WebSocket or Server-Sent Events for live progress
5. Whether assignments can target both named classes and explicit machine groups in version 1

## Recommended Next Step

After this API document, the next implementation-facing step should be:

1. Define DTOs in `Teacher.Common`
2. Sketch the SQLite storage model
3. Create a server project for test definitions, assignments, attempts, and results
4. Build a minimal import-to-storage path using the canonical schema
