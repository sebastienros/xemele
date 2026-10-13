"""Correctness/API/consumer/disabled source controls, finite and counter-free."""
import sys
sys.dont_write_bytecode=True
import json
import os
import time
import xml.etree.ElementTree as ET
from pathlib import Path
import bindings as b
from family import Runner,nested,publish_finite
import selection

NAMESPACES=['M8XsdMissingComponents','M8XsdImportVisibility','M8XsdOptionalComposition','M8XsdParticleNormalization','M8XsdTypeRelations',
'M8XsdInstanceHintOrdering','M8XsdContentFailureAssessment','M8XsdCountedMatcher','M8XsdInstanceAssessment','M8XsdAttributeFixed',
'M8SchemaResourceOutcomes','M8XsdRecursiveComponents','M8XsdComponentMapping','M8XsdSchemaSyntax','M8XsdFacade','M8XsdPublicationCheckpoints',
'M8XsdFixedRestriction','M8SchemaResourceBase','M8XsdValidate','M8ConstraintLexical','M8XsdContent','M8XsdCompile','M8XsdIdentity',
'M8XsdResolvedAvailability','M8Facets','M8Model','M8SourceLocations','M8XsdWildcardComposition','M8XsdEffectiveContent','M8Datatypes','M8Regex']
PROJECTS=[('tests','tests/Xemele.Tests/Xemele.Tests.csproj','Xemele.Tests.dll'),
('public','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj','ApiCheck.dll'),
('internal','docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj','ApiCheck.dll'),
('added','docs/reports/M8-xsd-resolved-availability/harness/Missing.Api.csproj','Missing.Api.dll'),
('normal','samples/M8-xsd-resolved-availability/Consumer.csproj','Consumer.dll'),
('xml-only','samples/M8-xsd-resolved-availability/XmlOnly.csproj','XmlOnly.dll'),
('probe','benchmarks/Xemele.Benchmarks/M8-xsd-resolved-availability/Resolved.Probe.csproj','Xemele.Benchmarks.dll')]
NEW_TYPES=['ReferenceKind','SymbolSpace','AvailabilityState','OwnerKind','DependencyUse','ComponentIdentity','DependencyEdge','AvailabilityEntry','ComponentAvailability','AvailabilityBuilder','CompilationEnvelope']


def verified_added_projection():
    proof=b.REPORT/'verified-runtime-api.json';golden=b.REPORT/'verified-added.golden.txt'
    assert b.digest(proof)=='5016c3b603fc99470bfdb2f53cbd500b972a4ce8c68d5469372f62b3fd46b187','Verified API provenance bytes changed'
    provenance=json.loads(proof.read_text())
    assert provenance['tested_integration']=='82ded28c85df6accccee4c798c5b49ff850aa797'
    assert provenance['released_check_sha256']=='c516fdbae022bf65148a2c420b0412fcd4f1418adacde540bbc48eb0dee95105'
    expected_sha='33a4823adf25f638d0e52990918f859c2b64a35359d25aa59c318f4543a1812d'
    assert provenance['canonical_added_api_sha256']==expected_sha and b.digest(golden)==expected_sha
    expected=b.rows(golden)
    assert len(expected)==93 and expected==provenance['canonical_added_projection'],'Verified93 canonical projection changed'
    return expected


def require_verified_added_projection(actual):
    assert actual==verified_added_projection(),'Verified93 added API membership/signature/default/access drift'


def test_summary(path):
    tests=list(ET.parse(path).getroot().iter('test'))
    coverage=selection.validate(tests, json.loads((b.HERE/('test-selection-'+json.loads(b.SOURCE.read_text())['phase']+'.json')).read_text()))
    own=[t for t in tests if t.attrib['type'].startswith('Xemele.Tests.M8XsdMissingComponents.')]
    original=[t for t in own if '.EightOriginalPositiveCasesStayIndividuallyBound' in t.attrib['name']]
    separate=[t for t in own if '.FourOriginalBadInstancesAreSeparateNonassessmentControlsWithZeroPassCredit' in t.attrib['name']]
    negative=[t for t in own if '.OriginalMissingBaseSchemasRemainCompileInvalid' in t.attrib['name']]
    assert len(original)==8 and len(separate)==4 and len(negative)==2
    return dict(coverage=coverage,affectedRows=len(tests),ownedRows=len(own),positiveOriginals=8,nonassessmentControls=4,missingBaseNegatives=2,
                nonassessmentOriginalPassCredit=0,nonassessmentGateCredit=0,identitySha256=b.identity_sha(sorted(t.attrib['id'] for t in tests)))


def main():
    assert sys.argv[1:]==['--root']
    started=time.monotonic();review_sha=os.environ['XEMELE_MISSING_REVIEW_SHA256'];source=b.review(review_sha)
    assert json.loads(selection.EXPECTED.read_text())['selectionNamespaces']==NAMESPACES
    controller=Path(os.environ['XEMELE_MISSING_RUN_DIRECTORY']);summary=None
    with Runner(controller/'checks',580,started=started,cleanup_reserve=40) as runner:
        out=runner.directory/'results';out.mkdir()
        roots={label:runner.directory/'active'/label for label,_,_ in PROJECTS}
        for label,project,_ in PROJECTS:
            for argv in b.build_commands(project,roots[label]):runner.run(argv,180)
        product=roots['tests']/'bin/Xemele.dll'
        args=[str(b.DOTNET),'--fx-version','10.0.11',str(roots['tests']/'bin/Xemele.Tests.dll')]
        for name in NAMESPACES:args+=['-namespace','Xemele.Tests.'+name]
        if source['phase']=='baseline':
            args+=['-method-','*ResolvedQueriesSucceedWithNoRemainingTraversalBudget','-method-','*UnchangedAnnotZ002CompletesUnderOrdinaryCorpusLimits']
        args+=['-method-','*WarmBorrowed*','-parallelMode','none','-noLogo','-noColor','-result-xml',str(out/'correctness.xml')]
        runner.run(args,180);tests=test_summary(out/'correctness.xml')
        union=out/'public-union.txt';b.write_api_union(union)
        def api(label,mode,path):
            dll=next(dll for name,_,dll in PROJECTS if name==label)
            runner.run([str(b.DOTNET),'--fx-version','10.0.11',str(roots[label]/'bin'/dll),mode,str(product),str(path)],90)
        api('public','compare',union)
        internal=out/'all-internal.txt';api('internal','emit',internal);actual=b.rows(internal)
        for lane in ['Model','Regex','Datatypes','Facets','RelaxNg','Xsd','Schematron','Reporting']:
            expected=b.rows(b.ROOT/('tests/Contracts/Fixtures/M8/'+lane+'.golden.txt'))
            assert all(actual.get(k)==v for k,v in expected.items()),lane
        for baseline,count in [('tests/Contracts/Fixtures/M8/Xsd.golden.txt',615),('docs/reports/M8-xsd-compile/compiler.golden.txt',257)]:
            expected=b.rows(b.ROOT/baseline);assert len(expected)==count;b.exact_projection(actual,expected)
        added=out/'availability-internal.txt';api('added','emit',added);added_rows=b.rows(added)
        expected_joins=b.rows(b.REPORT/'added-joins.golden.txt')
        observed_joins={name:row for name,row in added_rows.items() if not any(name[2:]==('Xemele.Validation.'+t) or name[2:].startswith('Xemele.Validation.'+t+'.') for t in NEW_TYPES)}
        assert len(added_rows)==93,'Exact93 additive row count changed'
        assert observed_joins==expected_joins,'Added join deletion/signature/nullability drift'
        for name in NEW_TYPES:assert 'T:Xemele.Validation.'+name in added_rows
        require_verified_added_projection(added_rows)
        api('added','compare',b.REPORT/'verified-added.golden.txt')
        for label,dll in [('normal','Consumer.dll'),('xml-only','XmlOnly.dll')]:
            runner.run([str(b.DOTNET),'--fx-version','10.0.11',str(roots[label]/'bin'/dll)],90)
        probe=roots['probe']/'bin/Xemele.Benchmarks.dll'
        runner.run(b.probe_commands(out,probe)[0],45,environment=b.environment(source['runtimeSourceCommit']))
        disabled=b.verify_resolved_probe(out/'disabled.json',probe,False,source['runtimeSourceCommit'])
        runner.run([sys.executable,'tools/planning/tasks.py','validate'],90)
        runner.run([str(b.GIT),'diff','--check',source['runtimeBaseCommit'],source['runtimeSourceCommit'],'--',*source['authoredPaths']],45)
        b.assert_prior_execution(runner.directory);b.review(review_sha)
        # Bind full output membership before terminal deletion; retain only the
        # canonical separate API rows, no original raw logs/results/assemblies.
        evidence=b.identities(p for p in out.rglob('*') if p.is_file())
        summary=dict(completed=True,controller=str(controller),sourceSha256=b.digest(b.SOURCE),tests=tests,
            publicRows=1430,xsdRows=615,compilerRows=257,addedJoinRows=len(expected_joins),addedInternalRows=len(added_rows),
            addedInternalProjection=added_rows,outputIdentities=evidence,outputMembershipSha256=b.identity_sha(evidence),
            normalConsumer='passed',xmlOnlyConsumer='passed',disabledRoutes=6,disabledConsumed=disabled['consumed'],countersEnabled=False,
            limitation='Affected frozen milestones only; original bad instances have zero original/gate pass credit')
    assert runner.receipt['completed']
    publish_finite(runner.directory/'checks-summary.json',summary,runner.hard)
    print('Correctness/API/normal/XML-only/disabled summary',flush=True)


if __name__=='__main__':
    with nested.interruptions():main()
