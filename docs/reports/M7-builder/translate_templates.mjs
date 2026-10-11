// Offline symbolic translation of unchanged WPT template assertions. No parser
// implementation supplies expected values. Fail on unrecognized DOM operations.
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
const root = path.resolve('tests/fixtures/M7-builder');
const cases = []; let file, title, current, nextDocument = 0;
const documents = new Map();
const expression = Symbol('expression');
function proxy(steps) {
  return new Proxy({}, {
    get(_, key) {
      if (key === expression) return steps;
      if (key === 'then') return undefined;
      if (['querySelector','getAttribute','hasAttribute','getElementById','getElementsByTagName','getElementsByTagNameNS'].includes(key))
        return (...values) => proxy([...steps, [key, ...values]]);
      if (key === 'createElement') return name => proxy([['created', name, steps[0][1]]]);
      if (key === 'appendChild') return node => node;
      if (typeof key !== 'string') throw Error('unknown symbolic key');
      return proxy([...steps, [key]]);
    },
    set(_, key, value) {
      if (key !== 'innerHTML') throw Error('unexpected assignment ' + key);
      current = {file, title, context: steps[0][0] === 'created' ? steps[0][1] : steps[1][0], html:value, assertions:[]};
      documents.set(steps[0][0] === 'created' ? steps[0][2] : steps[0][1], current);
      cases.push(current); return true;
    }
  });
}
function value(v) { return v?.[expression] ? {steps:v[expression]} : v; }
function assertion(kind, actual, expected, message) {
  const first = actual?.[expression]?.[0];
  const target = first ? documents.get(first[0] === 'created' ? first[2] : first[1]) ?? current : current;
  if (!target) throw Error('assertion without parse');
  target.title=title;
  target.assertions.push({kind, actual:value(actual), expected:value(expected), message:message??''});
}
const run = (fn,name) => {title=name; fn();};
function iframe(url,fn,name) {
  title=name; current={file,title,context:'document',support:path.basename(url),assertions:[]};cases.push(current);
  const id=++nextDocument;documents.set(id,current);
  fn({iframes:[{contentDocument:proxy([['doc',id]])}]});
}
const common=fs.readFileSync(path.join(root,'wpt-template-support/common.js'),'utf8');
const vocab=vm.runInNewContext(common.slice(0,common.indexOf('function newDocument()'))+'\n({HTML5_ELEMENTS,HTML5_VOID_ELEMENTS})');
const newDoc=()=>proxy([['doc',++nextDocument]]);
const context = {
  test:run, generate_tests:(fn,rows)=>rows.forEach(([name,...args])=>run(()=>fn(...args),name)),
  testInIFrame:iframe, newHTMLDocument:newDoc,
  newRenderedHTMLDocument:newDoc,newContext:()=>({}),cleanContext:()=>{},
  Node:{TEXT_NODE:3}, assert_equals:(a,b,m)=>assertion('equal',a,b,m),
  assert_not_equals:(a,b,m)=>assertion('unequal',a,b,m),
  assert_false:(a,m)=>assertion('equal',a,false,m),
  assert_true:(a,m)=>assertion('equal',a,true,m),
  HTML5_ELEMENTS:vocab.HTML5_ELEMENTS,isVoidElement:name=>vocab.HTML5_VOID_ELEMENTS.includes(name)
};
function walk(dir) {return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);}
for (const p of walk(path.join(root,'wpt-parsing/template')).filter(p=>p.endsWith('.html')).sort()) {
  file=path.relative(path.join(root,'wpt-parsing'),p);current=null;
  const scripts=[...fs.readFileSync(p,'utf8').matchAll(/<script\b[^>]*>([\s\S]*?)<\/script>/gi)].map(m=>m[1]);
  vm.runInNewContext(scripts.join('\n'),context,{filename:file,timeout:3000});
}
for (const name of ['math-parse01.html','math-parse03.html','Document.getElementsByTagName-foreign-01.html','Document.getElementsByTagName-foreign-02.html','Element.getElementsByTagName-foreign-01.html','Element.getElementsByTagName-foreign-02.html','foreign_content_dom_properties.html']) {
  file=name; title=name; current={file,title,context:'document',source:name,assertions:[]};cases.push(current);
  const id=++nextDocument; documents.set(id,current); context.document=proxy([['doc',id]]);
  const scripts=[...fs.readFileSync(path.join(root,'wpt-parsing',name),'utf8').matchAll(/<script\b[^>]*>([\s\S]*?)<\/script>/gi)].map(m=>m[1]);
  vm.runInNewContext(scripts.join('\n'),context,{filename:file,timeout:3000});
}
for (const c of cases) if (!c.assertions.length) throw Error('unasserted case '+c.file);
const output={upstream:'ddb25fdfff7766af4cd75eac3fb3b6ac206140ce',cases};
fs.writeFileSync(path.join(root,'template-assertions.json'),JSON.stringify(output,null,2)+'\n');
console.log(`${cases.length} static configurations, ${cases.reduce((n,c)=>n+c.assertions.length,0)} assertions`);
