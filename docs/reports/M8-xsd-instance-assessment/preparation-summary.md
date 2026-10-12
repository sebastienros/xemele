# Preparation and disabled supervisor preflight

Frozen source/probe revision `092a3ba4b4fc513e3439a496fef690a1a0563a99`; accepted correctness/runtime
revision `16687e8eae341b184f891a1f720e8c29be9e13fc`. Actual `python3 docs/reports/M8-xsd-instance-assessment/prepare.py`:
offline restore and pinned SDK10.0.400/.NET10.0.11 Release build passed with
0warnings/errors; all16owned validation routes passed.1first+16warm+64aggregate
calls/route plus16retained calls =1312calls and1968consumed units. Every allocation
counter/GC reading/benchmark timer was disabled; firstBytes/bytes/coldSetupBytes/
positiveBytes/retainedBefore/retainedAfterResults all0. No allocation result claim.

Actual elevated `python3 docs/reports/M8-xsd-instance-assessment/measure.py --verify-only`
matched462frozen identities and proved ps permission. Actual elevated
`python3 docs/reports/M8-xsd-instance-assessment/measure.py --preflight` ran only
--control:PID291,exit0,directChildReaped=true,ownedGroupAbsent=true,
groupMembers=[],logClosed=true;462before/after identities matched.

ManifestSHA256 `42cb79cc5bd5626c123e2256b93185ffbc19f5b72650afee3465f23087138312`;
supervisorSHA256 `2a74dfa9dd82c398f4a356e1a890ecd7f48af9bc53ce57c067a287361667c7d4`.
TERM/KILL/reap/group-absence/log-close core is unchanged from the accepted
attribute-fixed supervisor; only task path/environment naming changes. No worker
--measure, BDN or throughput timer launched. Raw controls/lifecycle are transient
under ignored artifacts; only these concise outcomes/hashes survive here.
