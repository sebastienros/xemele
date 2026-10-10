"""Bounded persistent JSON-lines child; always reaps its process and group."""
import asyncio, json, os, signal
MAXIMUM=1048576
class Child:
    def __init__(self, process):
        self.process=process;self.stderr_bytes=0;self.stderr_preview=bytearray()
        self.stderr_task=asyncio.create_task(self._stderr())
    @classmethod
    async def start(cls, command):
        return cls(await asyncio.create_subprocess_exec(*command,stdin=asyncio.subprocess.PIPE,
            stdout=asyncio.subprocess.PIPE,stderr=asyncio.subprocess.PIPE,start_new_session=True,limit=MAXIMUM+2))
    async def _stderr(self):
        while chunk:=await self.process.stderr.read(4096):
            self.stderr_bytes+=len(chunk)
            self.stderr_preview.extend(chunk[:max(0,512-len(self.stderr_preview))])
    async def exchange(self, request, timeout=10):
        if len(request.encode('utf-8'))>MAXIMUM: raise ValueError('request wire limit')
        async def operation():
            self.process.stdin.write(request.encode('utf-8')+b'\n');await self.process.stdin.drain()
            data=await self.process.stdout.readline()
            if not data.endswith(b'\n') or len(data)>MAXIMUM: raise ValueError('missing/oversized/unterminated child response')
            if self.stderr_bytes>65536: raise ValueError('child stderr limit')
            return json.loads(data.decode('utf-8',errors='strict'))
        try: return await asyncio.wait_for(operation(),timeout)
        except (asyncio.TimeoutError,ValueError,BrokenPipeError,ConnectionResetError) as error:
            raise RuntimeError(f'child {self.process.pid} exchange failed: {type(error).__name__}; stderr={bytes(self.stderr_preview).decode("utf-8",errors="replace")!r}') from error
    async def close(self, require_success):
        process=self.process
        try:
            if process.stdin: process.stdin.close()
            try: await asyncio.wait_for(process.wait(),2)
            except asyncio.TimeoutError:
                self._kill_group(signal.SIGTERM)
                try: await asyncio.wait_for(process.wait(),1)
                except asyncio.TimeoutError: self._kill_group(signal.SIGKILL);await process.wait()
            # Reap the leader, and kill descendants even if the leader exited.
            self._kill_group(signal.SIGKILL)
            await asyncio.wait_for(self.stderr_task,2)
            remainder=await asyncio.wait_for(process.stdout.read(MAXIMUM+1),2)
            if require_success and (process.returncode!=0 or remainder or self.stderr_bytes>65536):
                raise RuntimeError(f'child exit contract failed: code={process.returncode}, extra_stdout={len(remainder)}, stderr_bytes={self.stderr_bytes}, stderr={bytes(self.stderr_preview).decode("utf-8",errors="replace")!r}')
        finally:
            self._kill_group(signal.SIGKILL)
            if process.returncode is None: await process.wait()
            if not self.stderr_task.done(): self.stderr_task.cancel()
            try: os.kill(process.pid,0)
            except ProcessLookupError: pass
            else: raise RuntimeError('child survived cleanup')
    def _kill_group(self, sig):
        try: os.killpg(self.process.pid,sig)
        except ProcessLookupError: pass
