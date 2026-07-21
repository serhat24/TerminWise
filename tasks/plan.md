# Phase 0 — Foundation & ADRs — Implementation Plan

> Single source of truth: `ROADMAP.md`. Working Rules: `ROADMAP.md` §"Working Rules" + `CLAUDE.md`.
> This plan covers **Phase 0 only**. No Phase 1+ work is started here.

## Confirmed decisions (2026-07-21)

| Decision | Choice | Consequence for this plan |
|---|---|---|
| GitHub repo | **Local-only for now** | I init git + build locally. No remote is pushed and nothing is published. I hand you the `gh` commands to create the public repo + branch protection when you're ready. Task 0.6 becomes "prepare, don't execute". |
| CI build scope | **Full skeleton solution** | Minimal placeholder `src/` + `tests/` projects so `dotnet build && dotnet test` is real from day one. **Kept deliberately minimal — NOT the Phase-1 Clean Architecture layers** (Domain/Application/Infrastructure, CQRS dispatchers). One placeholder library + one xUnit test project with a smoke test. |
| ADR format | **MADR** | Markdown Any Decision Records template (Status, Context, Decision Drivers, Considered Options with pros/cons, Decision Outcome, Consequences). |

## Phase 0 acceptance criteria (from ROADMAP)

- [ ] Repo builds in CI.
- [ ] Four ADRs (001–004) merged, status Accepted.
- [ ] README explains vision and architecture at a glance.

## Guiding rules honored

1. **Phase discipline** — nothing from Phase 1+ is built. The skeleton solution is a build/test placeholder, not the architecture skeleton (that is Phase 1).
2. **ADR-first** — in Phase 0 the ADRs *are* the deliverable; each is drafted, reviewed by you, marked Accepted, then merged. No implementing code (that comes in Phases 1–3).
3. **`main` deployable** — every task lands via a feature branch → PR → green CI → merge (once the remote exists; until then, local branch discipline mirrors this).
4. **Boring & documented** over clever.
5. **No training-data trust** for volatile facts — each ADR starts by verifying current licensing/feature facts from official docs.

## Dependency graph

```
git init
  └─ scaffold dirs (src/ tests/ frontend/ docs/adr docs/diagrams .github/workflows)
       ├─ ADR MADR template + index ─┐
       ├─ README skeleton            │
       ├─ skeleton .sln + placeholder projects
       └─ CI skeleton (build+test+docs on PR)
            └─ (0.6 prepared, deferred) GitHub repo + branch protection
                                                   │
   ── CHECKPOINT A: bootstrap review, CI green ────┤
                                                   │
   sequential, each review-gated (one at a time):  │
   ADR-001 ▸ ADR-002 ▸ ADR-003 ▸ ADR-004 ──────────┘
        └─ README "architecture at a glance" finalize
             └─ Phase 0 acceptance check ▸ your sign-off ▸ bump CLAUDE.md phase
   ── CHECKPOINT B: Phase 0 → Phase 1 gate ──
```

## Vertical slices

Each ADR is one complete vertical path: **verify facts → draft → your review → revise → Accept → merge**. The bootstrap is the walking skeleton that makes a green CI + mergeable PR possible.

---

### Slice 0 — Repo bootstrap (walking skeleton)

**0.1 — git + base config**
- `git init` (branch `main`); `LICENSE` (MIT); `.gitignore` (VS/.NET + Node/Angular combined); `.editorconfig` (C# + TS conventions); `.gitattributes` (enforce LF for source, protect against Windows CRLF surprises).
- *Acceptance:* files committed, working tree clean.
- *Verify:* `git log --oneline`; open each file.

**0.2 — directory scaffold**
- Create `src/ tests/ frontend/ docs/adr/ docs/diagrams/ .github/workflows/` and `docker-compose.yml` placeholder. `.gitkeep` in dirs that would otherwise be empty.
- *Acceptance:* layout matches ROADMAP §Phase 0 scope.
- *Verify:* `find . -type d` tree review.

**0.3 — ADR MADR template + index**
- `docs/adr/0000-madr-template.md`; `docs/adr/README.md` index listing ADR-001..004 as *Proposed* with one-line scope each.
- *Acceptance:* template + index committed.
- *Verify:* open files; index links resolve.

**0.4 — README skeleton**
- Vision paragraph (from ROADMAP intro), tech-stack table, phase changelog stub, ADR index link, "Architecture at a glance" placeholder section (finalized in Slice 5).
- *Acceptance:* renders; all sections present.
- *Verify:* markdown preview.

**0.5 — skeleton solution + CI**
- Verify current **.NET LTS** version (`dotnet --list-sdks`; cross-check official release notes — do not assume from memory).
- Create minimal `*.sln` + one placeholder class-library in `src/` + one xUnit project in `tests/` with a single passing smoke test. **This is a CI placeholder, not the Phase-1 architecture.**
- `.github/workflows/ci.yml`: triggers on PR to `main`; jobs = `build` (`dotnet build`), `test` (`dotnet test`), `docs` (markdown lint/link check). **Verify current `actions/checkout`, `actions/setup-dotnet`, `actions/setup-node` pinned versions from docs.**
- *Acceptance:* `dotnet build` + `dotnet test` green locally; workflow YAML valid.
- *Verify:* run `dotnet build && dotnet test`; lint the workflow.

**0.6 — GitHub repo + branch protection (DEFERRED — prepared for you)**
- Produce ready-to-run commands: `gh repo create <name> --public --source=. --push`, then branch-protection API call (require PR + green CI on `main`).
- *Acceptance:* commands documented in `tasks/plan.md` / handed to you; **not executed** without your go-ahead.
- *Verify:* you run them, or authorize me to.

> ### ⛳ CHECKPOINT A
> You review the repo structure and confirm CI is green before any ADR drafting begins.

---

### Slices 1–4 — ADRs (one at a time, each review-gated)

Common path per ADR (**no implementation code**):
1. **Verify** current facts from official docs (licensing/feature availability).
2. **Draft** on branch `adr/00X-...`: Status=Proposed → Context → Decision Drivers → Considered Options (pros/cons each) → Decision Outcome → Consequences.
3. **Present** draft to you.
4. **Revise** per feedback.
5. **Accept** (Status=Accepted) + merge PR; update ADR index.

**Slice 1 — ADR-001: Tenancy model on PostgreSQL**
- Options: database-per-tenant · schema-per-tenant · shared schema + tenant discriminator · shared schema + **Row Level Security**.
- Verify: current Postgres RLS behavior; EF Core + Npgsql global query filters / RLS integration.
- *Acceptance:* ADR-001 merged, Accepted, indexed.
- **Gate: your approval before ADR-002.**

**Slice 2 — ADR-002: Keycloak tenancy**
- Options: realm-per-tenant vs single realm + **Organizations**/groups.
- Verify: current Keycloak **Organizations** feature status + version (recently added — do not trust memory).
- *Acceptance:* merged, Accepted, indexed. **Gate before ADR-003.**

**Slice 3 — ADR-003: Message broker & client library**
- Options: RabbitMQ vs Kafka (classic messaging vs event streaming); client: thin custom over `RabbitMQ.Client` (preferred) vs MassTransit v9+ vs Rebus.
- Verify: **current MassTransit licensing** (v8 OSS patch window, v9 commercial), Rebus license, `RabbitMQ.Client` status.
- *Acceptance:* merged, Accepted, indexed. **Gate before ADR-004.**

**Slice 4 — ADR-004: Custom CQRS + modular-monolith-first**
- Rationale: why no MediatR (dependency-light + demonstrate pattern from first principles); modular monolith with module boundaries = future service boundaries, extract in Phase 6.
- Verify: **current MediatR licensing** (v13+ dual RPL-1.5/commercial + Community tier).
- *Acceptance:* merged, Accepted, indexed. **Gate before Slice 5.**

---

### Slice 5 — Phase 0 close-out

**5.1 — Finalize README "architecture at a glance"** referencing the four Accepted ADRs; add a simple context diagram placeholder in `docs/diagrams/`.
**5.2 — Acceptance verification:** CI green ✔ · four ADRs merged ✔ · README vision + architecture ✔.
**5.3 — On your confirmation only:** bump `CLAUDE.md` "Current phase" to 1.

> ### ⛳ CHECKPOINT B — Phase 0 → Phase 1 gate
> Do not start Phase 1 (Clean Architecture skeleton, custom CQRS, Booking module, ADR-009/010/011) until you sign off that Phase 0 acceptance criteria pass.

## Open items / assumptions

- Repo name for the eventual GitHub repo: **TBD** (needed for 0.6 commands and README).
- Exact .NET LTS version pinned during 0.5 after verification.
- Local branch discipline mirrors PR flow until the remote exists (0.6 deferred).
