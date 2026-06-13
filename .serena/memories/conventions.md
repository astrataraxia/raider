# Conventions

- Prefer simple and elegant code with few concepts, clear names, direct data flow, and no unproven layers or packages.
- Follow Red, Green, Refactor, Verify for implementation tasks.
- Keep platform DTOs at the collection boundary and expose only the shared live broadcast model.
- Screen requests read the current immutable memory snapshot and never call external platforms directly.
- New source files start with a one-line Korean role comment below required directives or shebangs.
- Follow `ENGINEERING.md` for naming, errors, concurrency, configuration, and security.
