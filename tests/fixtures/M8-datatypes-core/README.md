# Datatype core fixtures

Focused cases are authored inline in CoreTests.cs. Unpaired-surrogate cases are
constructed from integer code units at runtime: custom-attribute string encoding
cannot preserve an isolated UTF-16 surrogate. Whitespace fixtures include XML
9/A/D/20, NBSP, NEL, em-space, Arabic/Greek/CJK and supplementary emoji.

Measurement fixtures are prepared before the lease: short ASCII ` value `;
1,024 literal x characters; multilingual `\t根😀 العربية Ελληνικά\r`;
and 512 spaces + `token\t\r\nvalue` + 512 spaces. Each BDN pair consumes the same
fixture and two scalar passes, the same shared-operation work charges and a
preallocated output. Probe input is `\t根😀  value\r`, plus the 1,024-x fixture.
Input SHA256 is recorded by the prepared control and measurement probe.
