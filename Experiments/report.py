import argparse
import json
import os
from collections import defaultdict


def percentile(values, p):
    if not values:
        return None
    values = sorted(values)
    k = (len(values) - 1) * p
    f = int(k)
    c = min(f + 1, len(values) - 1)
    if f == c:
        return values[f]
    return values[f] + (values[c] - values[f]) * (k - f)


def load_csv_values(path, column_name, include_warmup):
    values = []
    with open(path, "r", encoding="utf-8") as handle:
        header = next(handle, None)
        warmup_index = None
        target_index = None
        if header:
            headers = header.strip().split(",")
            if column_name in headers:
                target_index = headers.index(column_name)
            if "warmup" in headers:
                warmup_index = headers.index("warmup")
        for line in handle:
            parts = line.strip().split(",")
            if target_index is None or len(parts) <= target_index:
                continue
            if warmup_index is not None and not include_warmup:
                try:
                    if int(parts[warmup_index]) == 1:
                        continue
                except ValueError:
                    pass
            try:
                values.append(float(parts[target_index]))
            except ValueError:
                continue
    return values


def collect_runs(out_root):
    runs = defaultdict(list)
    for arch in os.listdir(out_root):
        arch_path = os.path.join(out_root, arch)
        if not os.path.isdir(arch_path):
            continue
        for timestamp in os.listdir(arch_path):
            ts_path = os.path.join(arch_path, timestamp)
            if not os.path.isdir(ts_path):
                continue
            for trial in os.listdir(ts_path):
                trial_path = os.path.join(ts_path, trial)
                if not os.path.isdir(trial_path):
                    continue
                runs[arch].append(trial_path)
    return runs


def reproducibility_rate(audit_paths, include_warmup):
    input_to_outputs = defaultdict(set)
    for path in audit_paths:
        if not os.path.exists(path):
            continue
        with open(path, "r", encoding="utf-8-sig") as handle:
            for line in handle:
                line = line.strip()
                if not line:
                    continue
                record = json.loads(line)
                if not include_warmup and record.get("warmup") is True:
                    continue
                event = record.get("event") or record.get("inputs") or {}
                decision = record.get("decision") or record.get("output") or {}
                event_key = json.dumps(
                    {
                        "scene_id": event.get("scene_id"),
                        "result_z": event.get("result_z"),
                        "time_t": event.get("time_t"),
                        "attempts_a": event.get("attempts_a"),
                        "seed": event.get("seed"),
                        "config_version": event.get("config_version"),
                    },
                    sort_keys=True,
                )
                decision_key = json.dumps(decision, sort_keys=True)
                input_to_outputs[event_key].add(decision_key)

    if not input_to_outputs:
        return 0.0

    reproducible = sum(1 for outputs in input_to_outputs.values() if len(outputs) == 1)
    return reproducible / float(len(input_to_outputs))


def summarize_arch(arch_runs, include_warmup):
    adapter_calls = []
    scene_transitions = []
    audit_paths = []

    for run in arch_runs:
        adapter_csv = os.path.join(run, "adapter_calls.csv")
        scene_csv = os.path.join(run, "scene_transitions.csv")
        audit_paths.append(os.path.join(run, "audit.jsonl"))
        if os.path.exists(adapter_csv):
            adapter_calls.extend(load_csv_values(adapter_csv, "call_ms", include_warmup))
        if os.path.exists(scene_csv):
            scene_transitions.extend(load_csv_values(scene_csv, "transition_ms", include_warmup))

    return {
        "adapter_p50": percentile(adapter_calls, 0.50),
        "adapter_p95": percentile(adapter_calls, 0.95),
        "adapter_p99": percentile(adapter_calls, 0.99),
        "scene_p50": percentile(scene_transitions, 0.50),
        "scene_p95": percentile(scene_transitions, 0.95),
        "scene_p99": percentile(scene_transitions, 0.99),
        "repro": reproducibility_rate(audit_paths, include_warmup),
    }


def fmt(value):
    if value is None:
        return "n/a"
    return f"{value:.3f}"


def load_breakdown_values(paths, include_warmup):
    columns = {
        "t_client_serialize_ms": [],
        "t_http_rtt_ms": [],
        "t_server_compute_ms": [],
        "t_client_deserialize_ms": [],
        "t_total_client_ms": [],
    }
    for path in paths:
        if not os.path.exists(path):
            continue
        with open(path, "r", encoding="utf-8") as handle:
            header = next(handle, None)
            if not header:
                continue
            headers = header.strip().split(",")
            idx = {name: headers.index(name) for name in columns.keys() if name in headers}
            warmup_index = headers.index("warmup") if "warmup" in headers else None
            for line in handle:
                parts = line.strip().split(",")
                if warmup_index is not None and not include_warmup:
                    try:
                        if int(parts[warmup_index]) == 1:
                            continue
                    except (ValueError, IndexError):
                        pass
                for name, values in columns.items():
                    col_index = idx.get(name)
                    if col_index is None or len(parts) <= col_index:
                        continue
                    try:
                        values.append(float(parts[col_index]))
                    except ValueError:
                        continue
    return columns


def write_breakdown(out_root, include_warmup):
    runs = collect_runs(out_root)
    b2_runs = runs.get("B2", [])
    breakdown_paths = [os.path.join(run, "b2_breakdown.csv") for run in b2_runs]
    values = load_breakdown_values(breakdown_paths, include_warmup)

    summary = {
        name: {
            "p50": percentile(vals, 0.50),
            "p95": percentile(vals, 0.95),
            "p99": percentile(vals, 0.99),
        }
        for name, vals in values.items()
    }

    md_lines = [
        "| Component | p50 | p95 | p99 |",
        "| --- | --- | --- | --- |",
    ]
    csv_lines = ["component,p50,p95,p99"]
    for name in sorted(summary.keys()):
        md_lines.append(
            "| {component} | {p50} | {p95} | {p99} |".format(
                component=name,
                p50=fmt(summary[name]["p50"]),
                p95=fmt(summary[name]["p95"]),
                p99=fmt(summary[name]["p99"]),
            )
        )
        csv_lines.append(
            "{component},{p50},{p95},{p99}".format(
                component=name,
                p50=fmt(summary[name]["p50"]),
                p95=fmt(summary[name]["p95"]),
                p99=fmt(summary[name]["p99"]),
            )
        )

    summary_md = os.path.join(out_root, "summary_breakdown.md")
    summary_csv = os.path.join(out_root, "summary_breakdown.csv")
    with open(summary_md, "w", encoding="utf-8") as handle:
        handle.write("\n".join(md_lines))
    with open(summary_csv, "w", encoding="utf-8") as handle:
        handle.write("\n".join(csv_lines))
    print(f"Wrote {summary_md}")
    print(f"Wrote {summary_csv}")


def main():
    parser = argparse.ArgumentParser(description="Summarize adaptation experiment outputs.")
    parser.add_argument("--input", default=os.path.join("Experiments", "out"))
    parser.add_argument("--output", default=os.path.join("Experiments", "out", "summary.md"))
    parser.add_argument("--include-warmup", action="store_true")
    args = parser.parse_args()

    runs = collect_runs(args.input)
    lines = [
        "| Arch | Adapter p50 | Adapter p95 | Adapter p99 | Scene p50 | Scene p95 | Scene p99 | Reproducibility |",
        "| --- | --- | --- | --- | --- | --- | --- | --- |",
    ]

    for arch in sorted(runs.keys()):
        summary = summarize_arch(runs[arch], args.include_warmup)
        lines.append(
            "| {arch} | {a50} | {a95} | {a99} | {s50} | {s95} | {s99} | {repro:.3f} |".format(
                arch=arch,
                a50=fmt(summary["adapter_p50"]),
                a95=fmt(summary["adapter_p95"]),
                a99=fmt(summary["adapter_p99"]),
                s50=fmt(summary["scene_p50"]),
                s95=fmt(summary["scene_p95"]),
                s99=fmt(summary["scene_p99"]),
                repro=summary["repro"],
            )
        )

    os.makedirs(os.path.dirname(args.output), exist_ok=True)
    with open(args.output, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines))

    print(f"Wrote {args.output}")

    write_breakdown(args.input, args.include_warmup)


if __name__ == "__main__":
    main()
