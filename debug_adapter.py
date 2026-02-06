import csv
from pathlib import Path
from Experiments.make_all_sessions import parse_int, parse_warmup, parse_float, infer_index

def read_adapter_calls(path):
    with open(path, newline="", encoding="utf-8") as fh:
        reader=csv.DictReader(fh)
        has_index="session_index" in reader.fieldnames if reader.fieldnames else False
        rows={}
        duplicates=set()
        for row in reader:
            session_id=row.get("session_id",
                                 "").strip()
            if not session_id:
                continue
            idx=parse_int(row.get("session_index")) if has_index else None
            if idx is None:
                idx=infer_index(session_id)
            warmup=parse_warmup(row.get("warmup"), idx, has_index, set(), path)
            entry=rows.get(session_id)
            call_ms=float(row.get("call_ms") or 0)
            adapter=row.get("adapter","").strip()
            if entry:
                if call_ms<entry["adapter_ms"]:
                    entry.update({
                        "session_index": idx,
                        "warmup": warmup,
                        "adapter_name": adapter,
                        "adapter_ms": call_ms,
                    })
                duplicates.add(session_id)
            else:
                rows[session_id]={
                    "session_id": session_id,
                    "session_index": idx,
                    "warmup": warmup,
                    "adapter_name": adapter,
                    "adapter_ms": call_ms,
                }
    return rows

path=Path('Experiments/out/B1/20260202_163146/trial_0000/adapter_calls.csv')
rows=read_adapter_calls(path)
print(len(rows))
print(list(rows.values())[:3])
