# Exploratory attempts before final revision

Initial branch creation found the coordinator had already created the assigned
branch; sandbox required escalation for isolated checkout Git metadata. The branch
then matched the claim and verified protocol ancestry. No main/peer edits occurred.

Rust1.95.0 provisioned successfully. First source-clean assertion failed on Cargo's
untracked `.cargo-ok` marker after compilation; checker now allows only that marker
and rejects any other source-cache change. No source patch was made.

Early comparison runs exposed real pinned CRLF attribute/CDATA and prolog whitespace
differences; output remains actual, and independent baseline/divergence checks now
record them. One adversarial fixture used a literal percent in a general entity
value: both engines rejected this invalid XML, so the positive fixture was corrected
to a character reference. Its initial rejection was not claimed as passing.

Parameter declarations disappear during upstream DTD serialization, so tree-only
inspection initially missed them. Prolog-only lexical inspection was added with
quotes/comment/PI/text/CDATA adversarial cases. Nested guard diagnostics and DTD
content-model guards were audited and categorized as limits. Oversized wire drains
now retain strict incremental UTF8 validation, including malformed bytes past the
storage cap. The final verification is run after the implementation commit and
supersedes these exploratory attempts. No unrun or failed attempt is passing evidence.
