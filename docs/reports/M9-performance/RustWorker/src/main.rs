use std::{error::Error, fs, time::{Duration, Instant}};
use xmloxide::{Document, tree::{NodeId, NodeKind}, xpath::{parser, XPathContext}};

fn frame(output: &mut Vec<u8>, kind: u8, values: &[&str]) {
    output.push(kind);
    for value in values { output.extend_from_slice(&(value.len() as u32).to_le_bytes()); output.extend_from_slice(value.as_bytes()); }
}
fn semantic(doc: &Document, id: NodeId, output: &mut Vec<u8>) -> Result<(), Box<dyn Error>> {
    match &doc.node(id).kind {
        NodeKind::Comment{content} => frame(output, b'C', &[content]),
        NodeKind::ProcessingInstruction{target,data} => frame(output, b'P', &[target,data.as_deref().unwrap_or("")]),
        NodeKind::Element{name,namespace,attributes,..} => {
            frame(output, b'S', &[namespace.as_deref().unwrap_or(""),name]);
            let mut attrs: Vec<_> = attributes.iter().filter(|a| a.name != "xmlns" && a.prefix.as_deref() != Some("xmlns")).collect();
            attrs.sort_by_key(|a| (a.namespace.as_deref().unwrap_or(""),a.name.as_str()));
            for a in attrs { frame(output, b'A', &[a.namespace.as_deref().unwrap_or(""),&a.name,&a.value]); }
            let mut pending = String::new();
            for child in doc.children(id) {
                match &doc.node(child).kind {
                    NodeKind::Text{content}|NodeKind::CData{content} => pending.push_str(content),
                    NodeKind::EntityRef{..} => pending.push_str(&doc.text_content(child)),
                    _ => { if !pending.is_empty() { frame(output,b'T', &[&pending]); pending.clear(); } semantic(doc,child,output)?; }
                }
            }
            if !pending.is_empty() { frame(output,b'T', &[&pending]); }
            frame(output,b'E', &[]);
        }
        _ => return Err("Unexpected common-model node".into()),
    }
    Ok(())
}
fn parse(bytes: &[u8]) -> Result<Document,Box<dyn Error>> {
    let text = xmloxide::encoding::decode_to_utf8(bytes)?;
    let options = xmloxide::parser::ParseOptions { max_entity_expansions: 1000000, ..Default::default() };
    Ok(xmloxide::parser::parse_str_with_options(&text, &options)?)
}
fn main() -> Result<(),Box<dyn Error>> {
    let args: Vec<_> = std::env::args().collect();
    if args.len()!=6 { return Err("input expression qualify|parse|lifecycle|serialize|xpath iterations output-prefix".into()); }
    let bytes=fs::read(&args[1])?;
    if bytes.is_empty() || bytes.len()>16777216 { return Err("Invalid frozen fixture bytes".into()); }
    let mut doc=parse(&bytes)?;
    // All output workers serialize UTF-8; setup precedes the timed operations.
    doc.encoding=Some("UTF-8".to_string());
    let expression=parser::parse(&args[2])?;
    let context=XPathContext::new(&doc,doc.root());
    let expected=context.evaluate(&expression)?.to_number();
    let iterations: u32=args[4].parse()?;
    if iterations==0 || iterations>1000000 { return Err("Invalid iterations".into()); }
    let mut consumed=0usize; let mut elapsed=Duration::ZERO;
    if args[3]=="qualify" {
        let mut output=Vec::new(); semantic(&doc,doc.root_element().ok_or("No root element")?,&mut output)?;
        fs::write(format!("{}.events",args[5]),output)?;
        let serialized=xmloxide::serial::serialize(&doc); consumed=serialized.len(); fs::write(format!("{}.xml",args[5]),serialized.as_bytes())?;
    } else { for _ in 0..iterations {
        let start=Instant::now();
        match args[3].as_str() {
            "parse"|"lifecycle" => { let value=parse(&bytes)?; consumed+=usize::from(value.root_element().is_some()); if args[3]=="parse" {elapsed+=start.elapsed();} drop(value); if args[3]=="lifecycle" {elapsed+=start.elapsed();} }
            "serialize" => {let value=xmloxide::serial::serialize(&doc); elapsed+=start.elapsed(); consumed+=value.len(); std::hint::black_box(&value);}
            "xpath" => {let value=context.evaluate(&expression)?; elapsed+=start.elapsed(); if value.to_number()!=expected{return Err("XPath result changed".into());} consumed+=value.to_number() as usize; std::hint::black_box(&value);}
            _=>return Err("Unknown operation".into()),
        }
    }}
    println!("{}",serde_json::json!({"engine":"xmloxide","engineRevision":"555055f1ea6bf9944488fd96415b6f03b5b43696","xpath":expected,"consumed":consumed,"iterations":iterations,"steadyNanoseconds":elapsed.as_nanos()}));
    Ok(())
}
