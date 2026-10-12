# errF001 Second Edition oracle adjudication

Exact case: `msMeta/Errata10_w3c.xml#g18/s:errF001`, original fixture `msData/errata10/errF001.xsd`, SHA-256 `1a2485b85be6ef584242d06d1911580f6f7c68dcc539deb5034e52abdeb6ce46`, suite revision `7bc3365c652a322f3d762021b3879eb92dae7e30`.

Preserve the original VALID expectation, queried status dated 2007-06-21, issue link, and actual Xemele rejection. Do not count rejection as agreement with the original oracle. This is a separate coordinator standards disposition, independently reviewed by `/root/review_m8_contract` and root from primary sources; it is not a completed W3C issue resolution.

The schema's `st` introduces `length=5`; derived `st2` introduces `maxLength=5`. Although the numeric inequality holds, no ancestor supplies that same maximum without a length facet. [Second Edition §4.3.1.4 clause 2.2](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#length-coss) therefore makes this schema INVALID. [E2-66](https://www.w3.org/2001/05/xmlschema-errata.html#E2-66) supersedes the older E2-35 rule used by the test. [Issue 4681](https://www.w3.org/Bugs/Public/show_bug.cgi?id=4681) identifies this exact metadata defect and remains NEW.

No product repair is warranted for rejecting these original bytes. Report original-oracle disagreement separately from conformance to the adjudicated Second Edition expectation. The dependent instance remains `schema-unavailable` and unexecuted, never an invalid-instance pass. This disposition is restricted to this exact schema and its dependent-instance execution status; it does not blanket-exclude queried tests or waive any product defect.
