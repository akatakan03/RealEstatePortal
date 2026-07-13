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
- **Favorites & saved searches** — requires visitor accounts. Enables buyers to save listings and re-run filter sets.

---

## Features (future enhancements)

- **Interactive map pin on create/edit** — ✅ **DONE.** Agent confirms/corrects location on a Leaflet map: "Locate on map" geocodes the typed address to a draggable marker (first guess), then the agent drags it or clicks the map to set the true spot. The marker's final position is saved as `Latitude`/`Longitude`; server-side geocoding remains the fallback when the agent never touches the map. Shared `location-picker.js` powers both create and edit; AJAX endpoint at `/Listings/Geocode?q=...`.
  - **Still optional / not built:** *reverse* geocoding (pin → address), i.e. updating the address text box when the agent drags the marker, via a `ReverseAsync` on `IGeocodingService`. Minor nicety; the forward flow is complete without it.
  - Retires the geocoder-accuracy limitation — agents now have final say on location.

- **Commercial geocoder option** — Nominatim + fallback geocodes reliably but only to district/neighbourhood centre for sparsely-mapped Turkish streets (OSM coverage gap, not a bug). For building-level accuracy, a commercial provider (Google, HERE, or a Turkey-specialised service) behind the existing `IGeocodingService` interface would be a drop-in swap. Deferred because it needs an API key + billing, which this project deliberately avoids. **Largely mooted by the interactive map pin (now done)** — agents place the exact spot manually, so the geocoder no longer needs building-level precision.

---

## Architecture & refactors (tech debt)

- **Domain-event dispatcher** — `BaseEntity` already collects domain events (e.g. `ListingPublishedEvent` raised in `Listing.Publish()`), but nothing dispatches them yet. Add a `SaveChanges` interceptor that publishes collected events through MediatR after save, with `ListingPublishedEvent` as the first real handler. Deferred deliberately: a dispatcher with no listeners is speculative. Worth building as its own focused pass when a second side effect appears.
- **Shared ownership-check helper** — the "load entity → verify `OwnerId == currentUser` → act" pattern is now duplicated across several command handlers (update/delete/publish listing, photo commands, inquiry status commands). Extract into a small shared helper or a MediatR authorization behaviour. Left inline so far because premature abstraction is worse than visible duplication; it has now recurred enough to justify the refactor.
- **Reusable exception-handling filter** — controllers repeat `try/catch (ValidationException | NotFoundException | ForbiddenAccessException)` blocks that map to `ModelState` / `NotFound()` / `Forbid()`. A reusable MVC action filter (or exception filter) would DRY this up. One or two endpoints didn't justify it; it now spans many.
- **Soft delete for listings** — current listing delete is a hard delete (row + R2 objects removed). A production system typically prefers soft delete (`IsDeleted` flag + global query filter) so listings can be recovered and history preserved. Clean to add via an EF query filter; would also change the R2-cleanup timing.
- **Admin-archive is not agent-proof** — an admin can archive a listing, but the owning agent can currently re-publish it from their dashboard, because `Listing.Publish()` doesn't block the `Archived → Active` transition. A stricter moderation model would prevent agents re-activating admin-archived listings (e.g. an `AdminLocked` flag, or distinguishing agent-archived from admin-archived). Left out to keep the moderation pass focused.

## Performance

- **Cache the unread-inquiry count** — the nav badge runs a `COUNT` query on every page load for logged-in agents. Fine at current scale; if it ever matters, cache it briefly (short in-memory TTL) or refresh on inquiry create / mark-read.
- **`IQueryable` projection for read queries** — some read paths materialise entities then map/fill in memory (e.g. cover-image URLs, detail images). At scale, projecting straight to DTO columns in SQL is cheaper. Kept explicit for readability while data volumes are tiny.
- **Async / background domain-event handlers** — the domain-event dispatcher (`DispatchDomainEventsInterceptor`) publishes events synchronously *after* commit, so the triggering request waits for handlers (e.g. the listing-published email) to finish. Fine locally; a production system would offload event handlers to a background queue (e.g. hosted service + channel, or an outbox pattern) so the user's request returns immediately and side effects retry on failure.

## Quality

- **Automated tests** — ✅ the test trio is established and green (40+ tests) with the full command surface covered. `Domain.UnitTests` (value-object + entity invariants); `Application.UnitTests` (validators + handlers via NSubstitute/MockQueryable — listings, inquiries, admin moderation, photo-upload orchestration, domain-event handler; plus `NominatimGeocodingService` fallback via a stub `HttpMessageHandler`); `IntegrationTests` (real pipeline + LocalDB via Respawn — create/publish, spatial radius query, domain-event dispatch, inquiry leads loop). Guarantees under test include ownership enforcement, geocode-on-save, save-first-email-best-effort, R2-orphan cleanup, cover-photo logic, and the empty-then-fallback geocoding chain. Stack: xUnit + NSubstitute + MockQueryable + Shouldly + Respawn. *Future: broaden as new features land.*

---

*Last updated: 2026-07-10 (admin moderation, SEO, interactive map pin, domain events shipped; test suite covers full command surface)*
