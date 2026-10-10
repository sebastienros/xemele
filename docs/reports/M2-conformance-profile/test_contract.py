import asyncio, json, os, sys, unittest
from audit import model
from child import Child
from classify import build, used_sources

START={'kind':'start','namespaceUri':'','localName':'r','attributes':[]}
END={'kind':'end','namespaceUri':'','localName':'r'}
class ModelControls(unittest.TestCase):
    def test_authored_effective10_normalization(self):
        events=[{'kind':'text','value':'\r\n '},START,{'kind':'text','value':'a\u0085'}, {'kind':'text','value':'\u2028\r'},END,{'kind':'text','value':'\n'}]
        self.assertEqual(model(events),[START,{'kind':'text','value':'a\u0085\u2028\r'},END])
    def test_malformed_unbalanced_multiple_root_controls(self):
        for events in ([END,START],[START,dict(END,localName='other')],[START,END,START,END],[START],[],[START,{'kind':'text','value':7},END],[dict(START,attributes=[{'localName':'a'}]),END],[dict(START,localName=''),END],[START,END,{'kind':'text','value':'outside'}]):
            with self.subTest(events=events),self.assertRaises(ValueError): model(events)

class ClassificationControls(unittest.TestCase):
    def test_exact_malformed_primary_and_used_text_versions(self):
        rows={c['id']:c for c in build()['cases']}
        for sid,digest in [('rmt-008','0a0e7302bc673ddc538024e6969a6642b6afcb89e8823432856f24170632b5ae'),('rmt-009','040685907d72d1c656d291f9f8a86cef1701054c7b1470301475859f831d6fd8')]:
            row=rows['xmlconf/eduni/xml-1.1/xml11.xml#'+sid]
            self.assertEqual(row['inputSha256'],digest)
            self.assertEqual(row['expectedOutcomes'],['rejected'])
            self.assertEqual(row['rule'],'version-production26')
            self.assertTrue(any(v['version']=='42.88' for v in row['versionEvidence']))
        external=rows['xmlconf/eduni/xml-1.1/xml11.xml#rmt-009']['versionEvidence'][1]
        self.assertEqual(external['sha256'],'718d160c3348d2360097c40428752f13ef5af6704009c238c7a9ffec93759577')
        row=rows['xmlconf/eduni/xml-1.1/xml11.xml#rmt-014']
        self.assertEqual(row['expectedOutcomes'],['rejected'])
        self.assertFalse(row['historicalNameEvidence'][0]['fifthEditionName'])
    def test_granted_unused_unparsed_and_comment_resources_are_not_read_witnesses(self):
        files={p:{'sha256':'0'*64} for p in ('main.xml','bad.ent')}
        declarations="<!DOCTYPE r [<!ENTITY unused SYSTEM 'bad.ent'><!ENTITY unparsed SYSTEM 'bad.ent' NDATA n><!NOTATION n SYSTEM 'bad.ent'>]>"
        for content in ('<r/>','<r><!--&unused;--><![CDATA[&unused;]]></r>'):
            self.assertEqual(set(used_sources('main.xml',{'main.xml':declarations+content},files)),{'main.xml'})
        used=used_sources('main.xml',{'main.xml':declarations+'<r>&unused;</r>'},files)
        self.assertEqual(set(used),{'main.xml','bad.ent'})
        self.assertEqual(used['bad.ent']['kind'],'general-entity-content-reference')

class ProcessControls(unittest.IsolatedAsyncioTestCase):
    async def test_protocol_utf8_unterminated_and_exit_controls(self):
        scripts=["import sys;sys.stdin.readline();print('not json')", "import sys;sys.stdin.readline();sys.stdout.buffer.write(b'\\xff\\n');sys.stdout.flush()", "import sys;sys.stdin.readline();sys.stdout.write('{}');sys.stdout.flush()"]
        for script in scripts:
            child=await Child.start([sys.executable,'-c',script])
            try:
                with self.assertRaises((RuntimeError,UnicodeError)): await child.exchange('{}',1)
            finally: await child.close(False)
        child=await Child.start([sys.executable,'-c',"import sys;sys.stdin.readline();print('{}',flush=True);sys.exit(7)"])
        await child.exchange('{}',1)
        with self.assertRaises(RuntimeError): await child.close(True)
    async def test_timeout_reaps_leader_and_descendant(self):
        child=await Child.start([sys.executable,'-c',"import subprocess,sys,time;p=subprocess.Popen([sys.executable,'-c','import time;time.sleep(60)']);print(p.pid,file=sys.stderr,flush=True);time.sleep(60)"])
        try:
            with self.assertRaises(RuntimeError): await child.exchange('{}',.1)
        finally: await child.close(False)
        pid=int(bytes(child.stderr_preview).strip())
        for _ in range(20):
            try: os.kill(pid,0)
            except ProcessLookupError: return
            await asyncio.sleep(.05)
        self.fail('descendant survived process-group cleanup')
    async def test_one_failure_cleans_both_children(self):
        children=[await Child.start([sys.executable,'-c',script]) for script in ("import sys;sys.stdin.readline();print('invalid')", "import time;time.sleep(60)")]
        try:
            with self.assertRaises(RuntimeError): await children[0].exchange('{}',1)
        finally:
            results=await asyncio.gather(*(child.close(False) for child in children),return_exceptions=True)
        self.assertTrue(all(result is None for result in results))
        self.assertTrue(all(child.process.returncode is not None for child in children))
    async def test_extra_stdout_is_failure(self):
        child=await Child.start([sys.executable,'-c',"import sys;sys.stdin.readline();print('{}');print('{}');sys.stdout.flush()"])
        await child.exchange('{}',1)
        with self.assertRaises(RuntimeError): await child.close(True)

if __name__=='__main__': unittest.main()
