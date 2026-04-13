# ClassCommander Test Platform Schema

## Purpose

This document defines the first draft of the canonical JSON model for the ClassCommander testing platform.

The model is intended to support:

- import from `MyTest XML`
- native test editing
- local classroom delivery
- attempt and result tracking
- Drupal and WordPress integrations

The key rule is that this schema is not a JSON-shaped copy of `MyTest XML`. It is the long-term internal model for ClassCommander.

## Design Principles

- Keep `MyTest XML` as an import source, not as the storage contract.
- Separate `test definition`, `assignment`, `attempt`, and `result`.
- Normalize question types into a stable canonical enum.
- Prefer external assets over embedding large image blobs in JSON by default.
- Keep the schema versioned from the beginning.

## Top-Level Documents

The platform should work with four main JSON document types:

1. `test-definition`
2. `assignment`
3. `attempt`
4. `result`

## 1. Test Definition

`test-definition` describes the reusable structure of a test independently of who takes it.

### Required fields

- `schemaVersion`
- `type`
- `id`
- `version`
- `title`
- `settings`
- `groups`

### Suggested shape

```json
{
  "schemaVersion": 1,
  "type": "test-definition",
  "id": "test_algorithms_5_grade",
  "version": 3,
  "title": "Тема 5. Алгоритми та програми",
  "description": "Практикум",
  "language": "uk-UA",
  "grade": "5",
  "subjects": ["informatics"],
  "tags": ["algorithms", "practice"],
  "author": {
    "name": "Мацаєнко Сергій Васильович",
    "email": null
  },
  "source": {
    "kind": "mytest-xml-import",
    "originalFileName": "Тема 5. Алгоритми та програми_new.xml",
    "importedAtUtc": "2026-04-13T08:00:00Z",
    "metadata": {
      "myTestVersion": "11.0"
    }
  },
  "settings": {
    "shuffleQuestions": true,
    "shuffleOptions": true,
    "showCorrectAnswersAfterFinish": false,
    "timeLimitSeconds": null,
    "defaultAttemptLimit": 1,
    "resultVisibility": "score-only"
  },
  "assets": [],
  "groups": []
}
```

## 2. Assignment

`assignment` describes when, where, and for whom a test is available.

```json
{
  "schemaVersion": 1,
  "type": "assignment",
  "id": "assignment_2026_04_13_001",
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
  },
  "status": "published"
}
```

## 3. Attempt

`attempt` describes one concrete student session for one assignment.

```json
{
  "schemaVersion": 1,
  "type": "attempt",
  "id": "attempt_001",
  "assignmentId": "assignment_2026_04_13_001",
  "testId": "test_algorithms_5_grade",
  "testVersion": 3,
  "student": {
    "lastName": "Іваненко",
    "firstName": "Олена",
    "className": "5-А",
    "deviceId": null
  },
  "status": "in-progress",
  "startedAtUtc": "2026-04-13T08:15:00Z",
  "lastSavedAtUtc": "2026-04-13T08:22:00Z",
  "submittedAtUtc": null,
  "answers": []
}
```

## 4. Result

`result` is the scored evaluation of an attempt.

```json
{
  "schemaVersion": 1,
  "type": "result",
  "attemptId": "attempt_001",
  "scoreEarned": 8,
  "scoreMax": 10,
  "percent": 80,
  "grade": 9,
  "completedAtUtc": "2026-04-13T08:28:00Z",
  "questionResults": [
    {
      "questionId": "q_0008",
      "isCorrect": true,
      "scoreEarned": 1,
      "scoreMax": 1
    }
  ]
}
```

## Test Definition Details

## Groups

Questions should be organized into `groups`, matching imported structure where available.

```json
{
  "id": "group_1",
  "title": "Основи алгоритмів",
  "description": "",
  "order": 1,
  "questions": []
}
```

## Assets

Images and other reusable resources should be stored once at test level and referenced by questions.

### Recommended asset shape

```json
{
  "id": "asset_d22184a7",
  "kind": "image",
  "mimeType": "image/webp",
  "path": "assets/D22184A7-A84F-4A97-8D23-77C544C9E952.webp",
  "width": 860,
  "height": 464,
  "source": {
    "originalFileName": "{D22184A7-A84F-4A97-8D23-77C544C9E952}.png"
  }
}
```

### Asset Strategy

For imported tests with images:

- default storage mode should be external `webp`
- embedded base64 should remain optional
- original source file name and source image format should be preserved in metadata

## Question Base Shape

All questions should share a common envelope.

```json
{
  "id": "q_0001",
  "type": "single-choice",
  "prompt": "Питання",
  "description": null,
  "score": 1,
  "required": true,
  "assets": [],
  "content": {},
  "interaction": {},
  "answerKey": {},
  "source": {
    "myTestType": "TYPE_TASK_CHOICE_SINGLE"
  }
}
```

### Shared Question Fields

- `id`
- `type`
- `prompt`
- `description`
- `score`
- `required`
- `assets`
- `content`
- `interaction`
- `answerKey`
- `source`

## Canonical Question Types

Version 1 must support at least these nine canonical question types:

- `single-choice`
- `multiple-choice`
- `ordering`
- `matching`
- `true-false-group`
- `numeric-input-group`
- `text-input`
- `image-point`
- `letter-ordering`

## MyTest To Canonical Type Mapping

| MyTest type | Canonical type |
|---|---|
| `TYPE_TASK_CHOICE_SINGLE` | `single-choice` |
| `TYPE_TASK_CHOICE_MULTIPLE` | `multiple-choice` |
| `TYPE_TASK_CHOICE_ORDER` | `ordering` |
| `TYPE_TASK_CHOICE_COLLATION` | `matching` |
| `TYPE_TASK_CHOICE_TRUE_FALSE` | `true-false-group` |
| `TYPE_TASK_ENTER_NUM` | `numeric-input-group` |
| `TYPE_TASK_ENTER_TEXT` | `text-input` |
| `TYPE_TASK_IMAGE_POINT` | `image-point` |
| `TYPE_TASK_WORD` | `letter-ordering` |

## Question Type Shapes

## 1. Single Choice

```json
{
  "id": "q_single_1",
  "type": "single-choice",
  "prompt": "Оберіть правильну відповідь",
  "score": 1,
  "required": true,
  "assets": [],
  "content": {},
  "interaction": {
    "options": [
      { "id": "opt_1", "text": "Варіант 1", "order": 1 },
      { "id": "opt_2", "text": "Варіант 2", "order": 2 },
      { "id": "opt_3", "text": "Варіант 3", "order": 3 }
    ]
  },
  "answerKey": {
    "correctOptionIds": ["opt_1"]
  }
}
```

## 2. Multiple Choice

```json
{
  "id": "q_multi_1",
  "type": "multiple-choice",
  "prompt": "Оберіть усі правильні відповіді",
  "score": 1,
  "required": true,
  "assets": [],
  "content": {},
  "interaction": {
    "options": [
      { "id": "opt_1", "text": "Варіант 1", "order": 1 },
      { "id": "opt_2", "text": "Варіант 2", "order": 2 },
      { "id": "opt_3", "text": "Варіант 3", "order": 3 },
      { "id": "opt_4", "text": "Варіант 4", "order": 4 }
    ]
  },
  "answerKey": {
    "correctOptionIds": ["opt_1", "opt_2"]
  }
}
```

## 3. Ordering

```json
{
  "id": "q_order_1",
  "type": "ordering",
  "prompt": "Встановіть правильний порядок",
  "score": 1,
  "required": true,
  "assets": [],
  "content": {},
  "interaction": {
    "options": [
      { "id": "opt_a", "text": "test 1", "order": 1 },
      { "id": "opt_b", "text": "test 2", "order": 2 },
      { "id": "opt_c", "text": "test 4", "order": 3 },
      { "id": "opt_d", "text": "test 5", "order": 4 },
      { "id": "opt_e", "text": "test 6", "order": 5 }
    ]
  },
  "answerKey": {
    "correctOrder": ["opt_a", "opt_c", "opt_d", "opt_b", "opt_e"]
  }
}
```

## 4. Matching

`matching` is the canonical equivalent of `TYPE_TASK_CHOICE_COLLATION`.

```json
{
  "id": "q_match_1",
  "type": "matching",
  "prompt": "Встановіть відповідність",
  "score": 1,
  "required": true,
  "assets": [],
  "content": {},
  "interaction": {
    "leftItems": [
      { "id": "left_1", "text": "1", "order": 1 },
      { "id": "left_2", "text": "2", "order": 2 },
      { "id": "left_3", "text": "3", "order": 3 },
      { "id": "left_4", "text": "2", "order": 4 }
    ],
    "rightItems": [
      { "id": "right_1", "text": "1", "order": 1 },
      { "id": "right_2", "text": "2", "order": 2 },
      { "id": "right_3", "text": "2", "order": 3 },
      { "id": "right_4", "text": "3", "order": 4 }
    ]
  },
  "answerKey": {
    "pairs": [
      { "leftId": "left_1", "rightId": "right_1" },
      { "leftId": "left_2", "rightId": "right_3" },
      { "leftId": "left_3", "rightId": "right_4" },
      { "leftId": "left_4", "rightId": "right_2" }
    ]
  }
}
```

## 5. True False Group

`TYPE_TASK_CHOICE_TRUE_FALSE` is not a simple yes/no question. It is a list of statements, each with its own truth value.

```json
{
  "id": "q_tf_1",
  "type": "true-false-group",
  "prompt": "Для кожного твердження вкажіть, чи воно правильне",
  "score": 1,
  "required": true,
  "assets": [],
  "content": {},
  "interaction": {
    "statements": [
      { "id": "s_1", "text": "Питання 1", "order": 1 },
      { "id": "s_2", "text": "Питання 2", "order": 2 },
      { "id": "s_3", "text": "Питання 3", "order": 3 }
    ]
  },
  "answerKey": {
    "statementTruth": [
      { "statementId": "s_1", "value": true },
      { "statementId": "s_2", "value": false },
      { "statementId": "s_3", "value": true }
    ]
  }
}
```

## 6. Numeric Input Group

`TYPE_TASK_ENTER_NUM` should support multiple numeric blanks in one question.

```json
{
  "id": "q_num_1",
  "type": "numeric-input-group",
  "prompt": "Введіть числові відповіді",
  "score": 1,
  "required": true,
  "assets": [],
  "content": {},
  "interaction": {
    "entries": [
      { "id": "n_1", "caption": "34+45", "order": 1 },
      { "id": "n_2", "caption": "34+45", "order": 2 }
    ]
  },
  "answerKey": {
    "values": [
      { "entryId": "n_1", "acceptedNumbers": [44] },
      { "entryId": "n_2", "acceptedNumbers": [35] }
    ]
  }
}
```

### Numeric Rules

Version 1 should support:

- integer values
- decimal values if present
- optional tolerance in future versions

## 7. Text Input

```json
{
  "id": "q_text_1",
  "type": "text-input",
  "prompt": "Введіть текстову відповідь",
  "score": 1,
  "required": true,
  "assets": [],
  "content": {},
  "interaction": {
    "placeholder": null,
    "maxLength": null
  },
  "answerKey": {
    "acceptedTexts": ["відповідь"],
    "caseSensitive": false,
    "trimWhitespace": true
  }
}
```

## 8. Image Point

```json
{
  "id": "q_image_1",
  "type": "image-point",
  "prompt": "Вкажіть точку на зображенні",
  "score": 1,
  "required": true,
  "assets": [
    {
      "assetId": "asset_d22184a7",
      "role": "question-image"
    }
  ],
  "content": {},
  "interaction": {
    "selectionMode": "single-point"
  },
  "answerKey": {
    "regions": [
      {
        "shape": "polygon",
        "points": [[10, 10], [120, 10], [120, 80], [10, 80]]
      }
    ]
  }
}
```

### Image Rules

Version 1 should support:

- one displayed image per question
- one selected point from the student
- one or more accepted regions
- polygon regions

## 9. Letter Ordering

`TYPE_TASK_WORD` should remain a distinct question type in the canonical model.

```json
{
  "id": "q_word_1",
  "type": "letter-ordering",
  "prompt": "Складіть слово",
  "score": 1,
  "required": true,
  "assets": [],
  "content": {
    "sourceWord": "АБРАКАДАБРА"
  },
  "interaction": {
    "mode": "assemble-word"
  },
  "answerKey": {
    "targetWord": "АБРАКАДАБРА",
    "caseSensitive": true
  }
}
```

## Attempt Answer Shapes

Answers should be stored in a common `answers[]` array inside `attempt`, but their `value` shape depends on question type.

## Single Choice Answer

```json
{
  "questionId": "q_single_1",
  "type": "single-choice",
  "value": {
    "selectedOptionIds": ["opt_1"]
  },
  "isFinal": true,
  "savedAtUtc": "2026-04-13T08:16:10Z"
}
```

## Multiple Choice Answer

```json
{
  "questionId": "q_multi_1",
  "type": "multiple-choice",
  "value": {
    "selectedOptionIds": ["opt_1", "opt_2"]
  }
}
```

## Ordering Answer

```json
{
  "questionId": "q_order_1",
  "type": "ordering",
  "value": {
    "orderedOptionIds": ["opt_a", "opt_c", "opt_d", "opt_b", "opt_e"]
  }
}
```

## Matching Answer

```json
{
  "questionId": "q_match_1",
  "type": "matching",
  "value": {
    "pairs": [
      { "leftId": "left_1", "rightId": "right_1" },
      { "leftId": "left_2", "rightId": "right_3" }
    ]
  }
}
```

## True False Group Answer

```json
{
  "questionId": "q_tf_1",
  "type": "true-false-group",
  "value": {
    "statementTruth": [
      { "statementId": "s_1", "value": true },
      { "statementId": "s_2", "value": false },
      { "statementId": "s_3", "value": true }
    ]
  }
}
```

## Numeric Input Group Answer

```json
{
  "questionId": "q_num_1",
  "type": "numeric-input-group",
  "value": {
    "entries": [
      { "entryId": "n_1", "value": 44 },
      { "entryId": "n_2", "value": 35 }
    ]
  }
}
```

## Text Input Answer

```json
{
  "questionId": "q_text_1",
  "type": "text-input",
  "value": {
    "text": "Відповідь"
  }
}
```

## Image Point Answer

```json
{
  "questionId": "q_image_1",
  "type": "image-point",
  "value": {
    "x": 104,
    "y": 22
  }
}
```

## Word Assembly Answer

```json
{
  "questionId": "q_word_1",
  "type": "letter-ordering",
  "value": {
    "text": "АБРАКАДАБРА"
  }
}
```

## Scoring Rules For Version 1

Recommended version 1 scoring behavior:

- `single-choice`
  Full score only if selected option set matches exactly one correct option.
- `multiple-choice`
  Full score only if selected set matches the correct set exactly.
- `ordering`
  Full score only if order matches exactly.
- `matching`
  Full score only if all pairs match exactly.
- `true-false-group`
  Full score only if all statement values match exactly.
- `numeric-input-group`
  Full score only if all entered numeric values match accepted numeric answers.
- `text-input`
  Full score only if normalized text matches one accepted answer.
- `image-point`
  Full score only if selected point falls inside an accepted region.
- `letter-ordering`
  Full score only if assembled word matches target word.

Partial credit can be introduced in later schema versions.

## Import Notes For MyTest

The importer should preserve these source details for diagnostics:

- original MyTest question type
- original ordering
- imported asset file name
- source-specific fields that do not map cleanly
- import warnings

Recommended `source` payload on question:

```json
{
  "myTestType": "TYPE_TASK_CHOICE_COLLATION",
  "importWarnings": []
}
```

## Open Questions

These areas should be decided before freezing version 1 DTOs:

1. Whether `letter-ordering` is rendered as scrambled letters or as plain text reconstruction
2. Whether `numeric-input-group` should support tolerance
3. Whether `text-input` should support multiple aliases and normalization rules per locale
4. Whether `true-false-group` and `matching` should support partial credit in version 1 or only later
5. Whether question-level rich text should be normalized to plain text for version 1 editor screens, with richer formatting preserved only as import metadata

## Recommended Next Step

After this schema draft, the next technical document should be:

- `TestPlatformApi.md`

That API document should use the exact shapes defined here for:

- returning test definitions to clients
- starting assignments and attempts
- syncing student answers
- submitting attempts
- returning scored results
