import csv
import hashlib
import json
import logging
import os
import sys
from collections import defaultdict
from pathlib import Path

WARMUP_DEFAULT = 5

logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")


def parse_int(value):
    if value is None or value == "":
        return None
    try:
        return int(float(value))
    except ValueError:
        return None


def parse_float(value):
    if value is None or value == "":
        return 0.0
    try:
        return float(value)
    except ValueError:
        try:
            return float(value.replace(",", "."))
        except ValueError:
            return 0.0


def canonical_json(obj):
    return json.dumps(obj, separators=(",", ":"), sort_keys=True, ensure_ascii=False)


def compute_hash(obj):
    payload = canonical_json(obj).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def read_adapter_calls(path, warnings):
    with open(path, newline="", encoding="utf-8-sig") as fh:
    reader = csv.DictReader(fh)
        has_index = "session_index" in reader.fieldnames if reader.fieldnames else False
        has_warmup = "warmup" in reader.fieldnames if reader.fieldnames else False
        has_net = "net_ms" in reader.fieldnames if reader.fieldnames else False
        has_local = "local_ms" in reader.fieldnames if reader.fieldnames else False
        has_decision = "decision_build_ms" in reader.fieldnames if reader.fieldnames else False
        rows = {}
        duplicates = set()
        for row in reader:
            session_id = row.get("session_id", "").strip()
            if not session_id:
                continue
            idx = parse_int(row.get("session_index")) if has_index else None
            if idx is None:
                idx = infer_index(session_id)
                warnings.add(f"Inferred session_index for {session_id} in {path}")
            warmup = parse_warmup(row.get("warmup"), idx, has_warmup, warnings, path)
            call_ms = parse_float(row.get("call_ms"))
            adapter = row.get("adapter", "").strip()
            net_ms = parse_float(row.get("net_ms")) if has_net else 0.0
            local_ms = parse_float(row.get("local_ms")) if has_local else 0.0
            decision_ms = parse_float(row.get("decision_build_ms")) if has_decision else 0.0
            entry = rows.get(session_id)
            if entry:
                if call_ms < entry["adapter_ms"]:
                    entry.update(
                        {
                            "session_index": idx,
                            "warmup": warmup,
                            "adapter_name": adapter,
                            "adapter_ms": call_ms,
                            "net_ms": net_ms,
                            "local_ms": local_ms,
                            "decision_ms": decision_ms,
                        }
                    )
                duplicates.add(session_id)
            else:
                rows[session_id] = {
                    "session_id": session_id,
                    "session_index": idx,
                    "warmup": warmup,
                    "adapter_name": adapter,
                    "adapter_ms": call_ms,
                    "net_ms": net_ms,
                    "local_ms": local_ms,
                    "decision_ms": decision_ms,
                }
        for dup in duplicates:
            warnings.add(f"Multiple adapter_calls for {dup} in {path}; kept min call_ms")
        return rows


def parse_warmup(value, session_index, has_column, warnings, path):
    if has_column and value is not None:
        val = value.strip().lower()
        if val in ("1", "true", "yes", "y"):
            return 1
        if val in ("0", "false", "no", "n"):
            return 0
        try:
            return int(float(val))
        except ValueError:
            pass
    warnings.add(f"Inferred warmup for session_index {session_index} ({path})")
    return 1 if session_index < WARMUP_DEFAULT else 0


def infer_index(session_id):
    digits = "".join(ch for ch in session_id if ch.isdigit())
    if digits:
        return int(digits)
    return 0


def read_scene_transitions(path, warnings):
    if not path.exists():
        warnings.add(f"Missing scene_transitions.csv at {path}")
        return {}

    data = {}
    with open(path, newline="", encoding="utf-8-sig") as fh:
        reader = csv.DictReader(fh)
        for row in reader:
            session_id = row.get("session_id", "").strip()
            if not session_id:
                continue
            from_scene = row.get("from_scene", "").strip()
            to_scene = row.get("to_scene", "").strip()
            transition_ms = parse_float(row.get("transition_ms"))
            data[session_id] = {
                "from_scene": from_scene,
                "to_scene": to_scene,
                "scene_ms": transition_ms,
            }
    return data


def read_audit(path, warnings):
    result = {}
    if not path.exists():
        warnings.add(f"Missing audit.jsonl at {path}")
        return result

    with open(path, "r", encoding="utf-8-sig") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                record = json.loads(line)
            except json.JSONDecodeError:
                warnings.add(f"Skipping invalid audit line in {path}")
                continue
            session_id = record.get("session_id")
            if not session_id:
                continue
            config_hash = record.get("config_version_hash", "")
            seed = (
                record.get("inputs", {}).get("seed")
                or record.get("seed")
                or ""
            )
            decision = record.get("output") or record.get("dk") or {}
            decision_hash = compute_hash(decision) if decision else ""
            result[session_id] = {
                "config_hash": config_hash,
                "seed": seed,
                "decision_hash": decision_hash,
            }
    return result


def row_session_index(row):
    return row.get("session_index", 0)


def main():
    root = Path("Experiments/out")
    if not root.exists():
        logging.error("Experiments/out directory does not exist")
        sys.exit(1)

    warnings = set()
    records = []
    arch_values = set()
    for arch_dir in sorted(root.iterdir()):
        if not arch_dir.is_dir():
            continue
        arch = arch_dir.name
        for run_dir in sorted(arch_dir.iterdir()):
            if not run_dir.is_dir():
                continue
            run_ts = run_dir.name
            for trial_dir in sorted(run_dir.iterdir()):
                if not trial_dir.is_dir():
                    continue
                trial_id = trial_dir.name
                adapter_path = trial_dir / "adapter_calls.csv"
                if not adapter_path.exists():
                    continue
                scene_path = trial_dir / "scene_transitions.csv"
                audit_path = trial_dir / "audit.jsonl"

                adapter_rows = read_adapter_calls(adapter_path, warnings)
                scene_rows = read_scene_transitions(scene_path, warnings)
                audit_rows = read_audit(audit_path, warnings)

                for session_id, adapter in adapter_rows.items():
                    scene = scene_rows.get(session_id, {})
                    has_scene = 1 if session_id in scene_rows else 0
                    has_audit = 1 if session_id in audit_rows else 0
                    audit_meta = audit_rows.get(session_id, {})

                    record = {
                        "arch": arch,
                        "run_ts": run_ts,
                        "trial_id": trial_id,
                        "session_id": session_id,
                        "session_index": adapter["session_index"],
                        "warmup": adapter["warmup"],
                    "adapter_name": adapter["adapter_name"],
                    "adapter_ms": adapter["adapter_ms"],
                    "net_ms": adapter.get("net_ms", 0.0),
                    "local_ms": adapter.get("local_ms", 0.0),
                    "decision_ms": adapter.get("decision_ms", 0.0),
                        "from_scene": scene.get("from_scene", ""),
                        "to_scene": scene.get("to_scene", ""),
                        "scene_ms": scene.get("scene_ms", 0.0),
                        "has_scene": has_scene,
                        "has_audit": has_audit,
                        "config_version_hash": audit_meta.get("config_hash", ""),
                        "seed": audit_meta.get("seed", ""),
                        "decision_hash": audit_meta.get("decision_hash", ""),
                        "adapter_calls_path": os.path.relpath(adapter_path, "."),
                        "scene_transitions_path": os.path.relpath(scene_path, ".") if scene_path.exists() else "",
                        "audit_path": os.path.relpath(audit_path, ".") if audit_path.exists() else "",
                    }
                    records.append(record)
                arch_values.add(arch)

    records.sort(
        key=lambda row: (
            row["arch"],
            row["run_ts"],
            row["trial_id"],
            row["session_index"],
        )
    )

    output_path = Path("Experiments/all_sessions.csv")
    columns = [
        "arch",
        "run_ts",
        "trial_id",
        "session_id",
        "session_index",
        "warmup",
        "adapter_name",
        "adapter_ms",
        "net_ms",
        "local_ms",
        "decision_build_ms",
        "from_scene",
        "to_scene",
        "scene_ms",
        "has_scene",
        "has_audit",
        "config_version_hash",
        "seed",
        "decision_hash",
        "adapter_calls_path",
        "scene_transitions_path",
        "audit_path",
    ]

    with open(output_path, "w", newline="", encoding="utf-8") as fh:
        writer = csv.writer(fh)
        writer.writerow(columns)
        for row in records:
            writer.writerow([row[col] for col in columns])

    print(f"Wrote {len(records)} rows to {output_path}")
    print(f"Distinct arch values: {', '.join(sorted(arch_values))}")
    if warnings:
        print("Warnings:")
        for warning in sorted(warnings):
            print(f"- {warning}")


if __name__ == "__main__":
    main()
