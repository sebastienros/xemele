"""Actual counter-disabled control and cleanup with the frozen lease supervisor."""
import subprocess,sys
from pathlib import Path
subprocess.run([sys.executable,str(Path(__file__).with_name('measure.py')),'--preflight'],check=True)
