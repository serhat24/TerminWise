# Phase 0 — Todo

Legend: `[ ]` todo · `[~]` in progress · `[x]` done · `⛳` checkpoint (stop for sign-off)

## Slice 0 — Repo bootstrap
- [ ] 0.1 git init (main) + LICENSE (MIT) + .gitignore + .editorconfig + .gitattributes
- [ ] 0.2 scaffold dirs: src/ tests/ frontend/ docs/adr/ docs/diagrams/ .github/workflows/ + docker-compose.yml placeholder
- [ ] 0.3 ADR MADR template (docs/adr/0000-madr-template.md) + ADR index (docs/adr/README.md, 001–004 Proposed)
- [ ] 0.4 README skeleton (vision, tech-stack table, changelog stub, ADR link, architecture placeholder)
- [ ] 0.5 verify .NET LTS → minimal .sln + placeholder src lib + xUnit smoke test → CI workflow (build+test+docs, verified action versions) → green locally
- [ ] 0.6 (DEFERRED) prepare gh repo-create + branch-protection commands; hand to user, do NOT execute

- [ ] ⛳ CHECKPOINT A — user reviews structure + confirms CI green before ADR drafting

## Slices 1–4 — ADRs (sequential, gated)
- [ ] ADR-001 Postgres tenancy — verify (RLS/EF Core+Npgsql) → draft → review → Accept → merge
- [ ] ⛳ approval gate before ADR-002
- [ ] ADR-002 Keycloak tenancy — verify (Organizations feature/version) → draft → review → Accept → merge
- [ ] ⛳ approval gate before ADR-003
- [ ] ADR-003 broker & client — verify (MassTransit/Rebus licensing) → draft → review → Accept → merge
- [ ] ⛳ approval gate before ADR-004
- [ ] ADR-004 custom CQRS + modular-monolith-first — verify (MediatR licensing) → draft → review → Accept → merge
- [ ] ⛳ approval gate before Slice 5

## Slice 5 — Close-out
- [ ] 5.1 finalize README "architecture at a glance" + context diagram placeholder
- [ ] 5.2 acceptance check: CI green · 4 ADRs merged · README vision+architecture
- [ ] 5.3 (on user confirm only) bump CLAUDE.md Current phase → 1

- [ ] ⛳ CHECKPOINT B — Phase 0 → Phase 1 gate (do not start Phase 1 until sign-off)

## Blockers / needs from you
- [x] Repo name for GitHub → **TerminWise**
- [x] Go-ahead to begin Slice 0 execution → approved 2026-07-21
