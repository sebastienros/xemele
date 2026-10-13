# Second root check failure and stdout control correction

Tested source: `aeda803f69a8955594b88e5d16c574ab1bd6785c`.
Root observed normal probe1156 assertions/288 fuzz cases, true XML-only
probe1157/288, and all10 guard controls passing. The real process-controls
command3 then failed at line77: authored closed-pipe expectation was exit3 with
stderr, but actual outcome was exit0/empty stderr. That original expectation
remains a failed historical control; it is not relabeled as passing delivery.
Three later check commands were unrun. Root observed all14 owned groups absent,
matched source/runtime/binaries, inner raw deleted, and no deadline failure.

Exact installed runtime10.0.11 `System.Console.dll` SHA256:
`3000a5a442b77ac67d41a61bb5cfce64742f36b9cad246a4aa1466423d6125a3`.
Root confirmed by read-only decompilation with cached dotnet-inspect0.26.0 that
`ConsolePal.Write` returns on EPIPE, polls on EAGAIN and throws other write errors.
Matching primary [v10.0.11 source](https://raw.githubusercontent.com/dotnet/runtime/v10.0.11/src/libraries/System.Console/src/System/ConsolePal.Unix.cs)
shows this behavior at lines897–921. Worker inspection only hashed the installed
module and read primary source; no runtime diagnostic was launched here.

Root approved keeping production `Console.OpenStandardOutput` unchanged. The
corrected closed-pipe case explicitly observes pinned macOS/.NET EPIPE suppression:
validation may exit0 although the consumer received no staged SVRL. This is a
documented delivery limitation, not a delivery pass or proof of exit3 on I/O error.
A separate real subprocess control opens an owned sentinel file read-only as
stdout and requires actual exit3, `Internal failure:` stderr and unchanged bytes.
Both controls inherit the registered owned group and retain105 seconds total,
15 seconds clipped child timeout and3 seconds reaping. Prior CLI expectations and
file atomicity are unchanged. Sample/report wording distinguishes observable
write exceptions from suppressed broken pipes; emitted stdout bytes cannot roll back.

These are source-only changes. The new read-only-descriptor control is unrun until
root's rebuilt reviewed sequence. Preparation receipts/certificate remain untouched;
task criteria and status are unchanged.
