# POS App Backend (.NET 8)

Production-ready minimal API for a Point-of-Sale system showcasing Clean Architecture, JWT auth with refresh tokens, CQRS/MediatR-style handlers, EF Core with PostgreSQL, and first-class DX (Swagger in Development, Serilog, versioned endpoints).

## Highlights

- **Authentication**: JWT access tokens + HttpOnly refresh token cookie
- **Authorization**: Role-based policies (admin-only menu write operations)
- **Domain**: Tickets, lines, menu items; payment flows (cash + mock gateway)
- **Architecture**: Clean layering (`Domain`, `Application`, `Infrastructure`, `Api`) with pipeline behaviors (validation, logging, transactions, idempotency)
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

## Idempotency

Write operations that implement `IIdempotentRequest<T>` are protected by request idempotency.

- **Header**: send `Idempotency-Key: <unique-string>` on write requests.
- **Scope**: keyed per request type and user. The same key can be reused for different request types without collision.
- **Behavior**:
  - First request stores a pending record, processes normally, then persists the response payload.
  - A subsequent identical request (same key, same user, same request type, same body) returns the original response with the original status code.
  - If the same key is reused with a different payload, the API returns `400 Bad Request` with a ProblemDetails message: "Idempotency key conflict: payload does not match previous request.".
  - If the first request is still being processed, a retry with the same key may return a ProblemDetails message: "Request with this idempotency key is currently being processed. Please retry later.".
- **Persistence**: records are stored in the database (`IdempotencyRecords`) with request hash and serialized response.
- **Enabled endpoints** (via Application commands implementing `IIdempotentRequest<T>`):
  - Create ticket
  - Add ticket line
  - Pay ticket (cash)
  - Pay ticket via mock gateway

Examples (replace tokens/IDs as needed):

```bash
# Create ticket (idempotent)
KEY=$(uuidgen)
curl -X POST http://localhost:5001/api/tickets \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $KEY" -i

# Add line to ticket (idempotent)
LINE_KEY=$(uuidgen)
curl -X POST http://localhost:5001/api/tickets/$TICKET_ID/lines \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $LINE_KEY" \
  -d '{"menuItemId":"{menu-item-guid}","qty":2}' -i

# Pay by cash (idempotent)
PAY_KEY=$(uuidgen)
curl -X POST http://localhost:5001/api/tickets/$TICKET_ID/pay/cash \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $PAY_KEY" -i

# Pay via mock gateway (idempotent)
GW_KEY=$(uuidgen)
curl -X POST http://localhost:5001/api/tickets/$TICKET_ID/pay/mock \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $GW_KEY" \
  -d '{"shouldSucceed":true}' -i
```

Notes:
- Use a fresh `Idempotency-Key` for each logical operation initiated by the client.
- Keys are user-scoped; if unauthenticated, the key is treated without a user scope.

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
