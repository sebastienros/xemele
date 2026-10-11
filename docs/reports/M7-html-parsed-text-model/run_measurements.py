import pathlib
root=pathlib.Path(__file__).resolve().parents[3]
source=root/'docs/reports/M7-html-model/run_measurements.py'
exec(compile(source.read_text().replace('M7-html-model','M7-html-parsed-text-model'),str(source),'exec'))
