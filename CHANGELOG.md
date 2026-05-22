# Changelog

All notable changes to `Tamp.SqlPackage` are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] — Unreleased

### Added

- Initial release. Typed wrappers for `sqlpackage` over the three highest-leverage verbs:
  - `Publish` (`/Action:Publish`) — apply a `.dacpac` to a target database.
  - `Extract` (`/Action:Extract`) — produce a `.dacpac` from a live database.
  - `Script` (`/Action:Script`) — produce a deployment T-SQL script without applying.
- Connection strings tracked as `Secret` and redacted from logs / plan emission.
- Optional Azure AD `/AccessToken:` support, also tracked as `Secret`.
- `/p:` action properties and `/v:` SqlCmd variables exposed via `SetProperty` / `SetVariable`.
- Standard knobs: `Profile`, `OverwriteFiles`, `Quiet`, `Diagnostics`, `DiagnosticsFile`.
- Parallel fluent + object-init authoring surface.
- Multi-target `net8.0;net9.0;net10.0`.
