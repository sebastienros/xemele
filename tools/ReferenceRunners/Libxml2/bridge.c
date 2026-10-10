/* Task-private reference infrastructure. No native code enters Xemele packages. */
#include <libxml/xmlreader.h>
#include <libxml/parser.h>
#include <libxml/xmlerror.h>
#include <stdlib.h>
#include <string.h>

typedef int (*resolve_fn)(void *, const char *, const unsigned char **, size_t *);
typedef struct {
    xmlTextReader *reader;
    resolve_fn resolve;
    void *user;
    int denied;
    int error_code;
    char error_message[4096];
} session;

static xmlParserErrors load(void *user, const char *url, const char *public_id,
                           xmlResourceType type, xmlParserInputFlags flags,
                           xmlParserInput **out) {
    session *s = user;
    const unsigned char *bytes = NULL;
    size_t length = 0;
    (void)public_id; (void)type; (void)flags;
    *out = NULL;
    if (url == NULL || !s->resolve(s->user, url, &bytes, &length)) {
        s->denied = 1;
        return XML_IO_ENOENT;
    }
    /* Flags 0 copies bytes: no static or zero-terminated memory assumptions. */
    *out = xmlNewInputFromMemory(url, bytes, length, 0);
    return *out == NULL ? XML_ERR_NO_MEMORY : XML_ERR_OK;
}

static void quiet(void *user, const xmlError *error) {
    session *s = user;
    /* Namespace errors can accompany a positive read result. Preserve the first
       real error, not warnings, so invalid XML cannot become normalized success. */
    if (error != NULL && error->level >= XML_ERR_ERROR && s->error_code == 0) {
        s->error_code = error->code;
        if (error->message != NULL) {
            strncpy(s->error_message, error->message, sizeof(s->error_message) - 1);
        }
    }
}

session *xb_create(const unsigned char *bytes, int length, const char *encoding,
                   resolve_fn resolve, void *user) {
    session *s = calloc(1, sizeof(*s));
    if (s == NULL) return NULL;
    s->resolve = resolve; s->user = user;
    s->reader = xmlReaderForMemory((const char *)bytes, length, "urn:xemele:input",
                                  encoding, XML_PARSE_NOENT | XML_PARSE_DTDLOAD | XML_PARSE_DTDATTR |
                                  XML_PARSE_NONET);
    if (s->reader == NULL) { free(s); return NULL; }
    xmlTextReaderSetResourceLoader(s->reader, load, s);
    xmlTextReaderSetStructuredErrorHandler(s->reader, quiet, s);
    return s;
}
void xb_free(session *s) { if (s != NULL) { xmlFreeTextReader(s->reader); free(s); } }
int xb_next(session *s) {
    int result = xmlTextReaderRead(s->reader);
    return s->error_code != 0 ? -1 : result;
}
int xb_type(session *s) { return xmlTextReaderNodeType(s->reader); }
int xb_empty(session *s) { return xmlTextReaderIsEmptyElement(s->reader); }
int xb_first_attr(session *s) { return xmlTextReaderMoveToFirstAttribute(s->reader); }
int xb_next_attr(session *s) { return xmlTextReaderMoveToNextAttribute(s->reader); }
int xb_element(session *s) { return xmlTextReaderMoveToElement(s->reader); }
const char *xb_ns(session *s) { return (const char *)xmlTextReaderConstNamespaceUri(s->reader); }
const char *xb_name(session *s) { return (const char *)xmlTextReaderConstLocalName(s->reader); }
const char *xb_value(session *s) { return (const char *)xmlTextReaderConstValue(s->reader); }
int xb_denied(session *s) { return s->denied; }
int xb_error_code(session *s) {
    const xmlError *e = xmlTextReaderGetLastError(s->reader);
    return s->error_code != 0 ? s->error_code : (e == NULL ? 0 : e->code);
}
const char *xb_error(session *s) {
    const xmlError *e = xmlTextReaderGetLastError(s->reader);
    return s->error_code != 0 ? s->error_message : (e == NULL ? "libxml2 reader failed" : e->message);
}
const char *xb_version(void) { return xmlParserVersion; }

const char *xb_xml_version(session *s) { return (const char *)xmlTextReaderConstXmlVersion(s->reader); }
