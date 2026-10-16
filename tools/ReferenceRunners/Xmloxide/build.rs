use std::process::Command;
fn main() {
    let revision =
        std::env::var("XEMELE_BUILD_REVISION").expect("use build.py to set the source revision");
    let rust = Command::new(std::env::var("RUSTC").unwrap())
        .arg("--version")
        .output()
        .unwrap();
    println!("cargo:rustc-env=XEMELE_BUILD_REVISION={revision}");
    println!(
        "cargo:rustc-env=XEMELE_RUST_VERSION={}",
        String::from_utf8(rust.stdout).unwrap().trim()
    );
    println!(
        "cargo:rustc-env=XEMELE_TARGET={}",
        std::env::var("TARGET").unwrap()
    );
    println!("cargo:rerun-if-env-changed=XEMELE_BUILD_REVISION");
}
