# POS App Backend (.NET 8)

Production-ready minimal API for a Point-of-Sale system showcasing Clean Architecture, JWT auth with refresh tokens, CQRS/MediatR-style handlers, EF Core with PostgreSQL, and first-class DX (Swagger in Development, Serilog, versioned endpoints).

## Highlights

- **Authentication**: JWT access tokens + HttpOnly refresh token cookie
- **Authorization**: Role-based policies (admin-only menu write operations)
- **Domain**: Tickets, lines, menu items; payment flows (cash + mock gateway)
- **Architecture**: Clean layering (`Domain`, `Application`, `Infrastructure`, `Api`) with pipeline behaviors (validation, logging, transactions)
- **Observability**: Serilog console logs, consistent ProblemDetails, response `x-trace-id`
- **Developer Experience**: API versioning, Swagger UI in Development, Docker-first setup

## Tech Stack

- .NET 8 Minimal APIs
- EF Core + PostgreSQL
- MediatR-style request handlers (Application layer)
- Serilog
- API Versioning
- Docker Compose for local infra

## Project Structure

```text
src/
  PosApp.Domain/          # Entities, domain exceptions
  PosApp.Application/     # Contracts (DTOs), features (commands/queries), behaviors
  PosApp.Infrastructure/  # EF Core, repositories, token service, options, migrations
  PosApp.Api/             # Minimal API endpoints, composition root, middleware, swagger

tests/
  PosApp.Domain.Tests/        # Domain unit tests
  PosApp.Application.Tests/   # Handler tests
  PosApp.Integration.Tests/   # End-to-end API tests
```

## Quickstart

### Option A: Docker Compose (fastest)

Prereqs: Docker Desktop

```bash
# from repo root
docker-compose up -d
# API: http://localhost:5001
# Postgres: localhost:5433 (container 5432)
```

Notes:
- The API runs in Production by default in Compose. Root path `/` returns `{ "status": "ok" }`.
- To enable Swagger in the container, set `ASPNETCORE_ENVIRONMENT=Development` for the `api` service.

Seeded admin credentials (via config):
- Email: `admin@example.com`
- Password: `ChangeMe123!`

### Option B: Local .NET + local PostgreSQL

Prereqs: .NET 8 SDK, a local PostgreSQL (default: `localhost:5432`, db `pos`, user/password `postgres`)

1) Set required JWT secrets (access + refresh) and optional CORS:

```bash
export Jwt__SigningKey="dev-only-change-in-prod-please-rotate"
export RefreshJwt__SigningKey="dev-only-change-in-prod-please-rotate-refresh"
export AllowedOrigins="http://localhost:3000"
# optional: override connection string
# export ConnectionStrings__Default="Host=localhost;Port=5432;Database=pos;Username=postgres;Password=postgres"
```

2) Run the API:

```bash
dotnet run --project src/PosApp.Api
# Dev profile exposes: http://localhost:5001 (Swagger available in Development)
```

On first run the app applies migrations and seeds a basic menu and an admin user.

## Exploring the API

- Swagger (Development only): `http://localhost:5001/swagger`
- All business endpoints are versioned internally at v1 and live under `/api/*`.
- Pagination headers on list endpoints: `X-Page-Index`, `X-Page-Size`, `X-Total-Count`.

### Auth flow

Login issues an access token (response body) and a refresh token (HttpOnly cookie). Use the access token in `Authorization: Bearer ...`. Use cookies to call refresh.

```bash
# Register
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user1@example.com","password":"P@ssw0rd!","role":"user"}'

# Login (save access token and refresh cookie)
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"ChangeMe123!"}' \
  -c cookie.txt | tee login.json
export TOKEN=$(jq -r '.access_token' < login.json)

# Me
curl http://localhost:5001/api/auth/me \
  -H "Authorization: Bearer $TOKEN"

# Refresh (uses refresh_token cookie)
curl -X POST http://localhost:5001/api/auth/refresh -b cookie.txt

# Logout
curl -X POST http://localhost:5001/api/auth/logout -b cookie.txt -i
```

### Menu (requires any authenticated user for read; admin for write)

```bash
# List menu items (supports q, pageIndex, pageSize)
curl "http://localhost:5001/api/menu?q=nasi&pageIndex=0&pageSize=20" \
  -H "Authorization: Bearer $TOKEN" -i
# Response headers: X-Page-Index, X-Page-Size, X-Total-Count

# Create menu item (admin only)
curl -X POST http://localhost:5001/api/menu \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"Es Teh","price":8000}' -i

# Update menu item (admin only)
curl -X PUT http://localhost:5001/api/menu/{id} \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"Es Teh Jumbo","price":12000}' -i

# Delete menu item (admin only)
curl -X DELETE http://localhost:5001/api/menu/{id} \
  -H "Authorization: Bearer $TOKEN" -i
```

### Tickets

```bash
# Create ticket
curl -X POST http://localhost:5001/api/tickets \
  -H "Authorization: Bearer $TOKEN"
# -> { "id": "..." }
export TICKET_ID=... # set from response

# Add line to ticket
curl -X POST http://localhost:5001/api/tickets/$TICKET_ID/lines \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"menuItemId":"{menu-item-guid}","qty":2}' -i

# Pay by cash
curl -X POST http://localhost:5001/api/tickets/$TICKET_ID/pay/cash \
  -H "Authorization: Bearer $TOKEN" -i

# Pay via mock gateway (simulate success/failure)
curl -X POST http://localhost:5001/api/tickets/$TICKET_ID/pay/mock \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"shouldSucceed":true}' -i

# Get a ticket
curl http://localhost:5001/api/tickets/$TICKET_ID \
  -H "Authorization: Bearer $TOKEN"

# List tickets (paginated)
curl "http://localhost:5001/api/tickets?pageIndex=0&pageSize=20" \
  -H "Authorization: Bearer $TOKEN" -i
```

## Configuration

Environment variables (names match .NET configuration binder):

- `ConnectionStrings__Default` – connection string for EF Core
- `AllowedOrigins` – comma-separated CORS origins (default `http://localhost:3000`)
- `Admin__Email`, `Admin__Password` – admin seeding
- `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`, `Jwt__AccessTokenTTLMinutes`
- `RefreshJwt__Issuer`, `RefreshJwt__Audience`, `RefreshJwt__SigningKey`, `RefreshJwt__TTLDays`
- `ASPNETCORE_ENVIRONMENT` – set to `Development` to enable Swagger UI

Docker Compose sets sensible defaults for local use; see `docker-compose.yml` for exact values.

## Testing

```bash
dotnet test
```

- Domain tests validate core entities/behavior
- Application tests cover command/query handlers
- Integration tests boot the API and exercise real endpoints

## Logging and Error Handling

- Serilog console logging with sane defaults (see `appsettings*.json`)
- All error responses use RFC 7807 ProblemDetails with helpful `traceId`
- Every response includes `x-trace-id` header for correlation

## Deployment

- Multi-stage Dockerfile at `src/PosApp.Api/Dockerfile`
- Environment-first configuration via .NET config binding (env vars override JSON)
- Health: in Production, `GET /` returns `{ "status": "ok" }`

## Roadmap / Trade-offs

- Mock payment gateway demonstrates integration seam; could be swapped for Stripe/Midtrans
- Add OpenAPI examples and response schemas for each endpoint
- Add role management endpoints and richer user profile

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
