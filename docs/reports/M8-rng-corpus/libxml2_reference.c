#define _POSIX_C_SOURCE 200809L
#include <libxml/parser.h>
#include <libxml/relaxng.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>
static void quiet(void *context, const char *format, ...) { (void)context; (void)format; }
/* Real libxml2 schema/instance invocations, with independent stage outcomes.
   Input TSV has case ID, schema path, zero or more instance paths. */
int main(int argc, char **argv) {
 if (argc != 2) return 2;
 FILE *input = fopen(argv[1], "r"); if (!input) return 2;
 xmlInitParser(); xmlSetGenericErrorFunc(NULL, quiet);
 char *line = NULL; size_t length = 0;
 while (getline(&line, &length, input) >= 0) {
  line[strcspn(line, "\r\n")] = 0;
  char *save = NULL, *id = strtok_r(line, "\t", &save), *path = strtok_r(NULL, "\t", &save);
  if (!id || !path) return 2;
  xmlRelaxNGParserCtxtPtr parser = xmlRelaxNGNewParserCtxt(path); if (!parser) return 2;
  xmlRelaxNGSetParserErrors(parser, quiet, quiet, NULL);
  xmlRelaxNGPtr schema = xmlRelaxNGParse(parser); xmlRelaxNGFreeParserCtxt(parser);
  printf("%s\tschema\t%s\n", id, schema ? "accepted" : "rejected");
  int index = 0;
  while ((path = strtok_r(NULL, "\t", &save))) {
   if (!schema) continue;
   xmlDocPtr doc = xmlReadFile(path, NULL, XML_PARSE_NONET);
   if (!doc) { printf("%s\t%d\tincomplete\n", id, index++); continue; }
   xmlRelaxNGValidCtxtPtr validation = xmlRelaxNGNewValidCtxt(schema); if (!validation) return 2;
   xmlRelaxNGSetValidErrors(validation, quiet, quiet, NULL);
   int result = xmlRelaxNGValidateDoc(validation, doc);
   printf("%s\t%d\t%s\n", id, index++, result == 0 ? "valid" : result > 0 ? "invalid" : "engine-error");
   xmlRelaxNGFreeValidCtxt(validation); xmlFreeDoc(doc);
  }
  xmlRelaxNGFree(schema);
 }
 free(line); fclose(input); xmlCleanupParser(); return 0;
}
