# Frozen runtime corpus coverage

The owned inventory is byte-identical to the M4-contracts freeze (SHA256 `87df3cf2ed58ef577d12186b23cd289b702269242629389c2a9e58b7dd0930e7`). All 105 IDs execute the public production XPathExpression facade. Expected data is copied unchanged, not generated from current runtime results. Main context is the second x tree handle; the ID document uses the root element.

| Group | Runtime vectors | Oracle |
| --- | ---: | --- |
| Axes | 13 | Exact item identity addresses in document order; every supplied reverse proximity sequence additionally checked with successive step predicates |
| Operators | 15 | Frozen typed scalar/node-set results, including short circuit and unary minus |
| Functions | 27 | Frozen typed results, including retained DTD IDs and Unicode scalar length |
| Grammar/edge/error | 50 | Frozen results or exact diagnostic codes; negative zero checked by IEEE bit identity |

The separate integrity test requires byte identity, 105 unique IDs and actual .NET 10.0.11. No expectation edits, exclusions, skipped vectors or later milestone dependencies.

| Acceptance ID | Feature | Expression | Frozen expectation |
| --- | --- | --- | --- |
| M4-contracts-A2-axis-01 | child | child::node() | ["/r/x[2]/text()[1]"] |
| M4-contracts-A2-axis-02 | descendant | descendant::node() | ["/r/x[2]/text()[1]"] |
| M4-contracts-A2-axis-03 | parent | parent::node() | ["/r"] |
| M4-contracts-A2-axis-04 | ancestor | ancestor::node() | ["/", "/r"] |
| M4-contracts-A2-axis-05 | following-sibling | following-sibling::node() | ["/r/p:y"] |
| M4-contracts-A2-axis-06 | preceding-sibling | preceding-sibling::node() | ["/r/x[1]", "/r/comment()[1]", "/r/processing-instruction()[1]"] |
| M4-contracts-A2-axis-07 | following | following::node() | ["/r/p:y"] |
| M4-contracts-A2-axis-08 | preceding | preceding::node() | ["/r/x[1]", "/r/x[1]/text()[1]", "/r/comment()[1]", "/r/processing-instruction()[1]"] |
| M4-contracts-A2-axis-09 | attribute | attribute::node() | ["/r/x[2]/@n"] |
| M4-contracts-A2-axis-10 | namespace | namespace::node() | ["/r/x[2]/namespace::p", "/r/x[2]/namespace::xml"] |
| M4-contracts-A2-axis-11 | self | self::node() | ["/r/x[2]"] |
| M4-contracts-A2-axis-12 | descendant-or-self | descendant-or-self::node() | ["/r/x[2]", "/r/x[2]/text()[1]"] |
| M4-contracts-A2-axis-13 | ancestor-or-self | ancestor-or-self::node() | ["/", "/r", "/r/x[2]"] |
| M4-contracts-A2-operator-01 | or | true() or $missing | true |
| M4-contracts-A2-operator-02 | and | false() and $missing | false |
| M4-contracts-A2-operator-03 | = | 2 = "2" | true |
| M4-contracts-A2-operator-04 | != | 2 != 3 | true |
| M4-contracts-A2-operator-05 | < | 2 < 3 | true |
| M4-contracts-A2-operator-06 | <= | 2 <= 2 | true |
| M4-contracts-A2-operator-07 | > | 3 > 2 | true |
| M4-contracts-A2-operator-08 | >= | 2 >= 2 | true |
| M4-contracts-A2-operator-09 | + | 1 + 2 | 3 |
| M4-contracts-A2-operator-10 | - | 5 - 2 | 3 |
| M4-contracts-A2-operator-11 | * | 3 * 2 | 6 |
| M4-contracts-A2-operator-12 | div | 5 div 2 | 2.5 |
| M4-contracts-A2-operator-13 | mod | -5 mod 2 | -1 |
| M4-contracts-A2-operator-14 | &#124; | /r/x[2] &#124; /r/x[1] &#124; /r/x[1] | ["/r/x[1]", "/r/x[2]"] |
| M4-contracts-A2-operator-15 | unary-minus | --2 | 2 |
| M4-contracts-A2-function-01 | last | last() | 1 |
| M4-contracts-A2-function-02 | position | position() | 1 |
| M4-contracts-A2-function-03 | count | count(/r/x) | 2 |
| M4-contracts-A2-function-04 | id | id("one one missing") | ["/r/x[1]"] |
| M4-contracts-A2-function-05 | local-name | local-name(/r/p:y) | "y" |
| M4-contracts-A2-function-06 | namespace-uri | namespace-uri(/r/p:y) | "urn:p" |
| M4-contracts-A2-function-07 | name | name(/r/p:y) | "p:y" |
| M4-contracts-A2-function-08 | string | string(/r/x[1]) | "ABC" |
| M4-contracts-A2-function-09 | concat | concat("a","b","c") | "abc" |
| M4-contracts-A2-function-10 | starts-with | starts-with("abc","ab") | true |
| M4-contracts-A2-function-11 | contains | contains("abc","b") | true |
| M4-contracts-A2-function-12 | substring-before | substring-before("abc","b") | "a" |
| M4-contracts-A2-function-13 | substring-after | substring-after("abc","b") | "c" |
| M4-contracts-A2-function-14 | substring | substring("12345",1.5,2.6) | "234" |
| M4-contracts-A2-function-15 | string-length | string-length("A😀B") | 3 |
| M4-contracts-A2-function-16 | normalize-space | normalize-space("  A\t B\n ") | "A B" |
| M4-contracts-A2-function-17 | translate | translate("abba","ab","X") | "XX" |
| M4-contracts-A2-function-18 | boolean | boolean(/r/x) | true |
| M4-contracts-A2-function-19 | not | not(/r/missing) | true |
| M4-contracts-A2-function-20 | true | true() | true |
| M4-contracts-A2-function-21 | false | false() | false |
| M4-contracts-A2-function-22 | lang | lang("en") | true |
| M4-contracts-A2-function-23 | number | number(" -2.5 ") | -2.5 |
| M4-contracts-A2-function-24 | sum | sum(/r/x/@n) | 3 |
| M4-contracts-A2-function-25 | floor | floor(-1.2) | -2 |
| M4-contracts-A2-function-26 | ceiling | ceiling(-1.2) | -1 |
| M4-contracts-A2-function-27 | round | round(-1.5) | -1 |
| M4-contracts-A2-case-01 | root | / | ["/"] |
| M4-contracts-A2-case-02 | abbreviations | //x/../x[1] | ["/r/x[1]"] |
| M4-contracts-A2-case-03 | attribute-short | @n | ["/r/x[2]/@n"] |
| M4-contracts-A2-case-04 | self-short | . | ["/r/x[2]"] |
| M4-contracts-A2-case-05 | parent-short | .. | ["/r"] |
| M4-contracts-A2-case-06 | namespace-wildcard | /r/p:* | ["/r/p:y"] |
| M4-contracts-A2-case-07 | principal-wildcard | /r/* | ["/r/x[1]", "/r/x[2]", "/r/p:y"] |
| M4-contracts-A2-case-08 | all-node-tests | /r/node() | ["/r/x[1]", "/r/comment()[1]", "/r/processing-instruction()[1]", "/r/x[2]", "/r/p:y"] |
| M4-contracts-A2-case-09 | pi-literal | /r/processing-instruction("go") | ["/r/processing-instruction()[1]"] |
| M4-contracts-A2-case-10 | text-coalesce | count(/r/x[1]/text()) | 1 |
| M4-contracts-A2-case-11 | filter-path | (/r/x)[1]/@n | ["/r/x[1]/@n"] |
| M4-contracts-A2-case-12 | reverse-predicate | preceding-sibling::node()[1] | ["/r/processing-instruction()[1]"] |
| M4-contracts-A2-case-13 | filter-order | (preceding-sibling::node())[1] | ["/r/x[1]"] |
| M4-contracts-A2-case-14 | predicate-focus | /r/x[position()=last()] | ["/r/x[2]"] |
| M4-contracts-A2-case-15 | predicate-sequential | /r/x[@n="2"][1] | ["/r/x[2]"] |
| M4-contracts-A2-case-16 | precedence | 1 + 2 * 3 = 7 and true() | true |
| M4-contracts-A2-case-17 | variable-expanded | $p:v + 1 | 3 |
| M4-contracts-A2-case-18 | existential-not-equal | /r/x/@n != /r/x/@n | true |
| M4-contracts-A2-case-19 | set-boolean | /r/missing = false() | true |
| M4-contracts-A2-case-20 | set-relational | /r/x/@n < 2 | true |
| M4-contracts-A2-case-21 | nan | number("+2") | "NaN" |
| M4-contracts-A2-case-22 | division-infinity | 1 div 0 | "Infinity" |
| M4-contracts-A2-case-23 | negative-zero | round(-0.5) | "-0" |
| M4-contracts-A2-case-24 | sum-empty | sum(/r/missing) | 0 |
| M4-contracts-A2-case-25 | namespace-string | string(namespace::p) | "urn:p" |
| M4-contracts-A2-case-26 | node-name-empty | name(/r/x[1]/text()) | "" |
| M4-contracts-A2-case-27 | optional-name | name() | "x" |
| M4-contracts-A2-case-28 | optional-string | string() | "D" |
| M4-contracts-A2-case-29 | optional-number | number() | "NaN" |
| M4-contracts-A2-case-30 | optional-length | string-length() | 1 |
| M4-contracts-A2-case-31 | optional-normalize | normalize-space() | "D" |
| M4-contracts-A2-case-32 | optional-local | local-name() | "x" |
| M4-contracts-A2-case-33 | optional-uri | namespace-uri() | "" |
| M4-contracts-A2-case-34 | substring-nan | substring("123",0 div 0) | "" |
| M4-contracts-A2-case-35 | substring-infinity | substring("123",-1 div 0,1 div 0) | "" |
| M4-contracts-A2-case-36 | empty-string-number | number("") | "NaN" |
| M4-contracts-A2-case-37 | nonxml-whitespace | number(" 1") | "NaN" |
| M4-contracts-A2-case-38 | translate-duplicate | translate("aba","aba","XYQ") | "XYX" |
| M4-contracts-A2-case-39 | literal-quotes | concat("a",'b') | "ab" |
| M4-contracts-A2-case-40 | decimal | 0.5 + .5 + 1. | 2 |
| M4-contracts-A2-case-41 | keyword-name | count(/r/div) | 0 |
| M4-contracts-A2-case-42 | unbound-variable | $missing | "XPATH-VARIABLE" |
| M4-contracts-A2-case-43 | bad-prefix | q:x | "XPATH-PREFIX" |
| M4-contracts-A2-case-44 | bad-function | unknown() | "XPATH-FUNCTION" |
| M4-contracts-A2-case-45 | bad-arity | count() | "XPATH-ARITY" |
| M4-contracts-A2-case-46 | bad-token | 1e2 | "XPATH-SYNTAX" |
| M4-contracts-A2-case-47 | bad-step | /r/ | "XPATH-SYNTAX" |
| M4-contracts-A2-case-48 | bad-union | 1 &#124; /r | "XPATH-TYPE" |
| M4-contracts-A2-case-49 | bad-pi-test | processing-instruction(1) | "XPATH-SYNTAX" |
| M4-contracts-A2-case-50 | xpath2 | if (true()) then 1 else 2 | "XPATH-SYNTAX" |
