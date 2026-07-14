# RealEstatePortal

A production-shaped, multi-agent real estate portal built with **ASP.NET Core 8** and **SQL Server**, following **Clean Architecture** and **CQRS**. It serves a server-rendered web app *and* a JWT-secured **REST API** from one shared domain core — with photo-rich, geolocated listings, geospatial search, a complete leads workflow, admin moderation, and a three-layer automated test suite.

The portal is branded **EmlakPortal** in the UI and set in İstanbul.

> **Status:** internship / portfolio project. Built to production-quality standards but not deployed. Some conveniences (a seeded admin account) are development-only — see [Notes](#notes).

---

## Table of contents

- [Screenshots](#screenshots)
- [Features](#features)
- [Tech stack](#tech-stack)
- [Architecture](#architecture)
- [REST API](#rest-api)
- [Testing](#testing)
- [Getting started](#getting-started)
- [Project structure](#project-structure)
- [Configuration reference](#configuration-reference)
- [Notes](#notes)
- [Roadmap](#roadmap)

---

## Screenshots

> _Add your own screenshots to a `docs/` folder and update the paths below._

| Browse | Listing detail |
|---|---|
| ![Browse page](docs/browse.png) | ![Listing detail](docs/detail.png) |

| Agent dashboard | API docs (Swagger) |
|---|---|
| ![My listings](docs/dashboard.png) | ![Swagger UI](docs/swagger.png) |

---

## Features

### For visitors
- Browse published listings with a **map-driven search** — filter by keyword, type, property, and price.
- **Geospatial "search this area"** — pan the map and search within a chosen radius (real SQL Server `geography` distance queries).
- Rich **listing detail pages** with a photo gallery, a facts panel, a location map, and full SEO (canonical slug URLs, OpenGraph, JSON-LD structured data).
- **Contact an agent** directly from a listing — the inquiry is saved and the agent is emailed.

### For agents
- Register, sign in, and manage **your own listings** (create / edit / publish / delete) with data-level ownership enforcement.
- **Photo management** — multi-upload with staged previews, processed to WebP and stored in object storage; set-cover and delete with storage cleanup.
- **Interactive map pin** — geocode a typed address to a first guess, then drag the marker to the exact spot; the marker position is what's saved.
- **Inquiry inbox** with an unread badge; mark inquiries read/handled.
- Get an **email notification** the moment your listing goes live (via a domain event).

### For administrators
- **Moderation dashboard** — view every listing across all agents, filter by status, and archive / restore / delete (with storage cleanup).
- Role-based access; the admin's actions are automatically audit-stamped.

### Under the hood
- **Clean Architecture** (Domain / Application / Infrastructure / Web) with the dependency rule enforced throughout.
- **CQRS** via MediatR, with pipeline behaviours for validation, logging, and performance.
- **Domain events** dispatched after commit through a `SaveChanges` interceptor.
- **Automatic auditing** (created/modified by whom & when) via an EF Core interceptor.
- **JWT-secured REST API** running alongside cookie-based MVC auth — the same authorization rules apply to both.
- **~45 automated tests** across three layers (domain, application, and full-pipeline integration).
- A **custom design system** (pine-green & brass, Spectral + Manrope) — no stock Bootstrap look.

---

## Tech stack

| Area | Technology |
|---|---|
| Framework | ASP.NET Core 8 (MVC + Web API) |
| Language | C# 12 / .NET 8 |
| Database | SQL Server (LocalDB in development) |
| ORM | Entity Framework Core 8 (code-first migrations) |
| Architecture | Clean Architecture (Jason Taylor convention), CQRS |
| Mediator / CQRS | MediatR |
| Mapping | Mapperly (source-generated) |
| Validation | FluentValidation |
| Auth | ASP.NET Core Identity (cookies) + JWT bearer (API) |
| Object storage | Cloudflare R2 (S3-compatible, via AWS SDK) |
| Image processing | SixLabors ImageSharp (→ WebP) |
| Geocoding | Nominatim (OpenStreetMap) with a fallback chain |
| Geospatial | NetTopologySuite + SQL Server `geography` |
| Maps | Leaflet + OpenStreetMap |
| Email | MailKit (SMTP) |
| Logging | Serilog |
| API docs | Swashbuckle / OpenAPI (Swagger UI) |
| Testing | xUnit, NSubstitute, MockQueryable, Shouldly, Respawn, WebApplicationFactory |

---

## Architecture

The solution follows **Clean Architecture**: dependencies point only inward, so the business core knows nothing about the database, the web framework, or any external service.

```
┌──────────────────────────────────────────────┐
│                     Web                       │  MVC controllers, views, API controllers,
│  ┌────────────────────────────────────────┐  │  composition root
│  │              Infrastructure             │  │  EF Core, Identity, R2, email, geocoding,
│  │  ┌──────────────────────────────────┐  │  │  spatial search
│  │  │            Application            │  │  │  CQRS use cases (commands/queries),
│  │  │  ┌────────────────────────────┐  │  │  │  interfaces, validation, behaviours
│  │  │  │           Domain           │  │  │  │  entities, value objects, enums,
│  │  │  └────────────────────────────┘  │  │  │  domain events — zero dependencies
│  │  └──────────────────────────────────┘  │  │
│  └────────────────────────────────────────┘  │
└──────────────────────────────────────────────┘
        Web → Application → Domain
        Infrastructure → Application → Domain
```

Key patterns: every operation is a MediatR **command or query**; external capabilities (storage, email, geocoding, spatial search, token issuance) sit behind **Application-defined interfaces** implemented in Infrastructure; cross-cutting concerns (validation, auditing, domain-event dispatch, geography projection) run through **EF Core interceptors** and **MediatR behaviours**.

**Design decisions are documented as ADRs** in [`ARCHITECTURE.md`](ARCHITECTURE.md) — nine of them, covering the Identity boundary, the repository-less data access, CQRS, the hybrid attribute model, staged geospatial search, the EF-Core-in-Application compromise, the isolated NetTopologySuite design, the read API, and the dual cookie/JWT auth scheme.

---

## REST API

A public REST API is exposed alongside the web UI and documented with **Swagger** (at `/swagger` in development).

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/listings` | none | Browse active listings (filters, paging, radius search) |
| `GET` | `/api/listings/{id}` | none | Single listing detail |
| `POST` | `/api/auth/login` | none | Exchange credentials for a JWT |
| `GET` | `/api/auth/me` | JWT | Current caller identity |
| `POST` | `/api/listings` | JWT (Agent) | Create a listing |
| `PUT` | `/api/listings/{id}` | JWT (Agent) | Update a listing you own |
| `POST` | `/api/listings/{id}/publish` | JWT (Agent) | Publish a listing you own |
| `DELETE` | `/api/listings/{id}` | JWT (Agent) | Delete a listing you own |

The API controllers send the **same MediatR commands/queries** as the MVC UI, so ownership enforcement, validation, and auditing apply identically — machine clients and browser users share one set of business rules.

---

## Testing

Around 45 tests across three projects, each covering what the others structurally can't:

- **`Domain.UnitTests`** — value-object validation and entity invariants (status transitions, value equality). No mocks.
- **`Application.UnitTests`** — command/query handlers with mocked dependencies (NSubstitute + MockQueryable): ownership enforcement, geocode-on-save, slug uniqueness, photo-upload orchestration, the domain-event handler, and the Nominatim fallback chain (via a stub `HttpMessageHandler`).
- **`IntegrationTests`** — the real pipeline against LocalDB (reset per-test with Respawn): EF query translation, the MediatR pipeline, the geography radius query, domain-event dispatch, and the API over real HTTP with JWT auth (`WebApplicationFactory`).

Run them all from Visual Studio's Test Explorer, or:

```bash
dotnet test
```

---

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server LocalDB** (ships with Visual Studio; or any SQL Server instance)
- Visual Studio 2022/2026, or the `dotnet` CLI
- _(optional)_ A **Cloudflare R2** bucket + API token — required for photo upload/display
- _(optional)_ A local SMTP inbox such as [Papercut SMTP](https://github.com/ChangemakerStudios/Papercut-SMTP) — to see notification emails

### 1. Clone

```bash
git clone https://github.com/akatakan03/RealEstatePortal.git
cd RealEstatePortal
```

### 2. Configure secrets

Non-secret settings live in `src/RealEstatePortal.Web/appsettings.json` (R2 endpoint/bucket/public URL, JWT issuer/audience, SMTP host). **Secrets** go in User Secrets so they never touch the repo. From the Web project folder:

```bash
cd src/RealEstatePortal.Web
dotnet user-secrets init
```

Then set the minimum needed to run:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RealEstatePortalDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "R2": {
    "AccessKey": "<your-r2-access-key>",
    "SecretKey": "<your-r2-secret-key>"
  },
  "Jwt": {
    "Key": "<a-long-random-secret-of-at-least-32-characters>"
  }
}
```

> The `R2` section must be present for the app to start. To try the app **without** photo features, you can use placeholder R2 values — everything except image upload/display will work. Also fill in your bucket's `ServiceUrl`, `BucketName`, and `PublicUrl` in `appsettings.json`.

### 3. Local email _(optional)_

Start **Papercut SMTP** (or any SMTP server on `localhost:25`) to receive inquiry and listing-published notifications. The defaults are in `appsettings.json` under `Email`.

### 4. Apply migrations

```bash
dotnet ef database update \
  --project src/RealEstatePortal.Infrastructure \
  --startup-project src/RealEstatePortal.Web
```

_(In Visual Studio: Package Manager Console → set default project to `RealEstatePortal.Infrastructure` → `Update-Database`.)_

### 5. Run

```bash
dotnet run --project src/RealEstatePortal.Web
```

Open the printed HTTPS URL. Swagger is at `/swagger`.

### Default login

A development administrator is seeded on first run:

| Email | Password |
|---|---|
| `admin@realestate.local` | `Admin123!` |

Register a new account from the UI to get an **Agent** (agents can create and manage listings).

---

## Project structure

```
RealEstatePortal.sln
├── src/
│   ├── RealEstatePortal.Domain/          # Entities, value objects, enums, domain events. No dependencies.
│   ├── RealEstatePortal.Application/      # CQRS commands/queries, interfaces, validation, behaviours.
│   ├── RealEstatePortal.Infrastructure/   # EF Core, Identity, R2, email, geocoding, spatial, JWT.
│   └── RealEstatePortal.Web/              # MVC + API controllers, views, design system, composition root.
└── tests/
    ├── RealEstatePortal.Domain.UnitTests/
    ├── RealEstatePortal.Application.UnitTests/
    └── RealEstatePortal.IntegrationTests/
```

---

## Configuration reference

| Setting | Location | Purpose |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | User Secrets | SQL Server / LocalDB connection |
| `R2:ServiceUrl`, `R2:BucketName`, `R2:PublicUrl` | appsettings | Cloudflare R2 endpoint, bucket, public base URL |
| `R2:AccessKey`, `R2:SecretKey` | User Secrets | R2 API credentials |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | appsettings | JWT token parameters |
| `Jwt:Key` | User Secrets | JWT signing key (32+ chars) |
| `Email:Host`, `Email:Port`, `Email:FromAddress` | appsettings | SMTP settings for notifications |

---

## Notes

- **Not deployed.** This is a learning/portfolio project built to production-quality standards, not a live service. Hosting was intentionally out of scope.
- **The seeded admin credentials are development-only.** In a real deployment they would be moved to configuration/secrets with a strong generated password.
- **Geocoding** uses the public Nominatim service, which is rate-limited and resolves sparsely-mapped Turkish streets only to district level — hence the interactive map pin, which lets agents set the exact location by hand.
- The public **r2.dev** bucket URL is for development; a production deployment would attach a custom domain.

---

## Roadmap

Planned and deferred work — with reasoning for each deferral — is tracked in [`BACKLOG.md`](BACKLOG.md). Highlights still open: favorites & saved searches, real-time (SignalR) inquiry notifications, background job processing, API integration test breadth, and a refresh-token flow.

---

*Built with ASP.NET Core 8 · Clean Architecture · CQRS*
