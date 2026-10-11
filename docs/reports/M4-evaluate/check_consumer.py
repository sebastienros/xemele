#!/usr/bin/env python3
"""Pack the real runtime and execute an independent net10.0 package consumer."""
import argparse
import pathlib
import subprocess
import tempfile
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);a=p.parse_args()
root=pathlib.Path(__file__).resolve().parents[3]
def run(*args,cwd=root):subprocess.run([a.dotnet,*map(str,args)],cwd=cwd,check=True)
with tempfile.TemporaryDirectory(prefix='m4-evaluate-consumer-') as temporary:
    temporary=pathlib.Path(temporary);packages=temporary/'packages';consumer=temporary/'consumer';consumer.mkdir()
    run('pack','src/Xemele','-c','Release','--no-build','--no-restore','-o',packages,'-p:PackageVersion=0.0.0-m4-evaluate')
    (consumer/'Consumer.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>IndependentConsumer</AssemblyName><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><PackageReference Include="Xemele" Version="0.0.0-m4-evaluate" /></ItemGroup></Project>')
    (temporary/'global.json').write_text((root/'global.json').read_text())
    (consumer/'Program.cs').write_text((root/'samples/M4-evaluate/Program.cs').read_text())
    run('restore','-m:1','-nr:false','--source',packages,'-p:NuGetAudit=false','--packages',temporary/'cache',cwd=consumer)
    run('build','-c','Release','--no-restore','-m:1','-nr:false','-p:UseSharedCompilation=false',cwd=consumer)
    run('--fx-version','10.0.11',consumer/'bin/Release/net10.0/IndependentConsumer.dll',cwd=consumer)
    print('PASS: fresh standalone net10.0 consumer uses packed public XPathExpression facade')
