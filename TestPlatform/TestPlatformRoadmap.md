# ClassCommander Test Platform Roadmap

## Purpose

This document outlines a staged plan for building a unified testing platform around ClassCommander that supports:

- desktop authoring and management for teachers
- local classroom delivery to student devices
- attempt tracking, progress syncing, and result reporting
- reuse of the same test bank in Drupal and WordPress

The goal is to avoid building five disconnected products. Instead, we should create one shared test domain model, one shared storage and delivery API, and multiple clients on top of that core.

## Product Scope

The target solution includes five deliverables:

1. A test editor in `ClassCommander.TestEditor`
2. A local test server in `ClassCommander.TestPlatform`
3. A student testing module for classroom delivery and result reporting
4. A Drupal module for online testing
5. A WordPress plugin for online testing

## Guiding Principles

- Treat `MyTest XML` as an import format, not as the long-term source of truth.
- Keep one canonical internal test format for desktop, local server, and CMS integrations.
- Keep one canonical result model for attempts, answers, progress, and scoring.
- Prefer shared DTOs in `Teacher.Common` so the server and clients stay aligned.
- Ship a useful local-network MVP first, then harden it for broader deployment.
- Ensure the student experience stays visible and classroom-safe, consistent with ClassCommander boundaries.

## Core Architecture

### Canonical Data Model

The platform should introduce a canonical JSON-based test model that represents:

- `Test`
- `TestGroup`
- `Question`
- `QuestionType`
- `Variant`
- `QuestionAsset`
- `Assignment`
- `Attempt`
- `AttemptAnswer`
- `AttemptProgress`
- `Result`

Supported question types should initially cover:

- single choice
- multiple choice
- ordered answers
- image point selection
- short text input if needed later

### Shared Components

The long-term shared foundation should live in `Teacher.Common` and optionally a dedicated tests package if the model becomes large.

Recommended shared areas:

- canonical DTOs for tests and attempts
- validation rules
- scoring logic contracts
- serialization helpers
- import/export contracts

### Server Role

The test server should be the single authority for:

- storing imported and edited tests
- publishing assignments to students
- issuing attempt sessions
- receiving progress updates
- receiving completed attempts
- calculating or finalizing results
- serving teacher-side reporting

The server project should live in `ClassCommander.TestPlatform`.

### Client Roles

Teacher-side:

- author or import tests
- review and edit tests
- assign tests to classes, groups, or selected students
- monitor live progress
- inspect results and attempt history

The dedicated teacher authoring tool should live in `ClassCommander.TestEditor`.
`TeacherClient` should only gain the late-stage integration needed to launch selected tests on student PCs and review operational classroom status.

Student-side:

- identify the student
- fetch the assigned test
- save progress while answering
- submit completion
- display result and feedback according to teacher policy

CMS-side:

- use the same test bank and attempt model through API integration
- optionally support standalone auth and web-based delivery modes

## Recommended Repository Direction

This is a suggested target structure, not a required immediate refactor:

- `Teacher.Common`
  Shared test DTOs, result DTOs, contracts, and serialization helpers
- `TeacherServer.Tests` or `ClassCommander.Tests.Server`
- `ClassCommander.TestPlatform`
  Local HTTP API, storage, scoring orchestration, reporting endpoints, and result management
- `ClassCommander.TestEditor`
  Dedicated Avalonia test editor for import, package IO, preview, and authoring
- `TeacherClient`
  Classroom orchestration only; launches selected tests and consumes reporting where needed
- `StudentAgent` or a dedicated `StudentTestClient`
  Student runtime for local test delivery
- `integrations/drupal`
  Drupal module
- `integrations/wordpress`
  WordPress plugin

## Delivery Plan

## Phase 1: MVP For Local Classroom Use

### Goal

Deliver a working teacher-to-student testing flow inside the existing ClassCommander environment for local classroom scenarios.

### Outcomes

- teachers can import MyTest XML into a canonical ClassCommander test format
- teachers can view and lightly edit imported tests
- teachers can assign a selected test to selected student machines
- student devices can receive and run the test
- the server stores attempts, answers, final scores, and completion timestamps
- teachers can review student results from the teacher client

### Recommended Scope

#### 1. Canonical Test Schema

Define a versioned JSON schema for tests that includes:

- metadata
- groups
- question types
- answer options
- scoring metadata
- assets such as images

This should be the foundation for editor, server, and web integrations.

#### 2. Import Pipeline

Build a robust import pipeline:

- `MyTest XML -> canonical JSON`
- preserve question type semantics
- preserve assets
- preserve answer correctness and scoring
- record import warnings where conversion is imperfect

The current `TestPlatform` converter can become the prototype for this pipeline.

#### 3. Local Test Server

Implement a local teacher-side service installed with `TeacherClient`.

Core endpoints for MVP:

- list tests
- get test definition
- create assignment
- get active assignment for student
- create attempt
- post progress
- submit attempt
- list results
- get result details

#### 4. Student Testing Module

Implement a student-facing module that:

- asks for surname and given name
- optionally asks for class/group
- fetches the active assignment
- renders the test
- periodically syncs progress
- submits completion
- displays result based on teacher policy

#### 5. Teacher UI

Implement a minimal editor and management experience in `ClassCommander.TestEditor` first:

- import tests
- browse tests
- preview questions and assets
- open and save `.cctest`
- prepare tests for later assignment from teacher-side classroom tools

#### 6. Local Storage

Use a local database on the teacher side, ideally SQLite for MVP, for:

- test definitions
- assignments
- attempts
- answers
- aggregated results

### MVP Non-Goals

- advanced collaborative editing
- cloud multi-school synchronization
- Drupal and WordPress integrations
- full anti-cheat feature set
- large-scale public internet hosting

### MVP Exit Criteria

MVP is done when:

- one teacher can import and assign a test
- one or more students can complete it
- progress and final results are saved
- the teacher can inspect both summary and detailed answers

## Phase 2: Production-Ready ClassCommander Platform

### Goal

Turn the MVP into a maintainable, resilient, and installable ClassCommander subsystem suitable for routine classroom use.

### Outcomes

- stable desktop editor workflow
- hardened local test server
- better reporting and administration
- stronger identity and assignment handling
- packaging and update support aligned with ClassCommander releases

### Recommended Scope

#### 1. Dedicated Test Editor

Ship a first-class editor in `ClassCommander.TestEditor`.

Preferred direction:

- keep authoring isolated in the dedicated Avalonia editor
- keep `TeacherClient` focused on classroom operations and test launching

Editor capabilities:

- create new tests natively
- edit imported tests
- manage question assets
- validate question integrity
- preview student rendering
- clone and version tests

#### 2. Hardened Test Server

Upgrade the server with:

- versioned API contracts
- migration-aware local database schema
- assignment lifecycle states
- retry-safe progress updates
- audit logging
- configurable retention rules
- backup/export of result history

#### 3. Better Student Identity

Move beyond free-text identity where needed.

Options to support:

- free-text name entry for quick classroom use
- teacher-provisioned class roster
- machine-bound or session-bound student identity
- optional one-time access token per assignment

#### 4. Better Result Model

Support:

- per-question scoring
- partial credit where appropriate
- attempt duration
- autosave and restore
- answer history
- teacher remarks
- export to CSV, JSON, or printable reports

#### 5. Packaging And Deployment

Ensure the editor and local test server are installed with `TeacherClient`.

This likely means:

- installer updates in `TeacherServer.Setup`
- service registration or local process management for the test server
- version-aligned deployment with the desktop client

#### 6. Security And Safety

Keep the feature set classroom-safe and transparent:

- explicit student-facing test UI
- no stealth monitoring
- authenticated teacher-side administration
- local network restrictions where applicable
- signed or validated test packages if tests are distributed between systems

### Production Exit Criteria

Phase 2 is done when:

- the editor is part of the regular teacher workflow
- the test server is stable enough for everyday classroom use
- results and attempts are trustworthy and recoverable
- packaging and upgrades fit existing ClassCommander release practices

## Phase 3: Web Integrations For Drupal And WordPress

### Goal

Expose the same test content and result model to web platforms without forking the product logic.

### Outcomes

- Drupal module backed by the same canonical format and API
- WordPress plugin backed by the same canonical format and API
- optional online delivery independent from the local classroom network

### Recommended Integration Strategy

Do not reimplement the testing engine separately in each CMS.

Preferred approach:

- keep canonical test definitions on the ClassCommander test server
- expose secure API endpoints for test retrieval, attempt creation, progress sync, and result submission
- let Drupal and WordPress act as integration shells and UI adapters

### Drupal Module Scope

- map tests to Drupal content or configuration entities
- assign tests to authenticated or anonymous web users
- render test UI using the canonical JSON model
- submit attempts to the API
- display result summaries and teacher-side reports where appropriate

### WordPress Plugin Scope

- connect to the same API
- provide shortcode or block-based embedding
- support assignment or direct launch modes
- sync attempts and results back to the central server

### CMS-Specific Questions To Resolve

- whether CMS users are mapped to ClassCommander student identities
- whether results stay only in the central test server or are mirrored locally
- whether tests can be public, protected, or roster-bound
- whether CMS sites can author tests or only consume them

### Web Phase Exit Criteria

Phase 3 is done when:

- a single canonical test can be rendered in desktop and web environments
- attempt and result data remain structurally identical
- Drupal and WordPress do not require a separate test authoring pipeline

## Cross-Cutting Decisions To Make Early

These decisions affect all phases and should be resolved near the start:

1. Canonical schema versioning
2. Result calculation ownership
   Server-side scoring is preferred for consistency.
3. Student identity model
4. Offline behavior
5. Asset storage strategy
   External `webp` assets are preferable over embedding large image blobs in JSON.
6. Test publishing model
   Draft, published, archived, and versioned states should exist early.
7. Reporting privacy and retention

## Suggested Technical Backlog

## Foundation

- define canonical `Test` JSON schema
- define `Attempt`, `Answer`, `Result`, and `Assignment` schemas
- create import mapping spec from `MyTest XML`
- define API contract draft
- decide SQLite schema for local server

## Desktop

- add test management section to `TeacherClient.Avalonia`
- implement import flow from XML
- build test preview screen
- build assignment workflow
- build result viewer

## Student

- build identity entry screen
- build test runner
- build autosave/progress sync
- build result screen

## Server

- implement local API host
- implement persistence
- implement scoring service
- implement result aggregation queries
- add audit logging and diagnostics

## CMS

- define API auth model for external web clients
- build Drupal module shell
- build WordPress plugin shell
- share rendering rules with desktop and student clients where possible

## Recommended Immediate Next Steps

I recommend the following sequence for actual implementation planning:

1. Write the canonical JSON schema for tests and attempts
2. Turn the current `TestPlatform` converter into a deliberate import pipeline prototype
3. Design the teacher-side local test server API
4. Add an MVP test-management screen in `TeacherClient.Avalonia`
5. Prototype the student runner against a mock server

## Suggested MVP Definition For The Team

If we want a narrow and realistic first target, the MVP should be:

- import MyTest XML
- store tests locally in canonical JSON form
- assign one test to selected student machines
- run the test on student side
- save attempt progress and final result
- show results in teacher UI

That gives us a coherent vertical slice and de-risks the hardest parts before we commit to Drupal and WordPress.
