# Security and threat model (pilot)

Scope: a local-first transcription tool that can optionally be exposed to a small trusted group.
It is not hardened for the public internet.

## Assets

- Project documents and revisions (scholarly evidence), annotations, review decisions.
- API keys (user and bootstrap) and the auth audit.
- Images/crops/checkpoints and model packages (weights + metadata).
- Server filesystem paths referenced by imports/exports (PAGE, EpiDoc, images, packages).

## Trust boundaries

1. **Local mode** (no accounts, no bootstrap key): the API is open and must bind localhost only.
2. **Shared mode**: every `/api/v1` request authenticates with a hashed account key (or the
   bootstrap admin key); authorization is role-based. SSE may pass `?api_key=` because
   EventSource cannot set headers — URLs can be logged, so use TLS and protect access logs.
3. **Worker** trusts jobs in the same SQLite store. A job may name a model package directory; the
   worker verifies the package (architecture, label/weights/data digests) before use.
4. **Browser**: the workspace stores the API key and read caches in localStorage. Caches are
   cleared when the key changes; shared machines should clear site data after use.

## Controls implemented

- SHA-256 key hashes; plaintext keys are shown once; rotation and disable take effect immediately.
- Roles: reader (GET), annotator (mutations), reviewer (+ review decisions), admin (+ users,
  audit, project deletion). `Auth:ReadOnly` overrides all roles for mutations.
- Auth audit records mutations/denials with user/role/path/status; no key material.
- Encoded-image size/dimension limits; remote image fetch bounded (64 MB) into a checksummed cache.
- Import paths must be absolute and readable on the server host; model selection via API is by
  package name under a configured directory (no path traversal).
- Model packages are data-only; the loader reads named artifacts and rejects architecture/digest
  mismatches. No script execution.
- CSV imports go through the same revision/validation service; malformed ids/rows are rejected
  or reported, not executed.
- SQLite store refuses newer schemas; backups use `VACUUM INTO`; restore verifies database and
  image checksums before writing.

## Known gaps

- No SSO/OIDC, refresh tokens, session expiry policy, CSRF/CORS hardening or per-object
  membership; the workspace key is a long-lived bearer token.
- No rate limiting, brute-force lockout or key-expiry policy.
- No structured per-request access log beyond the auth audit; no SIEM integration.
- Local mode must not be exposed; there is no automatic refusal to bind non-loopback addresses.
- Model/weights licensing and distribution rights are not enforced technically.
- Browser caches contain project text; clearing requires manual or key-change action.
