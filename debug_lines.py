from pathlib import Path
path=Path('Experiments/out/B1/20260202_163146/trial_0000/adapter_calls.csv')
print('lines', sum(1 for _ in path.open()))
with path.open() as fh:
    for i in range(5):
        print(repr(fh.readline()))
