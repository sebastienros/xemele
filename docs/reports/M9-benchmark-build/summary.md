# Aggregate benchmark build repair

The parent benchmark project now builds and lists 220 actual benchmark methods, including the new DOM storage and XPath ordering/overlap cases. Standalone entry points are excluded from the parent's source glob; the parent's own Program.cs is included once. All nested bin/obj outputs are excluded to prevent generated source collisions after child builds.

All four independently owned Schematron entry projects, the RNG probe, and the resolver sample retain their original source/project bytes and build without warnings/errors. The parent rebuilds after those generated outputs appear and lists identical methods. The original CS8802 and the first repair's CS7022 rejection are retained as failed evidence. No benchmark measurement, runtime source edit, API or release-threshold change was performed.
