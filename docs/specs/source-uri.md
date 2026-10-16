# Additive source identity entry points

The exact new signatures are `XmlDocument.Load(Stream input, XmlParseOptions? options, Uri sourceUri, CancellationToken cancellationToken)` and `DtdSchema.Compile(string dtd, XmlResourceResolver? resolver, XmlResourceLimits? limits, Uri sourceUri, CancellationToken cancellationToken)`. Every parameter is required. All older signatures, optional defaults and null/default literal calls remain unchanged.

The source URI must be nonnull and absolute; validation precedes input reads or resolver work. It identifies the caller-provided primary source and grants no resource access. XML delegates to the verified byte DOM adapter; DTD compilation seeds the existing external-subset parser location. Returned resources retain their final URI for nested relative declarations and diagnostics. Existing encoding, model retention, quotas, cancellation, borrowed primary stream and owned resolver-resource cleanup rules remain unchanged.
