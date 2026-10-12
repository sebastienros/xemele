"""Record concise binary/source/build identities, never execution output arrays."""
import hashlib,json,platform,subprocess
from pathlib import Path
HERE=Path(__file__).resolve().parent
JING=Path('/private/tmp/xemele-rng-jing');XML=Path('/private/tmp/xemele-rng-libxml2');BUILD=Path('/private/tmp/xemele-rng-libxml2-build')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run(*a):return subprocess.check_output(a,text=True).strip()
def main():
 cache={}
 for line in (BUILD/'CMakeCache.txt').read_text().splitlines():
  if line and not line.startswith(('#','//')) and '=' in line:
   key,val=line.split('=',1);name,kind=key.split(':',1)
   if name.startswith('LIBXML2_') or name in ['BUILD_SHARED_LIBS','CMAKE_BUILD_TYPE','CMAKE_OSX_DEPLOYMENT_TARGET']:cache[name]=val
 manifest={'jing':{'version':'20241231','revision':'a6bc0041035988325dfbfe7823ef2c098fc56597','submoduleRevision':run('git','-C',str(JING/'relaxng.org'),'rev-parse','HEAD'),'buildCommand':'JAVA_HOME=/Library/Java/JavaVirtualMachines/temurin-25.jdk/Contents/Home ./ant ant-jar','jarPath':str(JING/'build/jing.jar'),'jarSha256':sha(JING/'build/jing.jar'),'classPath':str(JING/'build'),'classSha256':sha(JING/'build/RngReference.class'),'sourceSha256':sha(HERE/'RngReference.java'),'java':'Temurin OpenJDK 25.0.2+10','schemaReader':'SAXSchemaReader.getInstance()','checkIdIdref':False},'libxml2':{'version':(XML/'VERSION').read_text().strip(),'revision':'96498992efa48d52b0e8b83058bd88dbdaf153c1','binaryPath':str(BUILD/'rng-reference'),'binarySha256':sha(BUILD/'rng-reference'),'archiveSha256':sha(BUILD/'libxml2.a'),'sourceSha256':sha(HERE/'libxml2_reference.c'),'compiler':run('clang','--version').splitlines()[0],'cmake':run('/private/tmp/xemele-toolchains/cmake-3.31.6/bin/cmake','--version').splitlines()[0],'config':cache,'bridgeFlags':'-O2 -std=c11 -mmacosx-version-min=15.8 -Wall -Wextra -Werror; static libxml2.a; XML_PARSE_NONET'},'platform':platform.system()+' '+platform.machine()}
 manifest['jing']['runnerClassHashes']={p.name:sha(p) for p in sorted((JING/'build').glob('RngReference*.class'))}
 manifest['jing']['sourceArchiveSha256']=hashlib.sha256(subprocess.check_output(['git','-C',str(JING),'archive',manifest['jing']['revision']])).hexdigest()
 manifest['libxml2']['sourceArchiveSha256']=hashlib.sha256(subprocess.check_output(['git','--git-dir=/private/tmp/xemele-reference-cache/libxml2.git','archive',manifest['libxml2']['revision']])).hexdigest()
 # Exact JDK identity comes from the executing VM, not a guessed release.
 manifest['jing']['java']=subprocess.run(['java','-version'],capture_output=True,text=True,check=True).stderr.splitlines()[0]
 (HERE/'reference-build.json').write_text(json.dumps(manifest,indent=2)+'\n')
if __name__=='__main__':main()
