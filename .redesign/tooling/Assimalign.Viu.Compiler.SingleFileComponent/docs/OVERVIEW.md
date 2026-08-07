# Assimalign.Viu.Compiler.SingleFileComponent

The public `SingleFileComponentCompiler` facade is shared by generators and editor services. Its
request and result expose source text, deterministic identities, generated source, neutral
diagnostics, and source mappings without exposing Roslyn types or the mutable compiler model.

The scaffold's internal projection pipeline is illustrative. The production parser, analyzers,
catalogs, and emitter remain internal behind the same public operation.
