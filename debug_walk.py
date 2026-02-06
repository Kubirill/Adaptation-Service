from pathlib import Path
root=Path('Experiments/out')
for arch_dir in sorted(root.iterdir()):
    if not arch_dir.is_dir():
        continue
    for run_dir in sorted(arch_dir.iterdir()):
        if not run_dir.is_dir():
            continue
        for trial_dir in sorted(run_dir.iterdir()):
            if not trial_dir.is_dir():
                continue
            adapter_path=trial_dir/'adapter_calls.csv'
            print('check', arch_dir.name, run_dir.name, trial_dir.name, adapter_path.exists())
