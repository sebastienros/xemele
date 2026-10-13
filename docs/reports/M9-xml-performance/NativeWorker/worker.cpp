#include <libxml/parser.h>
#include <libxml/tree.h>
#include <libxml/xpath.h>
#include <algorithm>
#include <chrono>
#include <cstring>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>
#include <tuple>
#include <vector>

static xmlParserErrors deny(void*, const char*, const char*, xmlResourceType, xmlParserInputFlags, xmlParserInput** result) {
    *result = nullptr; return XML_IO_LOAD_ERROR;
}
static xmlDocPtr parse(const std::string& bytes) {
    auto context = xmlNewParserCtxt();
    if (!context) throw std::runtime_error("parser context allocation failed");
    xmlCtxtSetResourceLoader(context, deny, nullptr);
    auto document = xmlCtxtReadMemory(context, bytes.data(), static_cast<int>(bytes.size()), nullptr, "UTF-8", XML_PARSE_NOENT | XML_PARSE_NONET);
    xmlFreeParserCtxt(context);
    if (!document) throw std::runtime_error("strict DOM parse failed");
    return document;
}
static std::string text(const xmlChar* value) { return value ? reinterpret_cast<const char*>(value) : ""; }
static void frame(std::ostream& out, char kind, std::initializer_list<std::string> values) {
    out.put(kind);
    for (const auto& value : values) {
        uint32_t size = static_cast<uint32_t>(value.size());
        for (unsigned shift = 0; shift < 32; shift += 8) out.put(static_cast<char>((size >> shift) & 255));
        out.write(value.data(), static_cast<std::streamsize>(value.size()));
    }
}
static void semantic(xmlNodePtr node, std::ostream& out) {
    if (node->type == XML_COMMENT_NODE) { frame(out, 'C', {text(node->content)}); return; }
    if (node->type == XML_PI_NODE) { frame(out, 'P', {text(node->name), text(node->content)}); return; }
    if (node->type != XML_ELEMENT_NODE) throw std::runtime_error("unexpected common-model node");
    frame(out, 'S', {node->ns ? text(node->ns->href) : "", text(node->name)});
    std::vector<std::tuple<std::string,std::string,std::string>> attributes;
    for (auto attr = node->properties; attr; attr = attr->next) {
        auto value = xmlNodeListGetString(node->doc, attr->children, 1);
        attributes.emplace_back(attr->ns ? text(attr->ns->href) : "", text(attr->name), text(value)); xmlFree(value);
    }
    std::sort(attributes.begin(), attributes.end());
    for (const auto& [uri, local, value] : attributes) frame(out, 'A', {uri, local, value});
    std::string pending;
    auto flush = [&]() { if (!pending.empty()) { frame(out, 'T', {pending}); pending.clear(); } };
    for (auto child = node->children; child; child = child->next) {
        if (child->type == XML_TEXT_NODE || child->type == XML_CDATA_SECTION_NODE) pending += text(child->content);
        else { flush(); semantic(child, out); }
    }
    flush(); frame(out, 'E', {});
}
int main(int argc, char** argv) {
    try {
        if (argc != 6) throw std::runtime_error("input expression qualify|parse|lifecycle|serialize|xpath iterations output-prefix");
        if (std::strcmp(xmlParserVersion, "21504")) throw std::runtime_error("unqualified libxml2 version");
        std::ifstream input(argv[1], std::ios::binary);
        std::string bytes((std::istreambuf_iterator<char>(input)), std::istreambuf_iterator<char>());
        if (!input || bytes.empty() || bytes.size() > 16777216) throw std::runtime_error("invalid frozen fixture input");
        std::string operation(argv[3]); unsigned iterations = static_cast<unsigned>(std::stoul(argv[4]));
        if (!iterations || iterations > 1000000) throw std::runtime_error("invalid iterations");
        auto document = parse(bytes); auto context = xmlXPathNewContext(document);
        auto expression = xmlXPathCtxtCompile(context, reinterpret_cast<const xmlChar*>(argv[2]));
        if (!context || !expression) throw std::runtime_error("XPath compile failed");
        auto result = xmlXPathCompiledEval(expression, context);
        if (!result || result->type != XPATH_NUMBER) throw std::runtime_error("XPath numeric result required");
        double expected = result->floatval; xmlXPathFreeObject(result);
        long long consumed = 0; std::chrono::nanoseconds elapsed(0);
        if (operation == "qualify") {
            std::ofstream events(std::string(argv[5])+".events", std::ios::binary); semantic(xmlDocGetRootElement(document), events);
            xmlChar* serialized = nullptr; int length = 0; xmlDocDumpMemoryEnc(document, &serialized, &length, "UTF-8");
            if (!serialized || length <= 0) throw std::runtime_error("UTF-8 serialization failed");
            std::ofstream output(std::string(argv[5])+".xml", std::ios::binary); output.write(reinterpret_cast<const char*>(serialized), length); consumed = length; xmlFree(serialized);
        } else for (unsigned i = 0; i < iterations; i++) {
            auto start = std::chrono::steady_clock::now();
            if (operation == "parse" || operation == "lifecycle") {
                auto value = parse(bytes); consumed += xmlDocGetRootElement(value) != nullptr;
                if (operation == "parse") elapsed += std::chrono::steady_clock::now()-start;
                xmlFreeDoc(value);
                if (operation == "lifecycle") elapsed += std::chrono::steady_clock::now()-start;
            } else if (operation == "serialize") {
                xmlChar* value = nullptr; int length = 0; xmlDocDumpMemoryEnc(document, &value, &length, "UTF-8");
                elapsed += std::chrono::steady_clock::now()-start; consumed += length; xmlFree(value);
            } else if (operation == "xpath") {
                auto value = xmlXPathCompiledEval(expression, context); elapsed += std::chrono::steady_clock::now()-start;
                if (!value || value->type != XPATH_NUMBER || value->floatval != expected) throw std::runtime_error("XPath changed result");
                consumed += static_cast<long long>(value->floatval); xmlXPathFreeObject(value);
            } else throw std::runtime_error("unknown operation");
        }
        std::cout << "{\"engine\":\"libxml2-2.15.4\",\"xpath\":" << expected << ",\"consumed\":" << consumed
                  << ",\"iterations\":" << iterations << ",\"steadyNanoseconds\":" << elapsed.count() << "}\n";
        xmlXPathFreeCompExpr(expression); xmlXPathFreeContext(context); xmlFreeDoc(document); return 0;
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 1; }
}
