# Common-only DTD parent sample

Run `dotnet run --project tests/Contracts/Consumers/xml/dtd/Sample.csproj -c Release` with the pinned .NET 10 SDK.
This project compiles DTD sources plus common limits, diagnostics and scalar locations independently of the resolver, decoder, core and DOM.

The first output combines two independently parsed subsets with internal-first ordinal precedence, freezes the effective model, then applies CDATA and NMTOKENS defaults using its effective general-entity table. The second output shows an explicitly granted external parameter entity, final resource URI, and saved parent suffix/final-state resumption. The fixed grant supplies authored characters; it performs no file or network open.

`--contract` inventories all twenty approved internal types (visibility, interfaces, enums, constructors, nullable metadata and defaults). The parent verifier compares this to an independently compiled approved DTD fixture and demonstrates that a real compiled nullable mutation is detected. It also compares exact sample output to independently authored expectations. These inventories are internal contract evidence, not a public ABI addition.
