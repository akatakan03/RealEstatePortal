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

- **Interactive map pin on create/edit** *(high value)* — let the agent confirm/correct the location instead of trusting the geocoder. The address field and a Leaflet map work together: typing an address geocodes to a **draggable marker** (a first guess); the agent drags it or clicks the map to set the true spot. The marker's final position — not the geocoder's guess — is saved as `Latitude`/`Longitude`.
  - Flips coordinates from a server-side side effect into explicit user input: `CreateListingCommand`/`UpdateListingCommand` gain nullable `Latitude`/`Longitude` submitted from the map.
  - Server-side geocoding (Nominatim + fallback) becomes the *backstop* when the agent never touches the map — so existing work is reused, not discarded.
  - Needs a thin AJAX geocode endpoint (`/Listings/Geocode?q=...`) and, optionally, reverse geocoding (pin → address) via a new `ReverseAsync` on the geocoding service.
  - Trade-off: JS-only; keep server-side geocode as the no-JS fallback.
  - This is the standard pattern on mature portals (Sahibinden, Zillow, Rightmove) and sidesteps geocoder accuracy limits entirely.

- **Commercial geocoder option** — Nominatim + fallback geocodes reliably but only to district/neighbourhood centre for sparsely-mapped Turkish streets (OSM coverage gap, not a bug). For building-level accuracy, a commercial provider (Google, HERE, or a Turkey-specialised service) behind the existing `IGeocodingService` interface would be a drop-in swap. Deferred because it needs an API key + billing, which this project deliberately avoids. The interactive map pin above is the preferred, cost-free path to accuracy.

---

## Architecture & refactors (tech debt)

- **Domain-event dispatcher** — `BaseEntity` already collects domain events (e.g. `ListingPublishedEvent` raised in `Listing.Publish()`), but nothing dispatches them yet. Add a `SaveChanges` interceptor that publishes collected events through MediatR after save, with `ListingPublishedEvent` as the first real handler. Deferred deliberately: a dispatcher with no listeners is speculative. Worth building as its own focused pass when a second side effect appears.
- **Shared ownership-check helper** — the "load entity → verify `OwnerId == currentUser` → act" pattern is now duplicated across several command handlers (update/delete/publish listing, photo commands, inquiry status commands). Extract into a small shared helper or a MediatR authorization behaviour. Left inline so far because premature abstraction is worse than visible duplication; it has now recurred enough to justify the refactor.
- **Reusable exception-handling filter** — controllers repeat `try/catch (ValidationException | NotFoundException | ForbiddenAccessException)` blocks that map to `ModelState` / `NotFound()` / `Forbid()`. A reusable MVC action filter (or exception filter) would DRY this up. One or two endpoints didn't justify it; it now spans many.
- **Soft delete for listings** — current listing delete is a hard delete (row + R2 objects removed). A production system typically prefers soft delete (`IsDeleted` flag + global query filter) so listings can be recovered and history preserved. Clean to add via an EF query filter; would also change the R2-cleanup timing.

## Performance

- **Cache the unread-inquiry count** — the nav badge runs a `COUNT` query on every page load for logged-in agents. Fine at current scale; if it ever matters, cache it briefly (short in-memory TTL) or refresh on inquiry create / mark-read.
- **`IQueryable` projection for read queries** — some read paths materialise entities then map/fill in memory (e.g. cover-image URLs, detail images). At scale, projecting straight to DTO columns in SQL is cheaper. Kept explicit for readability while data volumes are tiny.

## Quality

- **Automated tests** — the solution has `Domain.UnitTests`, `Application.UnitTests`, and `IntegrationTests` projects scaffolded but empty. Highest-value targets: domain entity invariants (status transitions, value-object validation), Application handlers (validation, ownership enforcement, geocode-on-save), and a few integration tests over the real query pipeline. Stack per `ARCHITECTURE.md`: xUnit + NSubstitute + Shouldly (Shouldly chosen over the now-paid FluentAssertions).

---

*Last updated: 2026-07-10*
