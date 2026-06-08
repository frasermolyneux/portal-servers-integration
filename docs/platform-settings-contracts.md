# Platform Settings Contracts

This document describes the current settings parsing model in `portal-servers-integration`.

## Architecture

- Typed contract and validator source: `XtremeIdiots.Portal.Settings.Contracts.V1`.
- File transport and RCON resolvers consume typed documents for:
  - `ftp`
  - `sftp`
  - `rcon`
- Resolver behavior is fail-closed when payloads are invalid or schema versions are unsupported.
- Repository transport remains dynamic (`namespace + JSON string`) and is resolved through typed parser boundaries in this repo.

## Migration Summary

- Old approach: resolver code extracted values with raw JSON-path assumptions.
- New approach: resolver code deserializes typed contracts and validates before use.
- Compatibility behavior for supported legacy schema versions is handled by contract validators.

## Troubleshooting Runbook

1. Resolver returns no credentials unexpectedly.
   - Confirm namespace payload exists and is non-empty.
   - Confirm payload passes typed validation for the configured schema version.

2. FTP/SFTP mode selection appears incorrect.
   - Verify file-transport namespace payload and selected mode.
   - Run resolver fixture tests to validate precedence and fallback behavior.

3. Unexpected production mismatch after deployment.
   - Verify contracts package version parity with upstream/downstream settings consumers.
