# Contracts

The only way modules talk to each other. Put one folder per module here (`Catalog/`, `Pricing/`, ...)
with the interface the module exposes and the plain records it accepts and returns.

Rules:
- No logic, no EF types, no references to module projects.
- A module implements its own contract internally and registers it in DI in its `Add...Module` method.
