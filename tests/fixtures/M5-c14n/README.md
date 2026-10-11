# Canonicalization vectors

These six repository-authored byte vectors supplement the focused C# tests.
They cover inclusive versus exclusive namespace context, exclusive prefix input,
comment/outer-root LF and C14N1.1 leading relative-base segments. Algorithm rules
are pinned to W3C [C14N1.0](https://www.w3.org/TR/2001/REC-xml-c14n-20010315),
[C14N1.1](https://www.w3.org/TR/2008/REC-xml-c14n11-20080502/) and
[Exclusive1.0](https://www.w3.org/TR/2002/REC-xml-exc-c14n-20020718/).
No remote corpus or fetch is needed to execute these fixtures.

The focused published section 3.8 subset test uses the official C14N1.1
[erratum E11-01](https://www.w3.org/2008/05/xml-c14n11-errata): the e3 base is
`bar/foo`, correcting the original publication's `something/bar/foo`. Section
2.4 stops the contiguous omitted-base chain at any included ancestor; carrying
`something/` through included e1 would duplicate its rendered base context.
