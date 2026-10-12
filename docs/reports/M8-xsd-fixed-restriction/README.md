# Complete particle restriction join

Complete XSD composition must replace its structural compilation call with:

```csharp
new XsdContentCompiler().Compile(schema, type, sharedOperation, sharedFacets);
```

`schema.Datatypes` must be the sealed executable registry supplied by the complete
composition, including its shared XSD library/provider set. `sharedFacets` is the
composition's existing facet evaluator, and `sharedOperation` is the aggregate
compile ledger. The overload has no executable-registry detection or fallback.
The frozen `IXsdContentCompiler.Compile(schema, type, operation)` remains the
structural lane and compiles with declaration-model dependencies alone.

The existing all-type compilation visits unused definitions and retained
redefine carriers. At each actual element-to-element restriction relation, after
the existing structural checks, a base fixed constraint causes both authored
lexicals to be validated against their respective effective simple datatypes and
declaration namespace contexts. Emptiable mixed content uses the string value
space. The supplied library compares the owned typed values directly; no
canonical string replaces their identity. Values live only through the current
relation evaluation. Datatype/facet implementations account for their owned parse
results and prepared plans using the same operation.

An unequal pair becomes a false relation in the existing AND/OR solver. It does
not emit an error while another particle mapping can still succeed. Identical
particle/global declaration shortcuts retain their existing behavior; they refer
to the same declaration. Unrelated declarations with the same expanded name are
never paired outside relation solving. Attribute-use lexical restriction rules
remain the existing structural rules.

The overload adds no public API and does not change frozen interfaces. Publishing
the public facade remains its owner's integration responsibility. Correctness
tests exercise the join directly using only integrated compiler/datatype/facet
implementations. Allocation evidence belongs to this task's owned measurement
driver, and must run only under the coordinator's explicit grant.
