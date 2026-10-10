# Common-only DTD parent sample

Run `dotnet run --project samples/M2-dtd/Sample.csproj -c Release` with the pinned .NET 10 SDK.
This project compiles actual verified DTD sources plus common limits, diagnostics and scalar locations. It has no resolver, decoder, core, DOM or later milestone implementation dependency.

The first output combines two independently parsed subsets with internal-first ordinal precedence, freezes the effective model, then applies CDATA and NMTOKENS defaults using its effective general-entity table. The helper is the verified grammar join demonstration copied into this task's ownership. The second output shows an explicitly granted external parameter entity, final resource URI, and saved parent suffix/final-state resumption. The fixed grant supplies authored characters; it performs no file or network open. Production DOCTYPE/general-entity composition remains M2-entities work.

`--contract` inventories all twenty approved internal types (visibility, interfaces, enums, constructors, nullable metadata and defaults). The parent verifier compares this to an independently compiled approved DTD fixture and demonstrates that a real compiled nullable mutation is detected. It also compares exact sample output to independently authored expectations. These inventories are internal contract evidence, not a public ABI addition.
