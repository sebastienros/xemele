# Approved XSD 1.0 time-policy exception

The original W3C case `msMeta/DataTypes_w3c.xml#g119/s:time_maxInclusive007_119` expects an invalid schema. Xemele accepts it; .NET 10.0.11 System.Xml rejects it. Preserve this original failure. The human approved this exact documented exception in reply to `call_b88f280c431643d2b5b911ee5fb92e63`: “Allow this documented exception (Recommended)”.

The base time bound is `12:00:00-10:00`; the derived bound is `12:00:00-14:00`.

| Reading | Normalized base | Normalized derived | Restriction result |
| --- | --- | --- | --- |
| Assign the same date before normalization | D 22:00Z | D+1 02:00Z | Invalid |
| Normalize recurring times before assigning a date | D 22:00Z | D 02:00Z | Valid |

[XSD 1.0 Second Edition section 3.2.8](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#time) describes recurring times but refers ordering to dateTime with an arbitrary date. Its UTC canonical-time rule, together with the [canonical/value correspondence](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#canonical-lexical-representation), supports the currently implemented recurring interpretation. [W3C R-229](https://www.w3.org/2001/05/xmlschema-rec-comments.html#pfiTimeOrderRelation) records this normalization-order dispute; related [issue 2116](https://www.w3.org/Bugs/Public/show_bug.cgi?id=2116) records the midnight conflict. Published [Second Edition errata](https://www.w3.org/2004/03/xmlschema-errata) supplies no Part 2 correction.

Independent source/standards review by `/root/review_m8_model` found no uniquely resolved provider defect. Removing modulo normalization would break verified equality and canonical round trips: a legal UTC time lexical cannot encode a retained previous/next-day key. Existing behavior is deliberate in TemporalProvider.cs, TemporalTests.RecurringTimesWrapUtcMidnightAndKeepZonePresence and canonical ConstraintTests.

Approved disposition: retain the existing internally consistent recurring-time policy, execute and report this exact unchanged original case as a standards-ambiguity exception, preserve expected=invalid and actual=schema-valid, and never count it as a passing upstream case. The M8 corpus/gate must explicitly report the exception instead of making an unqualified all-original-cases-pass claim. All concrete implementation defects remain required repairs. This changes strict gate acceptance only for this one identified case.

The filename is retained for existing links and provenance; the prior pending decision remains in Git history at `ef13fb703a8f7e1253c619b0c4b808a07467e4cf`. This approval does not authorize runtime changes, additional exceptions, or counting an unexecuted dependent instance as passing.
