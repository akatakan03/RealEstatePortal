# Backlog & Future Enhancements

Deferred work, consciously postponed during development. Each item notes *why* it was deferred and a rough approach, so it can be picked up later without re-deriving the context. Nothing here is a bug — the current build is functional; these are enhancements, refactors, and scope we chose to defer.

> See `ARCHITECTURE.md` for the design and the ADRs that several of these items reference.

---

## Remaining roadmap (planned, not yet built)

These were always intended and are the natural next features.

- **Browse-page map** — Leaflet map on the public listing search showing a pin per result, plus a "search this area" control that feeds the map centre + radius into the existing `centerLat`/`centerLng`/`radiusKm` query parameters (the spatial backend for this already exists — see ADR-007). *In progress next.*
- **Admin moderation** — an admin view of all listings (approve / archive / remove), giving the currently-unused `GetListings` query its home. Admin role already seeded.
- **SEO polish** — slug-based canonical URLs (`/listing/{id}/{slug}`), `schema.org` `RealEstateListing` structured data, sitemap, and per-listing meta tags. Slugs are already generated and stored; this wires them into routing and markup.
- **Bilingual TR/EN localization** — resource-file (`.resx`) based UI translation. **Note:** revisit the app-wide `en-US` culture pin when doing this — the mature approach keeps numbers/dates on invariant/`en-US` formatting internally while translating *text* via resources, so the decimal-separator ambiguity doesn't return. Record this in `ARCHITECTURE.md` at that time.

## Optional scope (designed for, not committed)

Modelled in the architecture but left as opt-in.

- **Agency grouping** — agents optionally belong to an `Agency` (name, logo, address). Table is designed in the data model; not built.
- **Favorites & saved searches** — ✅ **DONE.** Added a **Member** role and role-choice registration (Member vs Agent). Favorites: toggle from the detail page (form-POST) or browse-grid hearts (AJAX), with a "Saved" page. Saved searches: members name a filter set on the browse page; a second `ListingPublishedEvent` handler (`NotifySavedSearchesHandler`) emails matches via the shared `SavedSearchMatcher` when an agent publishes a matching listing (ADR-010). Manageable from an "Alerts" page.

---

## Features (future enhancements)

- **Interactive map pin on create/edit** — ✅ **DONE.** Agent confirms/corrects location on a Leaflet map: "Locate on map" geocodes the typed address to a draggable marker (first guess), then the agent drags it or clicks the map to set the true spot. The marker's final position is saved as `Latitude`/`Longitude`; server-side geocoding remains the fallback when the agent never touches the map. Shared `location-picker.js` powers both create and edit; AJAX endpoint at `/Listings/Geocode?q=...`.
  - **Still optional / not built:** *reverse* geocoding (pin → address), i.e. updating the address text box when the agent drags the marker, via a `ReverseAsync` on `IGeocodingService`. Minor nicety; the forward flow is complete without it.
  - Retires the geocoder-accuracy limitation — agents now have final say on location.

- **Commercial geocoder option** — Nominatim + fallback geocodes reliably but only to district/neighbourhood centre for sparsely-mapped Turkish streets (OSM coverage gap, not a bug). For building-level accuracy, a commercial provider (Google, HERE, or a Turkey-specialised service) behind the existing `IGeocodingService` interface would be a drop-in swap. Deferred because it needs an API key + billing, which this project deliberately avoids. **Largely mooted by the interactive map pin (now done)** — agents place the exact spot manually, so the geocoder no longer needs building-level precision.

---

## Architecture & refactors (tech debt)

- **Domain-event dispatcher** — ✅ **DONE.** `DispatchDomainEventsInterceptor` (Infrastructure) collects domain events off tracked entities *after* commit and publishes them through MediatR via a `DomainEventNotification<T>` wrapper (keeps Domain free of MediatR). First consumer: `ListingPublishedEventHandler` emails the owner when their listing goes live. Unit + integration tested. *Future: offload handlers to a background queue (see Performance).*
- **Shared ownership-check helper** — ✅ **DONE.** Extracted the "load entity → verify `OwnerId == currentUser` → act" pattern into `IApplicationDbContext.GetOwnedListingAsync(...)` (with an `includeMedia` flag for handlers needing photos). Applied across update/delete/publish listing and the three photo commands; admin handlers deliberately excluded (they don't check ownership). Behaviour preserved — verified by the existing ownership tests staying green.
- **Reusable exception-handling filter** — ✅ **DONE.** Added `DomainExceptionFilter` (registered globally) mapping `NotFoundException → 404` and `ForbiddenAccessException → 403`. Controllers no longer repeat those catches. `ValidationException` intentionally stays in the form actions (each re-fetches its own view model to redisplay field errors) — the genuinely action-specific case.
- **Soft delete for listings** — ✅ **DONE.** Deleting stamps `Listing.DeletedAt`/`DeletedBy` instead of removing the row, and a global query filter (`ListingConfiguration`) hides deleted listings from every read path — nothing had to remember to exclude them. The inquiries buyers sent, who saved it, its price history and its R2 objects all survive, so a restore brings the listing back whole (as a **draft**, so it can't silently reappear on the public site, and with any admin lock still in place). `DeletedListingPurgeWorker` sweeps every 6h and hard-deletes anything past `ListingDeletion.RetentionDays` (30), photos first then row, so a failed storage call can't orphan objects. Admins get a Trash tab with Restore and an "Erase now" escape hatch for takedowns/erasure requests. Two places opt out of the filter deliberately: the trash/purge queries, and the slug-uniqueness check in `CreateListingCommandHandler` — deleted rows keep their slug and the unique index still enforces it.
  - **Not built:** agent self-service restore. Only an administrator can bring a listing back; agents are told so on the confirm screen. The dashboard table is driven by the same row pipeline that feeds the KPIs, so a "Deleted" tab there needs its own load path rather than a filter flag.
  - **Also worth knowing:** inquiries survive the grace period, not forever — purging a listing takes its inquiry history with it. If an agent's lead history should outlive the listing, the fix is to denormalise the listing title onto `Inquiry` and break the FK, which is a separate change.
- **Admin-archive is not agent-proof** — an admin can archive a listing, but the owning agent can currently re-publish it from their dashboard, because `Listing.Publish()` doesn't block the `Archived → Active` transition. A stricter moderation model would prevent agents re-activating admin-archived listings (e.g. an `AdminLocked` flag, or distinguishing agent-archived from admin-archived). Left out to keep the moderation pass focused.

## REST API (shipped — future enhancements)

The public REST API is live: read endpoints (`GET /api/listings`, `/api/listings/{id}`) plus JWT-secured write endpoints (`POST/PUT/DELETE /api/listings`, publish), documented with Swagger UI (ADR-008, ADR-009). Natural follow-ups:

- **API integration tests** — ✅ **DONE.** `WebApplicationFactory<Program>`-based tests boot the real host against a dedicated LocalDB (`RealEstatePortalDb_ApiTest`) with external services faked, and exercise the endpoints over real HTTP: public read returns 200 without a token, bad login → 401, write without token → 401, agent create → 201, invalid body → 400, and cross-agent update → 403 (full JWT + ownership stack over the wire). Surfaced and fixed a real gap — the `DomainExceptionFilter` is now API-aware (clean 403/400 for `/api`, cookie redirect for MVC).
- **Refresh tokens / revocation** — JWTs are stateless with a 60-min expiry and no server-side revocation. A refresh-token flow (or short access + rotating refresh) would harden the auth story for real use.
- **API versioning** — routes are unversioned (`/api/listings`). `Asp.Versioning.Mvc` + `/api/v1/...` would future-proof the contract.
- **Rate limiting** — the built-in .NET rate limiter on `/api/*` (especially `/api/auth/login`) to blunt brute-force and abuse.
- **Scalar UI** — optionally add `Scalar.AspNetCore` (`MapScalarApiReference()`) as a modern docs UI alongside/instead of Swagger UI — reflects the 2026 .NET tooling direction.

## Performance

- **Cache the unread-inquiry count** — the nav badge runs a `COUNT` query on every page load for logged-in agents. Fine at current scale; if it ever matters, cache it briefly (short in-memory TTL) or refresh on inquiry create / mark-read.
- **`IQueryable` projection for read queries** — some read paths materialise entities then map/fill in memory (e.g. cover-image URLs, detail images). At scale, projecting straight to DTO columns in SQL is cheaper. Kept explicit for readability while data volumes are tiny.
- **Async / background domain-event handlers** — the domain-event dispatcher (`DispatchDomainEventsInterceptor`) publishes events synchronously *after* commit, so the triggering request waits for handlers (e.g. the listing-published email) to finish. Fine locally; a production system would offload event handlers to a background queue (e.g. hosted service + channel, or an outbox pattern) so the user's request returns immediately and side effects retry on failure.

## Quality

- **Automated tests** — ✅ the test trio is established and green (40+ tests) with the full command surface covered. `Domain.UnitTests` (value-object + entity invariants); `Application.UnitTests` (validators + handlers via NSubstitute/MockQueryable — listings, inquiries, admin moderation, photo-upload orchestration, domain-event handler; plus `NominatimGeocodingService` fallback via a stub `HttpMessageHandler`); `IntegrationTests` (real pipeline + LocalDB via Respawn — create/publish, spatial radius query, domain-event dispatch, inquiry leads loop). Guarantees under test include ownership enforcement, geocode-on-save, save-first-email-best-effort, R2-orphan cleanup, cover-photo logic, and the empty-then-fallback geocoding chain. Stack: xUnit + NSubstitute + MockQueryable + Shouldly + Respawn. *Future: broaden as new features land.*

- **Turkish-locale case-insensitive text matching** — `SavedSearchMatcher` keyword matching uses `StringComparison.OrdinalIgnoreCase`, which does not handle the Turkish dotted/dotless İ (`"KADIKÖY"` won't match `"kadıköy"`). Fine for typical input; a locale-aware improvement would use `InvariantCultureIgnoreCase` or normalize both sides. Deferred as a considered decision rather than a silent default — Turkish casing is subtle enough to warrant its own pass. The same consideration applies anywhere user text is compared case-insensitively.

---

*Last updated: 2026-07-10 (admin moderation, SEO, map pin, domain events, refactors, REST API, favorites & saved searches, user profiles, public agent profiles, and SignalR real-time notifications shipped). **Test-coverage note:** the test suite covers the command surface up to the REST API; features shipped since (saved-search matching, favorites, profiles, agent-profile query, real-time push) are **not yet covered** — a consolidation testing pass is the recommended next step.*
