# ADR-008 — Technical baseline for the first solution skeleton

- Status: Accepted
- Date: 2026-07-28
- Decision owners: Product Owner
- Task: P00-T07

## Context

P00 requires a stable target-framework policy, desktop UI framework and root namespace before creating the solution and project-reference graph. `AGENTS.md` already establishes .NET 10 and Windows-first as product constraints, but the project-level matrix and UI choice were previously unresolved.

## Decision drivers

- Keep domain and application projects portable and independent of Windows UI APIs.
- Support the Windows-first desktop MVP without allowing desktop-specific dependencies to leak into inner layers.
- Use one unambiguous namespace prefix for project names and architecture tests.

## Considered options

### Target framework matrix

- Use `net10.0` for platform-neutral projects.
- Use `net10.0-windows` only for projects that require Windows APIs or the desktop runtime.

### Desktop UI

- WPF
- WinUI 3
- Avalonia

### Root namespace

- `PewPew`
- A new product-specific prefix

## Decision

- Platform-neutral core projects use `net10.0`.
- Desktop and Windows-specific projects use `net10.0-windows`.
- The desktop UI framework is Avalonia.
- The root namespace and project prefix is `PewPew`.

The future solution skeleton must keep the Domain, SharedKernel, Application and Contracts portable. Windows-specific APIs and Avalonia dependencies belong only in outer desktop/device-adapter projects. A project may use a Windows TFM only when that boundary is explicit in the dependency rules and architecture tests.

## Consequences

### Positive

- P00-T01 can create a deterministic project graph and target-framework matrix.
- `PewPew.*` in the architecture map is now normative rather than illustrative.
- Avalonia is selected without changing the Windows-first MVP boundary.

### Negative / trade-offs

- Avalonia and Windows-targeted project packages must be evaluated and pinned by their owning implementation tasks.
- Cross-platform distribution is not implied by using Avalonia; the MVP remains Windows-first.

## Security and operational impact

- No permission, cloud, secret or action-policy behavior changes.
- Desktop-specific code remains outside Domain and Application, preserving the default-deny control path.

## Migration / rollback

- No production code or data exists yet. Before implementation begins, this ADR may be superseded by a new accepted decision if the Product Owner changes the baseline.
