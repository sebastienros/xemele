/* Test-only oracle, statically linked to the exact pinned libxml2. */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <libxml/HTMLparser.h>
#include <libxml/HTMLtree.h>
#include <libxml/xmlIO.h>
static xmlChar *decode(char *s) {
    if(strcmp(s,"!")==0)return NULL;size_t n=strlen(s)/2; xmlChar *v=malloc(n+1);
    for(size_t i=0;i<n;i++) {unsigned c;sscanf(s+2*i,"%2x",&c);v[i]=(xmlChar)c;}v[n]=0;return v;
}
static void string(const xmlChar *s) {putchar('"');if(s)for(;*s;s++){switch(*s){case '"':fputs("\\\"",stdout);break;case '\\':fputs("\\\\",stdout);break;case '\n':fputs("\\n",stdout);break;case '\r':fputs("\\r",stdout);break;case '\t':fputs("\\t",stdout);break;default:if(*s<32)printf("\\u%04x",*s);else putchar(*s);}}putchar('"');}
static void dump(xmlNode *n) {xmlBufferPtr b=xmlBufferCreate();xmlOutputBufferPtr o=xmlOutputBufferCreateBuffer(b,NULL);htmlNodeDumpFormatOutput(o,NULL,n,NULL,0);xmlOutputBufferFlush(o);string(xmlBufferContent(b));xmlOutputBufferClose(o);xmlBufferFree(b);}
int main(int argc,char **argv) {
 if(argc==3 && strcmp(argv[1],"--parse")==0){
  FILE *f=fopen(argv[2],"rb");if(!f)return 2;fseek(f,0,SEEK_END);long size=ftell(f);rewind(f);char *bytes=malloc(size+1);if(fread(bytes,1,size,f)!=(size_t)size)return 3;bytes[size]=0;fclose(f);
  xmlDocPtr parsed=htmlReadMemory(bytes,(int)size,NULL,NULL,HTML_PARSE_NODEFDTD|HTML_PARSE_NOERROR|HTML_PARSE_NOWARNING|HTML_PARSE_NONET);
  if(!parsed)return 4;dump((xmlNode*)parsed);putchar('\n');xmlFreeDoc(parsed);free(bytes);return 0;
 }
 if(argc!=2)return 2;FILE *f=fopen(argv[1],"r");if(!f)return 2;
 xmlDocPtr d=htmlNewDocNoDtD(NULL,NULL);xmlNode *stack[8192],*nodes[65536];int depth=1,count=0;stack[0]=(xmlNode*)d;
 char *line=NULL;size_t capacity=0;
 while(getline(&line,&capacity,f)>0){char *field[5];int nf=0;char *p=strtok(line,"\t\r\n");while(p&&nf<5){field[nf++]=p;p=strtok(NULL,"\t\r\n");}
 if(!nf)continue;if(field[0][0]=='Z'){if(depth<2)return 3;depth--;continue;}
 xmlChar *a=nf>1?decode(field[1]):NULL,*b=nf>2?decode(field[2]):NULL,*c=nf>3?decode(field[3]):NULL,*q=nf>4?decode(field[4]):NULL;xmlNode *n=NULL;
 switch(field[0][0]){
 case 'E': n=xmlNewDocNode(d,NULL,a,NULL);if(b&&b[0]){xmlNs *ns=calloc(1,sizeof(xmlNs));ns->type=XML_LOCAL_NAMESPACE;ns->href=xmlStrdup(b);ns->prefix=c&&c[0]?xmlStrdup(c):NULL;n->ns=ns;}break;
 case 'T':n=xmlNewDocText(d,a);break;
 case 'C':n=xmlNewDocComment(d,a);break;
 case 'P':n=xmlNewDocPI(d,a,b);break;
 case 'D':n=(xmlNode*)xmlCreateIntSubset(d,a,b,c);break;
 case 'A':{xmlNs *ns=NULL;if(c&&c[0]){ns=calloc(1,sizeof(xmlNs));ns->type=XML_LOCAL_NAMESPACE;ns->prefix=q&&q[0]?xmlStrdup(q):NULL;ns->href=xmlStrdup(c);}xmlNewNsProp(stack[depth-1],ns,a,b);break;}
 }
 if(n){if(n->type!=XML_DTD_NODE)xmlAddChild(stack[depth-1],n);nodes[count++]=n;if(n->type==XML_ELEMENT_NODE)stack[depth++]=n;}
 free(a);free(b);free(c);free(q);
 }
 if(depth!=1)return 4;fclose(f);free(line);printf("{\"document\":");dump((xmlNode*)d);printf(",\"nodes\":[");for(int i=0;i<count;i++){if(i)putchar(',');dump(nodes[i]);}puts("]}");
 /* Test-only namespace pointers intentionally belong to this short process. */
 xmlFreeDoc(d);return 0;
}
