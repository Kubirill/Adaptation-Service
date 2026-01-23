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


def load_csv_values(path, column_index):
    values = []
    with open(path, "r", encoding="utf-8") as handle:
        next(handle, None)
        for line in handle:
            parts = line.strip().split(",")
            if len(parts) <= column_index:
                continue
            try:
                values.append(float(parts[column_index]))
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


def reproducibility_rate(audit_paths):
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
                event = record.get("event", {})
                decision = record.get("decision", {})
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


def summarize_arch(arch_runs):
    adapter_calls = []
    scene_transitions = []
    audit_paths = []

    for run in arch_runs:
        adapter_csv = os.path.join(run, "adapter_calls.csv")
        scene_csv = os.path.join(run, "scene_transitions.csv")
        audit_paths.append(os.path.join(run, "audit.jsonl"))
        if os.path.exists(adapter_csv):
            adapter_calls.extend(load_csv_values(adapter_csv, 2))
        if os.path.exists(scene_csv):
            scene_transitions.extend(load_csv_values(scene_csv, 3))

    return {
        "adapter_p50": percentile(adapter_calls, 0.50),
        "adapter_p95": percentile(adapter_calls, 0.95),
        "adapter_p99": percentile(adapter_calls, 0.99),
        "scene_p50": percentile(scene_transitions, 0.50),
        "scene_p95": percentile(scene_transitions, 0.95),
        "scene_p99": percentile(scene_transitions, 0.99),
        "repro": reproducibility_rate(audit_paths),
    }


def fmt(value):
    if value is None:
        return "n/a"
    return f"{value:.3f}"


def main():
    parser = argparse.ArgumentParser(description="Summarize adaptation experiment outputs.")
    parser.add_argument("--input", default=os.path.join("Experiments", "out"))
    parser.add_argument("--output", default=os.path.join("Experiments", "out", "summary.md"))
    args = parser.parse_args()

    runs = collect_runs(args.input)
    lines = [
        "| Arch | Adapter p50 | Adapter p95 | Adapter p99 | Scene p50 | Scene p95 | Scene p99 | Reproducibility |",
        "| --- | --- | --- | --- | --- | --- | --- | --- |",
    ]

    for arch in sorted(runs.keys()):
        summary = summarize_arch(runs[arch])
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


if __name__ == "__main__":
    main()
