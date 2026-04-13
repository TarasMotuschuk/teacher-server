# ClassCommander Test Package Format

## Purpose

This document defines the portable package format for ClassCommander tests.

The package format is intended for:

- exporting a test as a single file
- importing a test into another ClassCommander instance
- moving tests between environments
- future Drupal and WordPress import/export flows

The package extension for version 1 is:

- `.cctest`

## Package Principles

- one package file should contain everything needed to transport one logical test
- the package should be easy to inspect and debug
- the package should use the canonical `test-definition` model
- all packaged image assets must be normalized to `webp`
- source import artifacts may be included, but they are optional

## Container Format

Version 1 package container:

- ZIP archive

The `.cctest` file is therefore a ZIP-based custom container with a defined internal structure.

## File Extension

Recommended file extension:

- `.cctest`

Examples:

- `Tema-5-Algorytmy-ta-prohramy.cctest`
- `Informatyka-5-klas-lesson-12.cctest`

## Versioning

The package format must be versioned independently from:

- app version
- test definition version

Recommended fields:

- `packageFormat`
- `packageFormatVersion`

## Package Structure

Version 1 package structure:

```text
example.cctest
  manifest.json
  definition.json
  assets/
    asset_d22184a7.webp
    asset_f29c5745.webp
  imports/
    original.xml
```

Only `manifest.json` and `definition.json` are required.

## Required Files

### `manifest.json`

Package-level metadata and integrity information.

### `definition.json`

Canonical `test-definition` JSON as defined in [TestPlatformSchema.md](./TestPlatformSchema.md).

## Optional Files

### `assets/`

Contains the image and media files referenced by `definition.json`.

### `imports/`

Contains optional raw source files such as imported `MyTest XML`.

### `meta/`

Reserved for future optional files such as:

- thumbnails
- editor metadata
- signatures
- migration notes

## Image Asset Rule

All packaged question images must be stored as:

- `webp`

This is a mandatory rule for `.cctest` version 1.

### Implications

- source `bmp` images must be converted to `webp`
- source `png` images must be converted to `webp`
- source `jpg` images must be converted to `webp`
- `definition.json` must only reference packaged image assets as `image/webp`

## Asset Naming

Asset file names should be stable and deterministic.

Recommended naming rule:

- `{asset_public_id}.webp`

Example:

```text
assets/asset_d22184a7.webp
```

This is preferred over preserving source file names as the primary packaged name.

Original source file name should instead be preserved in metadata.

## `definition.json` Requirements

The packaged `definition.json` must:

- be valid canonical `test-definition`
- use package-relative asset paths
- only point to packaged `webp` files

### Example asset entry

```json
{
  "id": "asset_d22184a7",
  "kind": "image",
  "mimeType": "image/webp",
  "path": "assets/asset_d22184a7.webp",
  "width": 860,
  "height": 464,
  "source": {
    "originalFileName": "{D22184A7-A84F-4A97-8D23-77C544C9E952}.png"
  }
}
```

## `manifest.json` Requirements

The package manifest should describe:

- package format version
- test identity
- test version
- package creation time
- asset inventory
- hashes
- optional source information

### Suggested shape

```json
{
  "packageFormat": "cctest",
  "packageFormatVersion": 1,
  "createdAtUtc": "2026-04-13T12:00:00Z",
  "test": {
    "publicId": "test_algorithms_5_grade",
    "version": 3,
    "title": "Тема 5. Алгоритми та програми"
  },
  "files": [
    {
      "path": "definition.json",
      "sha256": "abc123..."
    },
    {
      "path": "assets/asset_d22184a7.webp",
      "sha256": "def456..."
    }
  ],
  "source": {
    "kind": "mytest-xml-import",
    "includedFiles": [
      "imports/original.xml"
    ]
  }
}
```

## Integrity Rules

Version 1 should support integrity verification by SHA-256 hashes.

Recommended rules:

- every packaged file listed in `manifest.json`
- every listed file has SHA-256
- import validates listed file hashes before accepting the package

## Signing

Digital signing is optional for version 1.

Reserved future direction:

- package-level signature
- signer metadata
- trust policy for import

## Export Rules

When exporting a test to `.cctest`:

1. Read canonical `test-definition`
2. Collect all referenced assets
3. Normalize every image asset to `webp`
4. Rewrite asset paths in `definition.json` to packaged relative paths
5. Generate `manifest.json`
6. Optionally include import source files
7. Build ZIP archive
8. Save with `.cctest` extension

## Import Rules

When importing a `.cctest` package:

1. Open archive
2. Validate required files exist
3. Validate `manifest.json`
4. Validate file hashes
5. Load `definition.json`
6. Validate asset references
7. Copy assets into local storage
8. Store canonical definition in operational storage
9. Optionally preserve source import artifacts

## Relationship To Operational Storage

`.cctest` is a transport format, not the operational database format.

Recommended split:

- operational storage: `SQLite + external files`
- portable storage: `.cctest`

This allows:

- normal querying and reporting in the server
- easy import/export for users and integrations

## Relationship To Sync

The package format is useful for:

- manual transfer
- bulk import/export
- backup-style exchange
- CMS-side import

But normal system sync should still prefer API-based object synchronization.

Recommended interpretation:

- `.cctest` is for packaging and portability
- API is for runtime synchronization

## What Must Not Be In `.cctest`

Version 1 `.cctest` packages should not include:

- assignment records
- attempt history
- student answers
- result history
- class roster data

Those belong to operational or reporting data, not to the reusable test package itself.

## Future Package Types

If needed later, separate portable formats can be added:

- `.cctest` for one test definition
- `.ccbundle` for a bundle of tests
- `.ccresults` for exported results

Version 1 only standardizes:

- `.cctest`

## Example Minimal Package

```text
sample.cctest
  manifest.json
  definition.json
```

## Example Full Package

```text
sample.cctest
  manifest.json
  definition.json
  assets/
    asset_001.webp
    asset_002.webp
  imports/
    original.xml
```

## Recommended Next Step

After this package format document, the next implementation-facing step should be:

1. Add package import/export DTOs and helpers
2. Extend the converter and storage pipeline to emit canonical `definition.json`
3. Implement `.cctest` export from local operational storage
4. Implement `.cctest` import into the local operational storage
