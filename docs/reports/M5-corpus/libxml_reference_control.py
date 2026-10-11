"""Python-only injected adapter controls: no reference/runtime processes launched."""
import contextlib,io,json,pathlib,subprocess,tempfile,unittest
import libxml_reference as adapter

class InfrastructureControls(unittest.TestCase):
    def exercise(self,failure,version_failure=False):
        rows=[{'id':str(i),'mode':1,'xml':'<a/>','expected':'<a></a>','expectedUtf8Sha256':'pinned'} for i in range(6)]
        calls=[]
        def fake(command,**kwargs):
            calls.append(command)
            if command[-1]=='--version':
                if version_failure:raise failure
                return subprocess.CompletedProcess(command,0,stderr='injected version')
            if len(calls)==1 and not version_failure:raise failure
            return subprocess.CompletedProcess(command,0,stdout=b'<a></a>',stderr=b'')
        with tempfile.TemporaryDirectory(prefix='m5-corpus-libxml-control-') as folder:
            path=pathlib.Path(folder)/'result.json'
            with contextlib.redirect_stdout(io.StringIO()):
                status=adapter.main(rows,path,fake,lambda:'injected binary identity')
            report=json.loads(path.read_text())
        self.assertEqual(1,status)
        self.assertEqual(6,len(report['rows']))
        self.assertEqual([str(i) for i in range(6)],[r['id'] for r in report['rows']])
        self.assertEqual(7,len(calls))
        if version_failure:
            self.assertTrue(all(r['pass'] for r in report['rows']))
            self.assertEqual(type(failure).__name__,report['infrastructureErrors'][0]['type'])
        else:
            self.assertFalse(report['rows'][0]['pass'])
            self.assertEqual(type(failure).__name__,report['rows'][0]['infrastructureError'])
            self.assertTrue(all(r['pass'] for r in report['rows'][1:]))
    def test_vector_timeout_serializes_all_rows(self):self.exercise(subprocess.TimeoutExpired('injected',10))
    def test_vector_oserror_serializes_all_rows(self):self.exercise(OSError('injected executable unavailable'))
    def test_version_timeout_keeps_rows_and_fails(self):self.exercise(subprocess.TimeoutExpired('injected',10),True)
    def test_version_oserror_keeps_rows_and_fails(self):self.exercise(OSError('injected executable unavailable'),True)

if __name__=='__main__':unittest.main()
