use base64::{engine::general_purpose::STANDARD, Engine as _};
use serde_json::{json, Value};
use std::{
    collections::HashMap,
    io::{self, BufRead, Write},
    sync::{Arc, Mutex},
};
use xmloxide::{
    parser::{parse_str_with_options, ParseOptions},
    tree::{Document, NodeId, NodeKind},
};
const LINE: usize = 1_048_576;
const XML: usize = 1_048_576;
const COMMIT: &str = "555055f1ea6bf9944488fd96415b6f03b5b43696";

fn engine() -> Value {
    json!({"name":"xmloxide", "version":format!("0.5.0+{COMMIT}"),
        "runtime":format!("{}; {}",env!("XEMELE_RUST_VERSION"),env!("XEMELE_TARGET")),
        "build":format!("{}; release opt3 lto=false codegen-units16 panic=unwind; xmloxide default-features=false features=[]; locked",env!("XEMELE_BUILD_REVISION"))})
}
fn failure(outcome: &str, category: &str, message: impl ToString) -> Value {
    json!({"protocolVersion":1,"engine":engine(),"outcome":outcome,
        "diagnostics":[{"category":category,"message":message.to_string()}]})
}
fn protocol(message: &str) -> Value {
    failure("runner-failure", "protocol", message)
}
fn limit(message: &str) -> Value {
    failure("runner-failure", "limit", message)
}
fn decode(value: &Value) -> Result<Vec<u8>, Value> {
    STANDARD
        .decode(
            value
                .as_str()
                .ok_or_else(|| protocol("Expected base64 string."))?,
        )
        .map_err(|_| protocol("Invalid base64."))
}
fn optional_empty(v: &Value) -> bool {
    v.is_null() || v.as_object().is_some_and(|m| m.is_empty())
}
fn execute(request: &Value) -> Value {
    if !request.is_object()
        || request["protocolVersion"].as_i64() != Some(1)
        || !request["caseId"]
            .as_str()
            .is_some_and(|s| !s.trim().is_empty())
        || request["operation"] != "xml.events"
        || request["profile"] != "xml-1.0"
        || !optional_empty(&request["options"])
        || !(request["encodingHint"].is_null() || request["encodingHint"] == "utf-8")
    {
        return protocol(
            "Unsupported version, identity, operation, profile, options or encoding hint.",
        );
    }
    let input = match decode(&request["inputBase64"]) {
        Ok(v) => v,
        Err(e) => return e,
    };
    let mut resources = HashMap::new();
    if !request["offlineResources"].is_null() {
        let Some(map) = request["offlineResources"].as_object() else {
            return protocol("Expected offline resource object.");
        };
        for (key, value) in map {
            let uri = match url::Url::parse(key) {
                Ok(u) => u.to_string(),
                Err(_) => return protocol("Offline keys must be absolute URIs."),
            };
            let bytes = match decode(value) {
                Ok(v) => v,
                Err(e) => return e,
            };
            if resources.insert(uri, bytes).is_some() {
                return protocol("Duplicate canonical resource URI.");
            }
        }
    }
    let text = if request["encodingHint"] == "utf-8" {
        std::str::from_utf8(&input)
            .map(str::to_owned)
            .map_err(|e| e.to_string())
    } else {
        xmloxide::encoding::decode_to_utf8(&input).map_err(|e| e.to_string())
    };
    let text = match text {
        Ok(s) => s,
        Err(e) => return failure("parse-rejected", "xml", e),
    };
    if text.encode_utf16().count() > XML {
        return limit("Decoded XML exceeds document limit.");
    }
    // A request owns all resource state. No file, network, environment or global resolver is used.
    let resolver_error = Arc::new(Mutex::new(None::<(&'static str, String)>));
    let state = resolver_error.clone();
    if unsupported_parameter_dtd(&text) {
        return protocol(
            "Parameter entity DTD declarations unsupported by pinned xmloxide resolver API.",
        );
    }
    let options = ParseOptions::default().max_depth(256).max_name_length(50_000)
        .max_entity_expansions(10_000).entity_resolver(move |entity| {
            let uri = match url::Url::parse(entity.system_id) {
                Ok(u) => u.to_string(),
                Err(_) => { *state.lock().unwrap() = Some(("protocol", "Relative external entity URI unsupported: pinned API provides no base URI.".into())); return None }
            };
            match resources.get(&uri) {
                None => { *state.lock().unwrap() = Some(("resource-denied", format!("External resource denied: {uri}"))); None }
                Some(bytes) => match xmloxide::encoding::decode_to_utf8(bytes) {
                    Ok(s) if s.encode_utf16().count() <= XML => Some(s),
                    Ok(_) => { *state.lock().unwrap() = Some(("limit", "External resource exceeds document limit.".into())); None }
                    Err(e) => { *state.lock().unwrap() = Some(("xml", e.to_string())); None }
                }
            }
        });
    let parsed = parse_str_with_options(&text, &options);
    if let Some((category, message)) = resolver_error.lock().unwrap().take() {
        return failure(
            if category == "xml" {
                "parse-rejected"
            } else {
                "runner-failure"
            },
            category,
            message,
        );
    }
    let doc = match parsed {
        Ok(d) => d,
        Err(e) => {
            return if quota_error(&e.message) {
                limit(&e.message)
            } else {
                failure("parse-rejected", "xml", e.to_string())
            }
        }
    };
    // Upstream records external subsets but never loads them. Never emit incomplete events.
    for id in doc.descendants(doc.root()) {
        if let NodeKind::DocumentType {
            system_id,
            public_id,
            ..
        } = &doc.node(id).kind
        {
            if system_id.is_some() || public_id.is_some() {
                return protocol("External DTD subsets and parameter entities unsupported by pinned xmloxide resolver API.");
            }
        }
    }
    let mut events = Vec::new();
    let mut budget = XML;
    if let Err(e) = visit(&doc, doc.root(), &mut events, &mut budget) {
        return e;
    }
    json!({"protocolVersion":1,"engine":engine(),"outcome":"success","diagnostics":[],"events":events})
}
// The engine reserializes its DTD and drops parameter declarations. Inspect only
// an actual prolog DOCTYPE, respecting quoted literals/comments/PIs. No regex or
// document-wide substring matching; element text and CDATA are never inspected.
fn unsupported_parameter_dtd(text: &str) -> bool {
    let mut s = text.trim_start_matches('\u{feff}');
    loop {
        s = s.trim_start_matches([' ', '\t', '\r', '\n']);
        if let Some(rest) = s.strip_prefix("<!--") {
            let Some(end) = rest.find("-->") else {
                return false;
            };
            s = &rest[end + 3..];
        } else if let Some(rest) = s.strip_prefix("<?") {
            let Some(end) = rest.find("?>") else {
                return false;
            };
            s = &rest[end + 2..];
        } else {
            break;
        }
    }
    let Some(rest) = s.strip_prefix("<!DOCTYPE") else {
        return false;
    };
    if !rest.starts_with([' ', '\t', '\r', '\n']) {
        return false;
    }
    let b = rest.as_bytes();
    let mut i = 0;
    while i < b.len() {
        if b[i] == b'\'' || b[i] == b'"' {
            let quote = b[i];
            i += 1;
            while i < b.len() && b[i] != quote {
                i += 1;
            }
        } else if b[i] == b'[' {
            // Locate the subset's closing bracket without inspecting following content.
            let start = i + 1;
            let mut end = start;
            let mut quote = None;
            while end < b.len() {
                if let Some(q) = quote {
                    if b[end] == q {
                        quote = None;
                    }
                    end += 1;
                } else if b[end..].starts_with(b"<!--") {
                    end += 4;
                    while end < b.len() && !b[end..].starts_with(b"-->") {
                        end += 1;
                    }
                    end = (end + 3).min(b.len());
                } else if b[end..].starts_with(b"<?") {
                    end += 2;
                    while end < b.len() && !b[end..].starts_with(b"?>") {
                        end += 1;
                    }
                    end = (end + 2).min(b.len());
                } else if b[end] == b'\'' || b[end] == b'"' {
                    quote = Some(b[end]);
                    end += 1;
                } else if b[end] == b']' {
                    return parameter_entities(&rest[start..end]);
                } else {
                    end += 1;
                }
            }
            return false;
        } else if b[i] == b'>' {
            return false;
        }
        i += 1;
    }
    false
}
// Inspect only the parser's actual DTD internal subset; skip literal/comment/PI contents.
fn parameter_entities(s: &str) -> bool {
    let bytes = s.as_bytes();
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i..].starts_with(b"<!--") {
            i += 4;
            while i < bytes.len() && !bytes[i..].starts_with(b"-->") {
                i += 1;
            }
            i = (i + 3).min(bytes.len());
        } else if bytes[i..].starts_with(b"<?") {
            i += 2;
            while i < bytes.len() && !bytes[i..].starts_with(b"?>") {
                i += 1;
            }
            i = (i + 2).min(bytes.len());
        } else if bytes[i] == b'\'' || bytes[i] == b'"' {
            let quote = bytes[i];
            i += 1;
            while i < bytes.len() && bytes[i] != quote {
                i += 1;
            }
            i = (i + 1).min(bytes.len());
        } else if bytes[i] == b'%' {
            return true;
        } else {
            i += 1;
        }
    }
    false
}
// Match fixed pinned engine diagnostics, not arbitrary user-controlled substrings.
fn quota_error(s: &str) -> bool {
    if let Some(tail) = s.strip_prefix("entity '") {
        if let Some((name, error)) =
            tail.split_once("' replacement text is not well-formed XML content: ")
        {
            if !name.is_empty() && !name.contains('\'') {
                return quota_error(error);
            }
        }
    }
    s == "error in DTD internal subset: content model nesting exceeds maximum depth of 256"
        || s == "maximum nesting depth exceeded (256)"
        || s == "entity expansion limit exceeded (10000)"
        || s == "maximum entity amplification factor exceeded"
        || s.strip_prefix("name length (")
            .and_then(|t| t.strip_suffix(") exceeds maximum (50000)"))
            .is_some_and(|n| n.parse::<usize>().is_ok())
        || s.strip_prefix("entity '")
            .and_then(|t| t.strip_suffix("' exceeds maximum entity nesting depth"))
            .is_some_and(|name| !name.is_empty() && !name.contains('\''))
}
fn visit(
    doc: &Document,
    id: NodeId,
    events: &mut Vec<Value>,
    budget: &mut usize,
) -> Result<(), Value> {
    let kind = &doc.node(id).kind;
    if let NodeKind::Element {
        name,
        namespace,
        attributes,
        ..
    } = kind
    {
        let mut attrs: Vec<Value> = attributes.iter()
            .filter(|a| a.namespace.as_deref() != Some("http://www.w3.org/2000/xmlns/") && a.name != "xmlns" && a.prefix.as_deref() != Some("xmlns"))
            .map(|a| json!({"namespaceUri":a.namespace.as_deref().unwrap_or(""),"localName":a.name,"value":a.value})).collect();
        attrs.sort_by(|a, b| {
            a["namespaceUri"]
                .as_str()
                .unwrap()
                .encode_utf16()
                .cmp(b["namespaceUri"].as_str().unwrap().encode_utf16())
                .then_with(|| {
                    a["localName"]
                        .as_str()
                        .unwrap()
                        .encode_utf16()
                        .cmp(b["localName"].as_str().unwrap().encode_utf16())
                })
        });
        events.push(json!({"kind":"start","namespaceUri":namespace.as_deref().unwrap_or(""),"localName":name,"attributes":attrs}));
    } else if let NodeKind::Text { content } | NodeKind::CData { content } = kind {
        let n = content.encode_utf16().count();
        if n > *budget {
            return Err(limit("Normalized text exceeds document limit."));
        }
        *budget -= n;
        if let Some(last) = events.last_mut().filter(|e| e["kind"] == "text") {
            let mut value = last["value"].as_str().unwrap().to_owned();
            value.push_str(content);
            last["value"] = json!(value);
        } else {
            events.push(json!({"kind":"text","value":content}));
        }
    } else if let NodeKind::EntityRef { value: None, .. } = kind {
        return Err(protocol(
            "Unexpanded entity unsupported by pinned xmloxide.",
        ));
    }
    for child in doc.children(id) {
        visit(doc, child, events, budget)?;
    }
    if let NodeKind::Element {
        name, namespace, ..
    } = kind
    {
        events.push(json!({"kind":"end","namespaceUri":namespace.as_deref().unwrap_or(""),"localName":name}));
    }
    Ok(())
}
fn reply(line: &[u8], oversized: bool) -> Result<String, ()> {
    let text = if oversized {
        ""
    } else {
        std::str::from_utf8(line).map_err(|_| ())?
    };
    let mut response = if oversized || text.encode_utf16().count() > LINE {
        limit("Request exceeds line limit.")
    } else {
        match serde_json::from_str::<Value>(text) {
            Ok(request) => {
                let mut result = execute(&request);
                for key in ["caseId", "operation"] {
                    if request[key].is_string() {
                        result[key] = request[key].clone();
                    }
                }
                result
            }
            Err(_) => protocol("Malformed JSON request."),
        }
    };
    let mut output = response.to_string();
    if output.encode_utf16().count() > LINE {
        let mut limited = limit("Response exceeds line limit.");
        for key in ["caseId", "operation"] {
            if response[key].is_string() {
                limited[key] = response[key].take();
            }
        }
        output = limited.to_string();
        if output.encode_utf16().count() > LINE {
            output = limit("Identity cannot fit within response limit.").to_string();
        }
    }
    Ok(output)
}
// Streaming strict UTF-8 validation includes discarded oversized bytes. State
// handles buffer boundaries; no replacement characters or unbounded drain buffer.
#[derive(Default)]
struct WireUtf8 {
    remaining: u8,
    min: u8,
    max: u8,
}
impl WireUtf8 {
    fn accept(&mut self, bytes: &[u8]) -> bool {
        for &b in bytes {
            if self.remaining > 0 {
                if b < self.min || b > self.max {
                    return false;
                }
                self.remaining -= 1;
                self.min = 0x80;
                self.max = 0xbf;
            } else {
                let (remaining, min, max) = match b {
                    0x00..=0x7f => (0, 0x80, 0xbf),
                    0xc2..=0xdf => (1, 0x80, 0xbf),
                    0xe0 => (2, 0xa0, 0xbf),
                    0xe1..=0xec | 0xee..=0xef => (2, 0x80, 0xbf),
                    0xed => (2, 0x80, 0x9f),
                    0xf0 => (3, 0x90, 0xbf),
                    0xf1..=0xf3 => (3, 0x80, 0xbf),
                    0xf4 => (3, 0x80, 0x8f),
                    _ => return false,
                };
                self.remaining = remaining;
                self.min = min;
                self.max = max;
            }
        }
        true
    }
}
fn invalid_wire() -> ! {
    eprintln!("Invalid UTF-8 protocol input.");
    std::process::exit(2)
}
fn main() {
    let stdin = io::stdin();
    let mut input = stdin.lock();
    let stdout = io::stdout();
    let mut output = stdout.lock();
    let mut line = Vec::new();
    let mut oversized = false;
    let mut utf8 = WireUtf8::default();
    loop {
        let chunk = match input.fill_buf() {
            Ok(c) => c,
            Err(e) => {
                eprintln!("Protocol input failure: {e}");
                std::process::exit(2)
            }
        };
        if chunk.is_empty() {
            if utf8.remaining != 0 {
                invalid_wire();
            }
            if !line.is_empty() || oversized {
                emit(&line, oversized, &mut output)
            }
            break;
        }
        let end = chunk.iter().position(|b| *b == b'\n');
        let n = end.unwrap_or(chunk.len());
        let consumed = n + usize::from(end.is_some());
        if !utf8.accept(&chunk[..consumed]) {
            invalid_wire();
        }
        // Four bytes per UTF-16 code unit is a conservative raw wire bound.
        let available = (LINE * 4).saturating_sub(line.len());
        line.extend_from_slice(&chunk[..n.min(available)]);
        if n > available {
            oversized = true;
        }
        input.consume(consumed);
        if end.is_some() {
            emit(&line, oversized, &mut output);
            line.clear();
            oversized = false;
        }
    }
}
fn emit(line: &[u8], oversized: bool, output: &mut impl Write) {
    let response = match reply(line, oversized) {
        Ok(s) => s,
        Err(_) => {
            eprintln!("Invalid UTF-8 protocol input.");
            std::process::exit(2)
        }
    };
    if writeln!(output, "{response}")
        .and_then(|_| output.flush())
        .is_err()
    {
        eprintln!("Protocol output failure.");
        std::process::exit(2)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn quota_diagnostics_cannot_be_forged_by_entity_names() {
        assert!(quota_error("entity expansion limit exceeded (10000)"));
        assert!(!quota_error(
            "unknown entity reference: &entity_expansion_limit_exceeded;"
        ));
        assert!(!quota_error("name length (oops) exceeds maximum (50000)"));
    }
    #[test]
    fn dtd_literals_comments_and_processing_instructions_are_not_parameter_entities() {
        assert!(!parameter_entities(
            "<!-- %x; --> <?pi %x;?> <!ENTITY e '100% SYSTEM'>"
        ));
        assert!(parameter_entities("<!ENTITY % e 'x'>"));
        assert!(parameter_entities("%e;"));
        assert!(!unsupported_parameter_dtd(
            "<!-- <!DOCTYPE r [ %e; ]> --> <r><![CDATA[<!DOCTYPE r [%e;]>]]></r>"
        ));
        assert!(!unsupported_parameter_dtd(
            "<!DOCTYPE r [<!-- ] %e; --> <!ENTITY e 'SYSTEM &#37;'>]><r/>"
        ));
        assert!(unsupported_parameter_dtd(
            "<?xml version='1.0'?> <!--x--> <!DOCTYPE r [<!ENTITY % e 'x'> %e;]><r/>"
        ));
    }
    #[test]
    fn ordinal_is_utf16_not_scalar() {
        assert!("\u{10000}"
            .encode_utf16()
            .cmp("\u{e000}".encode_utf16())
            .is_lt());
        assert!("\u{10000}".cmp("\u{e000}").is_gt());
    }
}
