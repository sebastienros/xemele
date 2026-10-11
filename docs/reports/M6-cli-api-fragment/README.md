# Zero-member CLI inventory repair

CLI is internal and adds no library members. Official inventory rejects the header-only fragment. Removing that file preserves every declaration; no runtime or discovery tool changes. Official comparison is required before verification.

At `472a1800a160143ba2f96e9c7efad6da90463f07`, two fresh Release builds passed with zero warnings/errors using SDK 10.0.400 and explicit extractor runtime 10.0.11. Official inventory and compiled comparison matched 1054 members with bootstrap exemption disabled, original offline fixtures verified, planning164 valid. Independent source-only review approved deletion; no declarations/source/tool changes.
