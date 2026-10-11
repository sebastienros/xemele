"""Close the pinned resource ledger with exact parser checks and dependencies."""
import collections,hashlib,json,pathlib
root=pathlib.Path(__file__).resolve().parents[3]
fixtures=root/'tests/fixtures/M7-builder'
path=fixtures/'resource-dispositions.json';ledger=json.loads(path.read_text())
static=json.loads((fixtures/'template-assertions.json').read_text())['cases']
byfile=collections.defaultdict(list)
for c in static:byfile[c['file']].append(c)
checks={
 'foreign_content_dom_properties.html':'StaticForeignDomPropertiesAndHtmlBreakouts',
 'html_content_in_foreign_context.html':'StaticForeignDomPropertiesAndHtmlBreakouts',
 'newline-normalization-cr-then-lf.html':'StaticNewlineNormalizationAndNoscriptTemplate',
 'noscript-template-querySelector.html':'StaticNewlineNormalizationAndNoscriptTemplate',
 'math-parse01.html':'StaticMathParsingAndCaseAdjustments',
 'math-parse03.html':'StaticMathParsingAndCaseAdjustments',
 'named-character-references.html':'OriginalNamedReferencesInPublicFragmentLane',
 'named-character-references-data.js':'OriginalNamedReferencesInPublicFragmentLane',
 'zero.html':'OriginalNulAndAmbiguousAmpersandCases',
 'ambiguous-ampersand.html':'OriginalNulAndAmbiguousAmpersandCases',
 'form-element-pointer-in-template.html':'OriginalStaticAdoptionBaselineAndFormPointerTrees',
 'Object-prototype-names.html':'OriginalPrototypeNamesAndCustomElementBaselineGraph',
 'meta-inhead-insertion-mode.html':'OriginalMetaCharsetPrecedence',
 'quotes-in-meta.html':'OriginalMetaCharsetPrecedence',
 'adoption-agency-reparenting.html':'OriginalStaticAdoptionBaselineAndFormPointerTrees',
 'custom-element-reparenting.html':'OriginalPrototypeNamesAndCustomElementBaselineGraph',
}
for suffix in ['001','003','004','005','006','008','009','010','011','013']:checks['foreign_content_'+suffix+'.html']='OriginalForeignVisualTreePredicates'
for name in ['Document.getElementsByTagName-foreign-01.html','Document.getElementsByTagName-foreign-02.html','Element.getElementsByTagName-foreign-01.html','Element.getElementsByTagName-foreign-02.html']:checks[name]='OriginalForeignAdjustedVocabularyAndFontNamespaces'
dependencies={
 'DOMContentLoaded-defer-task-order.tentative.html':('document.addEventListener','Deferred script, timer task and DOMContentLoaded order. Every assertion observes executable task/event dispatch.'),
 'DOMContentLoaded-defer.html':('document.addEventListener','Deferred external script and DOMContentLoaded ordering; no static tree assertion.'),
 'support/DOMContentLoaded-defer.js':('t.step_timeout','Deferred-script task/timeouts observe dcl set by DOMContentLoaded listener.'),
 'the-end.html':('window.addEventListener','Four async tests observe DOMContentLoaded/load/pageshow Event properties and dispatch order.'),
 'unclosed-svg-script.html':('assert_true(scriptWithEndTagRan)','Five tests observe actual SVG script execution flags; static tree behavior remains covered by foreign .dat and breakouts.'),
 'support/svg-script-self-closing.js':('scriptSelfClosing = true','Execution-only side effect observed by unclosed-svg-script.html; no parser expectation.'),
 'insert-into-nonempty-document.html':('doc.open()','Four tests require an open parser document, writes before EOF and caller DOM mutation between writes. All public parser entrypoints complete fresh operations.'),
 'foster-parenting-into-document-with-element-child.html':('other.appendChild(document.querySelector','Inline script moves on-stack table into a second document before parser foster insertion.'),
 'foster-parenting-into-template-element.html':('window.tmpl.appendChild','Inline script gives on-stack table a script-created template parent before foster insertion.'),
 'foster-parenting-moved-table.window.js':('window.moveTable','Three variants/two tests each require script execution between two insertions of one token and table reparent/removal.'),
 'adoption-agency-reparenting-cycle.html':('window.fb.appendChild(window.ca)','Inline script moves common ancestor into furthest block before adoption.'),
 'adoption-agency-reparenting-document-target.html':('window.other.appendChild','Inline script adopts on-stack table into second Document before adoption foster move.'),
 'adoption-agency-reparenting-inner-loop-cycle.html':('window.fb.appendChild(window.ca)','Inline script creates cycle before adoption with extra inner-loop formatting element.'),
 'adoption-agency-reparenting-pagehide.tentative.html':('contentWindow.onpagehide','Synchronous pagehide mutation during adoption; optional browser execution behavior.'),
 'adoption-agency-reparenting-shadow-cycle.html':('attachShadow','Inline script creates host-including shadow-crossing cycle before adoption.'),
 'adoption-agency-reparenting-template-cycle.html':('tmpl.content.appendChild(window.ca)','Inline script moves common ancestor through a created template content before adoption.'),
 'custom-element-reparenting-shadow-cycle.html':('customElements.define','Custom-element constructor moves body into own shadow root before insertion.'),
 'custom-element-reparenting-template-cycle.html':('customElements.define','Custom-element constructor moves body through template content into own shadow root before insertion.'),
 'crashtests/adoption-agency-001.html':('customElements.define','Custom constructor reenters document.write and adopts furthest block into iframe document.'),
 'crashtests/adoption-agency-002.html':('customElements.define','Custom constructor reenters document.write with1000 formatting tokens and adopts furthest block.'),
 'crashtests/adoption-agency-bailout-001.html':('customElements.define','Custom constructor reenters document.write closing earlier formatting tokens.'),
 'crashtests/adoption-agency-shift-001.html':('customElements.define','Custom constructor reenters document.write closing b and adopts furthest block.'),
 'crashtests/adoption-agency-swap-empty-list-001.html':('customElements.define','Custom constructor reenters document.write clearing active formatting list.'),
 'crashtests/adoption-agency-custom-furthest-block.html':('connectedCallback','Custom connectedCallback reenters document.write during furthest-block reparenting.'),
 'crashtests/adoption-agency-cycle-mutation.html':('contentWindow.onpagehide','Iframe pagehide callback creates cycle during adoption.'),
 'crashtests/adoption-agency-pagehide-reentrant-write.html':('contentWindow.onpagehide','Iframe pagehide callback reenters document.write while adoption bookmark exists.'),
 'crashtests/reconstruct-active-formatting-elements-001.html':('customElements.define','Custom constructors adopt wrapper into iframe and reenter document.write during formatting reconstruction.'),
}
notes={
 'form-element-pointer-in-template.html':[{'assertion':'input.form equals form outside template; null inside both template variants','disposition':'browser_DOM_association_outside_frozen_API','reason':'No public form-owner property in contracted graph. All original ancestry/formcount/tree assertions translated. Root approved exact projection on2026-10-11.'}],
 'Object-prototype-names.html':[{'assertion':'el.constructor equals HTMLUnknownElement/Element','disposition':'JavaScript_class_identity_outside_frozen_API','reason':'No JavaScript class identity in .NET graph. Original names/case/namespace factory assertions translated; root approved.'}],
 'ambiguous-ampersand.html':[{'assertion':'href URL equals ?a=b&c=d&a0b=c&copy=1&noti=n&not=in&notin=%E2%88%89%C2%AC&;&%20&','disposition':'URL_property_serialization_outside_raw_attribute_API','reason':'Raw decoded href/text and child count translated; URL percent-serialization not exposed. Original actual document.write transport needs execution; static network markup remains applicable.'}],
 'noscript-template-querySelector.html':[{'assertion':'noscriptNode.innerHTML equals &lt;div&gt;&lt;/div&gt;','input':'<template id="template1"><noscript><div></div></noscript></template>','options':{'ScriptingEnabled':True},'disposition':'deferred_exact_assertion','owner':'M7-serializer','reason':'Root approved route to later serializer. Textnode/text and null-descendant assertions pass now; this serialization assertion is not marked passed.'}],
 'adoption-agency-reparenting.html':[{'assertion':'Tests1-3: cross-document common ancestor/furthest block adoption and scripted removal before </b>','disposition':'browser_execution_dependency','reason':'Exact no-script baseline test0 translated; remaining three tests execute inline reparenting scripts.'}],
 'custom-element-reparenting.html':[{'assertion':'Registration baseline plus attributeChangedCallback/constructor shadow and parent mutations','disposition':'browser_execution_dependency','reason':'No-reparenting baseline graph translated. Original registration/attributeChangedCallback/constructor behavior requires custom reactions; remaining three tests depend on it.'}],
}
# Keep the exact serialization follow-up separately from its applicable parsing assertions.
notes['noscript-template-querySelector.html']=[{'assertion':'noscriptNode.innerHTML equals &lt;div&gt;&lt;/div&gt;','input':'<template id="template1"><noscript><div></div></noscript></template>','options':{'ScriptingEnabled':True},'disposition':'deferred_exact_assertion','owner':'M7-serializer','reason':'Root approved route; textnode/text and null-descendant assertions pass in builder. Serialization assertion is not marked passed.'}]
for suffix in ['011','013']:notes['foreign_content_'+suffix+'.html']=[{'assertion':'Green square from ID/class CSS fill style','disposition':'browser_rendering_dependency','reason':'Tree, SVG namespace and ID/class markup translated; painted color requires CSS cascade and rendering, outside parser API.'}]
for row in ledger['resources']:
    p=fixtures/'wpt-parsing'/row['path'];data=p.read_bytes();assert hashlib.sha256(data).hexdigest()==row['sha256']
    name=row['path']
    if name in byfile:
        row.update(disposition='applicable_original_static_assertions',evidence='TemplateAssertionTests.OriginalStaticTemplateAssertionsAndAllRetainedSupportFiles',configurations=len(byfile[name]),assertions=sum(len(c['assertions']) for c in byfile[name]),vectors='tests/fixtures/M7-builder/template-assertions.json')
    elif name in checks:row.update(disposition='applicable_static_translated',evidence='StandaloneWptTests.'+checks[name])
    elif name in dependencies:
        witness,reason=dependencies[name];assert witness in data.decode(),name
        row.update(disposition='browser_execution_dependency',evidence=reason,dependency_witness=witness)
    assert row['disposition']!='unresolved_resource_audit',name
    if name in notes:row['assertion_dispositions']=notes[name]
assert len(ledger['resources'])==ledger['inventory']==166
ledger['complete']=True
ledger['summary']=dict(collections.Counter(r['disposition'] for r in ledger['resources']))
ledger['scope']='Complete per-resource and mixed-assertion audit, not a claim that browser execution or deferred serialization assertions ran.'
path.write_text(json.dumps(ledger,indent=2)+'\n')
print(ledger['summary'])
