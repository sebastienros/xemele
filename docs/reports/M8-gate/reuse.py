"""Read-only verified-evidence/source-equivalence checks after preparation; no corpus or counters."""
import sys
sys.dont_write_bytecode=True
import hashlib,json,subprocess,time,os
from pathlib import Path
from recipes import ROOT,HERE

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def cli_source_identity():
 groups=['src/Xemele','src/Xemele.Tool','tests/Xemele.Tests/M8-cli','tests/Xemele.Conformance.Tests/SchemaIntegration','tests/Xemele.Fuzz/Validation','docs/reports/M8-cli','samples/M8-cli','docs/api','docs/reports/M0-contracts/ApiCheck']
 files=[p for base in groups for p in (ROOT/base).rglob('*') if p.is_file() and not {'bin','obj','__pycache__'}.intersection(p.relative_to(ROOT).parts)
        and p.name not in ['prepared.json','results.json','handoff.md'] and not p.name.endswith('.receipt.json')]
 files += [ROOT/name for name in ['global.json','Directory.Build.props','Directory.Packages.props','docs/reports/M8-xsd-corpus/supervision.py','docs/reports/M8-xsd-corpus/nested_ownership.py','docs/reports/M5-validation-model/check_api.py','tools/planning/tasks.py']]
 rows={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))}
 return {'files':len(rows),'sha256':hashlib.sha256(json.dumps(rows,sort_keys=True).encode()).hexdigest()}

def git_equivalence(revision,paths,allowed=()):
 end=float(os.environ['XEMELE_GATE_WORK_DEADLINE'])
 clean=subprocess.run(['/usr/bin/git','status','--porcelain','--',*paths],cwd=ROOT,capture_output=True,text=True,timeout=min(20,max(.001,end-time.monotonic())))
 assert clean.returncode==0 and not clean.stdout, 'Scoped actual worktree must be clean, including unexpected untracked files'
 result=subprocess.run(['/usr/bin/git','diff','--name-only',revision,'--',*paths],cwd=ROOT,capture_output=True,text=True,timeout=min(20,max(.001,end-time.monotonic())))
 assert result.returncode==0,result.stderr
 changed=result.stdout.splitlines();assert set(changed)<=set(allowed),(revision,changed)
 return {'baseline':revision,'scope':paths,'documentationOnlyDelta':changed,'productOptionsCorpusEquivalent':True}

def main():
 packet=json.loads((HERE/'source-packet.json').read_text());proof=packet['reuse']
 for path,value in proof['recordHashes'].items():assert sha(ROOT/path)==value,(path,'Authoritative reuse record changed')
 cli=json.loads((ROOT/'docs/orchestration/M8-cli-primary-checks.json').read_text())
 assert cli['testedRevision']=='247203d78c7f4ff6d480ed8c24cc5a548d86f103'
 assert cli['actualOuterExit']==0 and cli['result']=='passed' and cli['inputsMatched']
 assert cli_source_identity()==cli['source']=={'files':333,'sha256':'dddbb1e180737e65e827677b665edc3a835fd83f64cf47d9537c65d5daf52ad6'}
 primary_controls=json.loads((ROOT/'docs/orchestration/M8-xsd-corpus-integration-controls.json').read_text())
 boundary=json.loads((ROOT/'docs/orchestration/M8-xsd-corpus-boundary-native-result.json').read_text())
 assert primary_controls['testedRevision']=='932561a4d49f0df10ec8e71a87caf8544c65db68' and primary_controls['result'].startswith('passed')
 assert boundary['testedRevision']=='8626e609a4e45a8e90d8eb57d3ef191a3fdfdec0' and boundary['result']=='passed' and boundary['failed']==boundary['errors']==boundary['skipped']==boundary['notRun']==0
 xsd=json.loads((ROOT/'docs/orchestration/M8-xsd-corpus-repair-campaign.json').read_text())
 supplements=json.loads((ROOT/'docs/orchestration/M8-xsd-corpus-repair-supplements.json').read_text())
 assert xsd['actualChildren']==186 and xsd['actualRows']==83472
 assert xsd['engines']['product']['originalApplicableCounts']=={'passed':39329,'failed':144,'indeterminate':11}
 assert xsd['approvedDispositionsMatched']==155 and xsd['approvedOriginalFalse']==144 and xsd['approvedOriginalNull']==11
 assert xsd['originalPassCreditForDispositions']==xsd['gatePassCreditForDispositions']==0 and not xsd['uncoveredApplicableProductOriginals']
 assert xsd['originalAnnotation']=={'id':'msMeta/Annotations_w3c.xml#g78/s:annotZ002','engine':'product','kind':'schema','expected':'valid','actual':'schema-valid','passed':True,'codes':[]}
 assert xsd['annotationWaiver'] is False
 assert len(supplements['rows'])==5 and all(row['complete'] and row['profilePassed'] and row['originalPassed'] is None and row['countAsOriginalPass'] is False and row['countAsGatePass'] is False for row in supplements['rows'])
 xsd_scope=['src/Xemele','tests/corpora/xsd','tests/fixtures','tests/Xemele.Conformance.Tests/Xsd','tests/Xemele.Differential.Tests/Xsd','docs/reports/M8-xsd-corpus']
 equivalence=git_equivalence('db2e6e2ca193cf5040c2235e73c55bb9c84bfaa6',xsd_scope,['docs/reports/M8-xsd-corpus/final-implementation-handoff.md'])
 disabled=json.loads((ROOT/'docs/reports/M8-xsd-resolved-availability/branch-evidence.json').read_text())
 assert disabled['disabledRoutes']==6 and disabled['consumed']==254
 assert sha(ROOT/'src/Xemele/Validation/Xsd/Compilation/Availability/Availability.cs')==disabled['runtimeSha256']
 print('PASS verified evidence reuse '+json.dumps({'xsd':{'sourceRevision':xsd['sourceRevision'],'branchAttribution':True,'freshGateCampaign':False,'originalProduct':xsd['engines']['product']['originalApplicableCounts'],
  'exactDispositions':155,'originalCredit':0,'gateCredit':0,'annotation':xsd['originalAnnotation'],'separateProfiles':5},
  'sourceEquivalence':equivalence,'cli':{'checkpoint':'247203d78c7f4ff6d480ed8c24cc5a548d86f103','source':cli['source'],'normalAssertions':1156,'xmlOnlyAssertions':1157,'guards':10,'actualCliControls':38,'originalFailures':3,'unixEpipeDeliveryCredit':0},
  'compatibleDisabledEvidence':{'source':'docs/reports/M8-xsd-resolved-availability/branch-evidence.json','routes':6,'consumed':254,'countersCollectionsInvoked':0},
  'inheritedNoEmit':'real+positive only; no invented disabled child','historicalWarmBorrowedFailure':{'expected':0,'actual':7744,'retained':True}},sort_keys=True),flush=True)
if __name__=='__main__':main()
