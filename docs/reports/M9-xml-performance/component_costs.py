"""Artifact-only coarse timers; never credit instrumented timing as parity evidence."""
import json, os, shutil, subprocess, tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
HOST='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
OUT=ROOT/'artifacts/M9-xml-performance/component-costs-v2';OUT.mkdir(parents=True,exist_ok=True)
DEST=Path(tempfile.mkdtemp(prefix='xemele-component-costs-',dir='/private/tmp'))
env=os.environ.copy();env.update(DOTNET_ROOT=str(Path(HOST).parent),DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0')
def run(cmd, log, cwd=DEST):
 with (OUT/log).open('w') as output:subprocess.run([str(x) for x in cmd],cwd=cwd,env=env,stdout=output,stderr=subprocess.STDOUT,check=True)
with (OUT/'source.tar').open('wb') as output:subprocess.run(['git','archive','HEAD','src','Directory.Build.props','Directory.Packages.props','global.json'],cwd=ROOT,stdout=output,check=True)
subprocess.run(['tar','-xf',str(OUT/'source.tar'),'-C',str(DEST)],check=True)
def wrap(path, declaration, counter):
 p=DEST/path;s=p.read_text();start=s.index(declaration);op=s.index('{',start);next_method=s.index('\n    private ',op+1) if declaration.startswith('private ') else s.index('\n    public ',op+1)
 close=s.rfind('}',op,next_method)
 s=s[:op+1]+f'\n        long __start = System.Diagnostics.Stopwatch.GetTimestamp();\n        try {{'+s[op+1:close]+f'}} finally {{ {counter}Ticks += System.Diagnostics.Stopwatch.GetTimestamp() - __start; {counter}Count++; }}\n    '+s[close:]
 class_open=s.index('\n{',s.index('internal sealed partial class'))+2
 s=s[:class_open]+f'\n    internal static long {counter}Ticks, {counter}Count;'+s[class_open:];p.write_text(s)
wrap('src/Xemele/Parsing/Core/XmlParserCore.cs','private void ParseTag()','CostTag')
wrap('src/Xemele/Parsing/Core/XmlParserCore.cs','private void ParseClose()','CostClose')
wrap('src/Xemele/Parsing/Dom/DomSink.cs','public bool CompleteTag(','CostDom')
app=DEST/'CostRunner';app.mkdir();(app/'CostRunner.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><RuntimeFrameworkVersion>10.0.11</RuntimeFrameworkVersion></PropertyGroup><ItemGroup><ProjectReference Include="../src/Xemele/Xemele.csproj"/></ItemGroup></Project>')
(app/'Program.cs').write_text(r'''using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Xemele;
var assembly=typeof(XmlDocument).Assembly;
var counters=new[]{("Xemele.Parsing.Syntax.XmlParserCore","CostTag"),("Xemele.Parsing.Syntax.XmlParserCore","CostClose"),("Xemele.Parsing.Dom.DomSink","CostDom")};
FieldInfo Field(string type,string name)=>assembly.GetType(type)!.GetField(name,BindingFlags.Static|BindingFlags.NonPublic)!;
var results=new List<object>();
foreach(var fixture in JsonDocument.Parse(File.ReadAllText(args[0])).RootElement.GetProperty("fixtures").EnumerateArray()){
 string id=fixture.GetProperty("id").GetString()!;
 byte[] input=File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(args[0])!,fixture.GetProperty("path").GetString()!));
 var options=new XmlParseOptions{DtdProcessing=XmlDtdProcessing.Parse};
 for(int i=0;i<5;i++)GC.KeepAlive(XmlDocument.Parse(input.AsSpan(),options));
 foreach(var (type,name) in counters){Field(type,name+"Ticks").SetValue(null,0L);Field(type,name+"Count").SetValue(null,0L);}
 var timer=Stopwatch.StartNew();
 for(int i=0;i<25;i++)GC.KeepAlive(XmlDocument.Parse(input.AsSpan(),options));
 timer.Stop();
 results.Add(new{fixture=id,perParseMilliseconds=timer.Elapsed.TotalMilliseconds/25,components=counters.Select(c=>new{name=c.Item2,perParseMilliseconds=(long)Field(c.Item1,c.Item2+"Ticks").GetValue(null)!*1000.0/Stopwatch.Frequency/25,perParseCalls=(long)Field(c.Item1,c.Item2+"Count").GetValue(null)!/25.0}).ToArray()});
}
long ticks=0;for(int i=0;i<1000000;i++){long start=Stopwatch.GetTimestamp();ticks+=Stopwatch.GetTimestamp()-start;}
Console.WriteLine(JsonSerializer.Serialize(new{instrumented=true,controlledTimingCredit=false,nestedTimers=false,stampPairNanoseconds=ticks*1e9/Stopwatch.Frequency/1000000,rows=results},new JsonSerializerOptions{WriteIndented=true}));
''')
run([HOST,'build',app/'CostRunner.csproj','-c','Release','--ignore-failed-sources','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','-p:RuntimeFrameworkVersion=10.0.11'],'build.log')
run([HOST,'--fx-version','10.0.11',app/'bin/Release/net10.0/CostRunner.dll',ROOT/'benchmarks/baselines/release/fixtures.json'],'report.json')
worker=DEST/'docs/reports/M9-xml-performance/MemoryWorker';shutil.copytree(ROOT/'docs/reports/M9-xml-performance/MemoryWorker',worker,ignore=shutil.ignore_patterns('bin','obj'))
p=worker/'Program.cs';p.write_text(p.read_text().replace('MaxInputCharacters = 16777216', 'MaxInputCharacters = 134217728'))
run([HOST,'build',worker/'XmlPerformanceMemory.csproj','-c','Release','--ignore-failed-sources','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','-p:RuntimeFrameworkVersion=10.0.11'],'qualification-build.log')
manifest=ROOT/'benchmarks/baselines/release/fixtures.json'
for f in json.loads(manifest.read_text())['fixtures']:
 run([HOST,'--fx-version','10.0.11',worker/'bin/Release/net10.0/XmlPerformanceMemory.dll','xemele',manifest.parent/f['path'],manifest,OUT/(f['id']+'-qualification.json'),'qualify'],f['id']+'-qualification.log')
(OUT/'metadata.json').write_text(json.dumps({'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'source':str(DEST),'qualified':10,'credit':'diagnostic coarse timers only'},indent=2))
print('PASS coarse timing plus ten instrumented semantic/XPath qualifications')
