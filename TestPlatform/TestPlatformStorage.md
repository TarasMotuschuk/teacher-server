# ClassCommander Test Platform Storage

## Purpose

This document defines the recommended storage model for the ClassCommander testing platform.

It builds on:

- [TestPlatformSchema.md](./TestPlatformSchema.md)
- [TestPlatformApi.md](./TestPlatformApi.md)
- [TestPlatformRoadmap.md](./TestPlatformRoadmap.md)

The main goal is to choose a storage approach that works well for:

- local teacher-side installation
- classroom-scale concurrency
- future sync with Drupal and WordPress
- long-term migration to a larger backend if needed

## Recommendation Summary

Recommended version 1 approach:

- `SQLite` as the primary operational database
- external asset files for large images and imported resources
- canonical test definition JSON stored as a snapshot blob
- sync implemented through API and object versioning, not by replicating the SQLite file

This gives the best balance of:

- easy installation
- low operational complexity
- strong enough querying
- future sync compatibility

## Why SQLite Fits

SQLite is a good fit for the first ClassCommander test server because:

- the initial deployment is local to the teacher workstation
- installation must remain simple
- backup and restore should be easy
- the data model is relational
- teacher-side classroom usage is not expected to require heavy multi-user write throughput

SQLite is not the sync protocol. It is the local operational store.

## What Must Not Be Synced

Do not sync by:

- copying the `.sqlite` file between systems
- giving Drupal or WordPress direct access to the database file
- attempting file-level replication

Sync should happen through:

- stable IDs
- timestamps
- version fields
- canonical API payloads

## Storage Strategy

## Operational Database

Use SQLite for:

- classes
- students
- subjects
- subject-class links
- tests
- test-class links
- assignments
- assignment-student links
- attempts
- attempt answers
- results
- sync metadata

## File Asset Storage

Use the filesystem for:

- question images
- imported raw source files if retained
- temporary import artifacts
- optional exported reports

Recommended asset format:

- `webp` for question images by default

Recommended asset structure:

```text
data/
  tests/
    {test_public_id}/
      definition.json
      assets/
        {asset_public_id}.webp
      imports/
        original.xml
```

This can later be adapted to the existing ClassCommander application data layout.

## High-Level Data Model

The data model should combine:

- school structure
- test engine structure
- assignment and attempt lifecycle

These are related but should remain distinct.

## School Structure Tables

## `classes`

Represents the academic class, such as `5-A`.

### Columns

- `id` integer primary key
- `public_id` text unique not null
- `name` text not null
- `grade` integer not null
- `sort_order` integer not null
- `is_active` integer not null default 1
- `created_at_utc` text not null
- `updated_at_utc` text not null
- `deleted_at_utc` text null

### Notes

- `public_id` should be a UUID or equivalent stable external identifier
- `name` may be values such as `5-A`, `5-Б`, `11-A`

## `students`

Represents a student as a journal record, not yet a full account.

### Columns

- `id` integer primary key
- `public_id` text unique not null
- `surname` text not null
- `name` text not null
- `middlename` text null
- `email` text null
- `class_id` integer not null references `classes(id)`
- `password_hash` text null
- `is_active` integer not null default 1
- `created_at_utc` text not null
- `updated_at_utc` text not null
- `deleted_at_utc` text null

### Notes

- version 1 may not require real password login
- `password_hash` exists only for forward compatibility
- do not store plain-text passwords

## `subjects`

Represents academic subjects such as `Інформатика`.

### Columns

- `id` integer primary key
- `public_id` text unique not null
- `name` text not null
- `code` text null
- `is_active` integer not null default 1
- `created_at_utc` text not null
- `updated_at_utc` text not null
- `deleted_at_utc` text null

## `subject_classes`

Many-to-many relation between subjects and classes.

### Columns

- `subject_id` integer not null references `subjects(id)`
- `class_id` integer not null references `classes(id)`
- `created_at_utc` text not null

### Key

- primary key: `(subject_id, class_id)`

## Test Engine Tables

## `tests`

Represents the logical test record used by teachers and assignments.

### Columns

- `id` integer primary key
- `public_id` text unique not null
- `subject_id` integer null references `subjects(id)`
- `title` text not null
- `description` text null
- `version` integer not null
- `status` text not null
- `definition_json` text not null
- `definition_hash` text null
- `source_kind` text null
- `source_file_name` text null
- `created_by` text null
- `created_at_utc` text not null
- `updated_at_utc` text not null
- `deleted_at_utc` text null

### Notes

- `definition_json` stores the canonical `test-definition` snapshot
- normalized tables for every question are not required in version 1
- editor operations can update `definition_json` as the source of truth

## `test_classes`

Optional many-to-many mapping between tests and classes they are intended for.

### Columns

- `test_id` integer not null references `tests(id)`
- `class_id` integer not null references `classes(id)`
- `created_at_utc` text not null

### Key

- primary key: `(test_id, class_id)`

### Notes

This is preferred over storing multiple class IDs in a single field.

## `test_assets`

Stores metadata about test asset files referenced by the canonical JSON.

### Columns

- `id` integer primary key
- `public_id` text unique not null
- `test_id` integer not null references `tests(id)`
- `kind` text not null
- `mime_type` text not null
- `relative_path` text not null
- `original_file_name` text null
- `width` integer null
- `height` integer null
- `created_at_utc` text not null
- `updated_at_utc` text not null

### Notes

- the canonical JSON may also contain asset metadata
- this table exists to support quick lookup, cleanup, and sync metadata

## Assignment Tables

## `assignments`

Represents the publication of a test to a class or selected students.

### Columns

- `id` integer primary key
- `public_id` text unique not null
- `test_id` integer not null references `tests(id)`
- `subject_id` integer null references `subjects(id)`
- `class_id` integer null references `classes(id)`
- `title` text not null
- `status` text not null
- `start_at_utc` text null
- `end_at_utc` text null
- `max_attempts` integer not null default 1
- `time_limit_seconds` integer null
- `show_score` integer not null default 1
- `show_correct_answers` integer not null default 0
- `show_per_question_feedback` integer not null default 0
- `created_at_utc` text not null
- `updated_at_utc` text not null
- `deleted_at_utc` text null

### Notes

- `assignments` must remain distinct from `tests`
- one test can have many assignments

## `assignment_students`

Optional explicit targeting of individual students.

### Columns

- `assignment_id` integer not null references `assignments(id)`
- `student_id` integer not null references `students(id)`
- `created_at_utc` text not null

### Key

- primary key: `(assignment_id, student_id)`

### Notes

This allows both models:

- assignment to the whole class
- assignment only to selected students

## Attempt And Result Tables

## `attempts`

Represents one student attempt for one assignment.

### Columns

- `id` integer primary key
- `public_id` text unique not null
- `assignment_id` integer not null references `assignments(id)`
- `student_id` integer not null references `students(id)`
- `attempt_number` integer not null
- `status` text not null
- `started_at_utc` text not null
- `last_saved_at_utc` text null
- `submitted_at_utc` text null
- `score_earned` real null
- `score_max` real null
- `percent` real null
- `grade` real null
- `result_json` text null
- `created_at_utc` text not null
- `updated_at_utc` text not null

### Notes

- `result_json` may cache the canonical `result`
- one assignment can have multiple attempts per student if policy allows

## `attempt_answers`

Stores answer payloads for the attempt.

### Columns

- `id` integer primary key
- `attempt_id` integer not null references `attempts(id)`
- `question_id` text not null
- `question_type` text not null
- `answer_json` text not null
- `is_final` integer not null default 0
- `is_correct` integer null
- `earned_score` real null
- `saved_at_utc` text not null
- `created_at_utc` text not null
- `updated_at_utc` text not null

### Notes

- `answer_json` stores the canonical answer payload
- `question_id` refers to the question inside `definition_json`

## Optional Reporting Tables

Version 1 can compute most reports from `attempts` plus `assignments` plus `tests`.

If later needed, add materialized summary tables such as:

- `student_subject_summaries`
- `assignment_result_summaries`

These should be derived, not primary truth.

## Sync-Ready Fields

To keep SQLite compatible with future sync, the following fields are strongly recommended for almost every table:

- `public_id`
- `created_at_utc`
- `updated_at_utc`
- `deleted_at_utc` or soft-delete equivalent

### Why `public_id` Matters

Local integer IDs are convenient inside SQLite but are not suitable for sync across systems.

Each syncable entity should therefore have:

- `id` for local relational joins
- `public_id` for API and sync

## Recommended Syncable Entities

These entities should be sync-friendly from the beginning:

- classes
- students
- subjects
- tests
- assignments
- attempts
- results

## Ownership Model For Sync

Different entities may have different source-of-truth systems.

Recommended initial direction:

- `tests`, `assignments`, `attempts`, `results`
  - source of truth: ClassCommander Test Server
- `classes`, `students`, `subjects`
  - source of truth: configurable later
  - initial MVP: ClassCommander local store
  - future option: Drupal as upstream source for school roster data

### WordPress Recommendation

WordPress should not be the master source for roster data.

Recommended role:

- content and testing integration consumer
- possible launcher and report viewer
- not academic master data owner

## Lightweight Sync Strategy

## Principle

Sync should be object-based and API-driven, not database-file-driven.

### Recommended pattern

For each syncable entity:

- expose `public_id`
- expose `updated_at_utc`
- support pull by `updatedSince`
- support bulk upsert
- support soft delete propagation

### Example Sync Endpoints

- `GET /api/tests/v1/sync/classes?updatedSince=...`
- `GET /api/tests/v1/sync/students?updatedSince=...`
- `GET /api/tests/v1/sync/subjects?updatedSince=...`
- `POST /api/tests/v1/sync/classes/bulk-upsert`
- `POST /api/tests/v1/sync/students/bulk-upsert`
- `POST /api/tests/v1/sync/subjects/bulk-upsert`

### Suggested Sync Modes

#### Phase 1

- no true two-way sync
- local ClassCommander is the only source
- export/import only if needed

#### Phase 2

- one-way sync for school structure
- for example: Drupal roster -> ClassCommander

#### Phase 3

- controlled two-way sync only where business rules are clear

## Why SQLite Still Works For Sync

SQLite is acceptable because:

- sync is at the API layer
- the DB is local operational storage
- entities have stable external IDs
- timestamps and soft delete fields allow change detection

This means SQLite is not a dead end. It remains compatible with future PostgreSQL migration if needed.

## Migration Path To PostgreSQL

If later needed, the model can move to PostgreSQL with relatively low conceptual churn if:

- canonical schema stays unchanged
- API stays unchanged
- `public_id` fields are already in place
- JSON snapshots are already part of the design

Recommended future path:

1. Keep DTOs and API stable
2. Replace storage implementation
3. Migrate SQLite rows to PostgreSQL rows
4. Keep file assets external

## Minimal Version 1 Table Set

If we want the smallest viable relational model for implementation, start with:

- `classes`
- `students`
- `subjects`
- `subject_classes`
- `tests`
- `test_classes`
- `test_assets`
- `assignments`
- `assignment_students`
- `attempts`
- `attempt_answers`

This is enough to support:

- roster structure
- subject-based reporting
- canonical test storage
- assignment lifecycle
- progress sync
- final results

## Example Reporting Queries

The model is intended to support queries such as:

- all scores for one student in one subject
- all attempts for one assignment
- all students in one class with latest result
- average result for one test
- average result for one subject in one class

## Example: Student Grades By Subject

Conceptually:

- `students`
- join `attempts`
- join `assignments`
- join `tests`
- join `subjects`

This is why `assignment`, `attempt`, and `subject` must remain distinct.

## Security Notes

- do not store student passwords in plain text
- version 1 can treat students as journal records, not full login accounts
- if password-based student login is introduced later, use `password_hash` only

## Recommended Next Step

After this storage document, the next implementation-facing step should be:

1. Draft DTOs in `Teacher.Common`
2. Draft SQLite migrations
3. Decide file storage path for test assets and imported sources
4. Implement the first import-to-storage flow for `test-definition`
