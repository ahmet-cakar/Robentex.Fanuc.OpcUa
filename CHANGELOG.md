# Changelog

All notable changes to **Robentex.Fanuc.OpcUa** should be recorded here, grouped by published version. Do not assign a feature to a release until verified against its actual package or Git tag.

## [1.0.2] - 2026-10-16

### Added
- Read-only FANUC STRING system-variable support through `ReadSystemVariableAsync<string>`.
- 40-register dynamic mapping with `MULTIPLY=1`, low-byte-first decoding, and NUL terminator validation.

### Verified
- STRING read tested using `$RBTIF.SERIAL_NUM` with ROBOGUIDE.

### Known limitations
- STRING writes are unsupported.

## [1.0.1] - 2026-10-15

_Release-specific changes must be verified against the 1.0.1 package or source tag before publishing._

## [1.0.0] - 2026-10-15

_Initial published version. Feature inventory should be confirmed against the 1.0.0 package before publishing._
