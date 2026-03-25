# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog, and this project currently starts with an initial baseline release.

## [0.1.0] - 2026-03-25

### Added

- Initial Visual Studio solution with `Teacher.Common`, `StudentAgent`, and `TeacherClient`.
- Shared DTO contracts for process, file, and server operations.
- Student-side HTTP API for process inspection, process termination, file browsing, upload, download, deletion, and remote directory creation.
- Teacher-side Windows Forms UI for connecting to the student agent and performing supported remote actions.
- Shared-secret middleware using the `X-Teacher-Secret` request header.
- Project documentation in `README.md`.
- Contributor guidance in `AGENTS.md`.

### Notes

- Current transport is plain HTTP.
- Current authorization model is a shared secret.
- File operations are not yet restricted to a sandbox directory.
