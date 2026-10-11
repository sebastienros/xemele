use std::io::{self, Read};
use serde_json::{json, Value};
use xmloxide::{css, Document};
fn main() {
    let mut input = String::new(); io::stdin().read_to_string(&mut input).unwrap();
    let source: Value = serde_json::from_str(&input).unwrap();
    let doc = Document::parse_str(source["xml"].as_str().unwrap()).unwrap();
    let root = doc.root_element().unwrap();
    let rows: Vec<Value> = source["vectors"].as_array().unwrap().iter().map(|v| {
        let selector = v["css"].as_str().unwrap();
        match css::select(&doc, root, selector) {
            Ok(nodes) => json!({"css":selector,"ids":nodes.iter().map(|n|doc.attribute(*n,"id").unwrap()).collect::<Vec<_>>()}),
            Err(error) => json!({"css":selector,"error":error.to_string()})
        }
    }).collect();
    println!("{}", serde_json::to_string_pretty(&rows).unwrap());
}
