# Phase 0 — Todo

Legend: `[ ]` todo · `[~]` in progress · `[x]` done · `⛳` checkpoint (stop for sign-off)

## Slice 0 — Repo bootstrap

- [x] 0.1 git init (main) + LICENSE (MIT) + .gitignore + .editorconfig + .gitattributes
- [x] 0.2 scaffold dirs: src/ tests/ frontend/ docs/adr/ docs/diagrams/ .github/workflows/ + docker-compose.yml placeholder
- [x] 0.3 ADR MADR template (docs/adr/0000-madr-template.md) + ADR index (docs/adr/README.md, 001–004 Proposed)
- [x] 0.4 README skeleton (vision, tech-stack table, changelog stub, ADR link, architecture placeholder)
- [x] 0.5 .NET 10 LTS → TerminWise.slnx + placeholder src lib + xUnit smoke test → CI (build+test+format, verified action versions) → green locally (build ✔, 2 tests ✔, format ✔)

- [~] 0.6 (DEFERRED — prepared, NOT executed) gh repo-create + branch-protection commands below

### 0.6 — Commands for you to run when ready to publish (local-only until then)

```bash
# From D:\Projects — create the PUBLIC repo, push main, set description + topics
gh repo create TerminWise --public --source=. --remote=origin --push \
  --description "Multi-tenant, event-driven SaaS reference architecture — .NET modular monolith evolving to microservices. Clean Architecture, custom CQRS, Keycloak, RabbitMQ + Outbox, Angular 22 Signal Forms, AI booking assistant."

gh repo edit --add-topic dotnet,angular,clean-architecture,multi-tenant,cqrs,ddd,modular-monolith,microservices,keycloak,rabbitmq,event-driven,saas,reference-architecture,postgresql

# Branch protection on main: require PR + green CI (both CI jobs)
gh api -X PUT repos/{owner}/TerminWise/branches/main/protection \
  -H "Accept: application/vnd.github+json" \
  -F "required_status_checks[strict]=true" \
  -F "required_status_checks[checks][][context]=Build & test (backend)" \
  -F "required_status_checks[checks][][context]=Format (editorconfig)" \
  -F "enforce_admins=true" \
  -F "required_pull_request_reviews[required_approving_review_count]=0" \
  -F "restrictions=" 2>/dev/null || echo "adjust owner; some protections need the repo to exist first"
```

- [ ] ⛳ CHECKPOINT A — user reviews structure + confirms CI green before ADR drafting

## Slices 1–4 — ADRs (sequential, gated)

- [x] ADR-001 Postgres tenancy — verified (RLS/EF Core 10/Npgsql, 2026-07-21) → drafted → reviewed → **Accepted** → merged
      Decision: shared schema + tenant discriminator (EF global query filter = ergonomics) + PostgreSQL RLS (= the guarantee). Non-owner app role, FORCE RLS, SET LOCAL per txn. Phase 2: schema-inspection test fails on unclassified tables; migration role bypasses RLS by design.
- [x] ⛳ approval gate before ADR-002 — passed
- [x] ADR-002 Keycloak tenancy — verified (Keycloak 26.x Organizations GA since 26.0, 2026-07-21) → drafted → reviewed → **Accepted** → merged
      Decision: single realm + Organizations (tenant = org). `organization` claim → app.current_tenant (spine with ADR-001). Multi-org users: fail-closed, single-org token only. Machine clients deferred to ADR-008.
- [x] ⛳ approval gate before ADR-003 — passed

- [x] ADR-003 broker & client — verified (MassTransit v9 commercial/v8 EOL 2026; Rebus MIT 8.9.2; RabbitMQ.Client 7.2.1) → drafted → reviewed → **Accepted** → merged
      Decision: RabbitMQ + thin custom client over RabbitMQ.Client v7. Rebus = OSS off-ramp. Explicit in/out scope. Envelope carries tenant_id (fail-closed) + W3C traceparent.
- [x] ⛳ approval gate before ADR-004 — passed
- [x] ADR-004 custom CQRS + modular-monolith-first — verified (MediatR v13+ dual RPL-1.5/commercial, Community <$5M/$10M, 2026-07-21) → drafted → reviewed → **Accepted** → merged
      Decision: custom CQRS dispatcher (no MediatR) + modular-monolith-first (extract Phase 6). martinothamar/Mediator = MIT off-ramp. Cross-module: async-by-default, sync only via public contract, no cross-module DB/tx, eventual consistency via outbox. AI service = designed-separate exception (ADR-013).
- [x] ⛳ approval gate before Slice 5 — passed (all four ADRs Accepted + merged)

## Slice 5 — Close-out

- [ ] 5.1 finalize README "architecture at a glance" + context diagram placeholder
- [ ] 5.2 acceptance check: CI green · 4 ADRs merged · README vision+architecture
- [ ] 5.3 (on user confirm only) bump CLAUDE.md Current phase → 1
- [ ] ⛳ CHECKPOINT B — Phase 0 → Phase 1 gate (do not start Phase 1 until sign-off)

## Blockers / needs from you

- [x] Repo name for GitHub → **TerminWise**
- [x] Go-ahead to begin Slice 0 execution → approved 2026-07-21
