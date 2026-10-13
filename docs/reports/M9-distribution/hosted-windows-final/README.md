# Required OS distribution matrix passed

[Windows run38460364837](https://github.com/sebastienros/xemele/actions/runs/38460364837), attempt1 at `b2e2457c4f3355d54c7afae64dc5f3033b650b5a`, passed the single approved Windowsx64 job in8.05 elapsed minutes under its20minute cap. This consumed the one-job authorization; no extra workflow was dispatched.

| Actual RID | Run | Units passed | Commands | Managed/trim/NativeAOT/tool |
| --- | --- | --- | --- | --- |
| linux-x64 |38457602484|10,754|21|All passed|
| osx-arm64 |38457602484|10,754|21|All passed|
| win-x64 |38460364837|10,754|23|All passed|

Every suite has zero failures/errors/skips/not-run. Each OS built two byte-identical canonical runtime/tool package pairs, checked notices/docs/license and embedded exact-revision Source Link, restored an isolated PackageReference consumer, executed managed, trimmed and NativeAOT XML/XPath/SAX/HTML/CSS/RNG/XSD/Schematron/SVRL usage, and installed/executed tool help. SDK10.0.400 and runtime10.0.11 are pinned. All3,386 original corpus files were hash verified per OS. Windows used the explicit source configuration; its23 direct children were reaped and logs closed. Per-command Windows process-group absence is not present in the report; Unix reports do confirm it.

Linux/macOS evidence is at a19b7ea8; Windows evidence is at b2e2457c. The exact runtime, unit, fixture, consumer, package/build inputs are unchanged between them. The later changes affect Windows feed configuration and workflow selection; Unix consumer arguments retain their earlier behavior. Each package records its actual commit in Source Link. Windows and Unix archive hashes are separate; clean-rebuild equality is within each OS, with Linux/macOS additionally matching each other.

`matrix.json` binds these exact reports and unchanged source paths. Failed earlier runs remain preserved. Embedded Source Link was tested; remote source fetching was not. These shared-runner results verify the distribution lane only, supplying no dedicated timing or sustained fuzz evidence. No publication, remote main/tag changes or fuzz work occurred.
