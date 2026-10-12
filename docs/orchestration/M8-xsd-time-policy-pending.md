# Pending XSD 1.0 time-policy decision

The original W3C case `msMeta/DataTypes_w3c.xml#g119/s:time_maxInclusive007_119` expects an invalid schema. Xemele accepts it; .NET 10.0.11 System.Xml rejects it. Preserve this original failure. No repair or exception has been authorized by this note.

The base time bound is `12:00:00-10:00`; the derived bound is `12:00:00-14:00`.

| Reading | Normalized base | Normalized derived | Restriction result |
| --- | --- | --- | --- |
| Assign the same date before normalization | D 22:00Z | D+1 02:00Z | Invalid |
| Normalize recurring times before assigning a date | D 22:00Z | D 02:00Z | Valid |

[XSD 1.0 Second Edition section 3.2.8](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#time) describes recurring times but refers ordering to dateTime with an arbitrary date. Its UTC canonical-time rule, together with the [canonical/value correspondence](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#canonical-lexical-representation), supports the currently implemented recurring interpretation. [W3C R-229](https://www.w3.org/2001/05/xmlschema-rec-comments.html#pfiTimeOrderRelation) records this normalization-order dispute; related [issue 2116](https://www.w3.org/Bugs/Public/show_bug.cgi?id=2116) records the midnight conflict. Published [Second Edition errata](https://www.w3.org/2004/03/xmlschema-errata) supplies no Part 2 correction.

Independent source/standards review by `/root/review_m8_model` found no uniquely resolved provider defect. Removing modulo normalization would break verified equality and canonical round trips: a legal UTC time lexical cannot encode a retained previous/next-day key. Existing behavior is deliberate in TemporalProvider.cs, TemporalTests.RecurringTimesWrapUtcMidnightAndKeepZonePresence and canonical ConstraintTests.

Proposed disposition for user decision: retain the existing internally consistent recurring-time policy, execute and report this exact unchanged original case as a standards-ambiguity exception, preserve expected=invalid and actual=schema-valid, and never count it as a passing upstream case. The M8 corpus/gate would explicitly report the exception instead of making an unqualified all-original-cases-pass claim. All concrete implementation defects remain required repairs. This changes the strict gate acceptance for this one identified case and therefore remains pending the user's decision.

Alternative: keep this exact case as a gate blocker while the time-policy and canonical contract receive further design resolution. Other corpus and implementation work continues independently.
