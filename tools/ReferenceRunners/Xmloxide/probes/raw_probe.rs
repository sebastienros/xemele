// Reproduction-only probe: no v1 success response. Actual unmodified native DOM.
use serde_json::json;
use std::io::{self, Read};
use xmloxide::tree::NodeKind;
fn main() {
    let mut input = Vec::new();
    io::stdin().read_to_end(&mut input).unwrap();
    let text = xmloxide::encoding::decode_to_utf8(&input).unwrap();
    let doc = xmloxide::Document::parse_str(&text).unwrap();
    let nodes: Vec<_> = doc
        .children(doc.root())
        .map(|id| match &doc.node(id).kind {
            NodeKind::Element { name, .. } => json!({"kind":"element","name":name}),
            NodeKind::Text { content } => json!({"kind":"text","value":content}),
            other => json!({"kind":"other","debug":format!("{other:?}")}),
        })
        .collect();
    println!(
        "{}",
        json!({"engineCommit":"555055f1ea6bf9944488fd96415b6f03b5b43696", "rawDocumentChildren":nodes})
    );
}
