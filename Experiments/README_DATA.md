# Data utilities

## All sessions aggregator

- **Script:** `python .\Experiments\make_all_sessions.py`
- **Output:** `Experiments/all_sessions.csv` (UTF-8 without BOM)
- **Purpose:** Collapse every trial (adapter calls + scene transitions, optional audits) into one row per session for downstream analysis.

### Columns (in order):
1. `arch` – experiment architecture folder (e.g., `B1`, `E3`)
2. `run_ts` – timestamp of each overall run directory under `Experiments/out`
3. `trial_id` – trial folder name (`trial_0000`, etc.)
4. `session_id` – unique session identifier
5. `session_index` – numeric index (parsed or inferred from `session_id`)
6. `warmup` – 1 if session was a warmup entry; otherwise 0. When the original file lacks the column, sessions with `session_index < 5` are treated as warmup.
7. `adapter_name` – adapter recorded in `adapter_calls.csv`
8. `adapter_ms` – milliseconds spent in adapter call
9. `from_scene` – scene transition source (`scene_transitions.csv`)
10. `to_scene` – scene transition target
11. `scene_ms` – transition duration in milliseconds
12. `has_scene` – 1 when transition row exists, 0 otherwise
13. `has_audit` – 1 when `audit.jsonl` entry exists for that session
14. `config_version_hash` – `config_version_hash` from audit records
15. `seed` – audit-record seed (taken from `inputs.seed` when available)
16. `decision_hash` – SHA-256 of the canonical JSON of the audit output object
17. `adapter_calls_path` – relative path to the source `adapter_calls.csv`
18. `scene_transitions_path` – relative path to the source `scene_transitions.csv` (empty if missing)
19. `audit_path` – relative path to the source `audit.jsonl` (empty if missing)

### Notes
- The script walks `Experiments/out` recursively, handles optional files gracefully, and warns when it infers indices or warmups or encounters duplicate adapter entries.
- Warmup inference falls back to `session_index < 5` when the `warmup` column is absent.
