"""Counter-disabled control with finite process lifetime and owned cleanup."""
import subprocess,sys
from pathlib import Path
subprocess.run([sys.executable,str(Path(__file__).with_name('measure.py')),'--preflight'],check=True,timeout=40)
