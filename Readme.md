# Conference Room Booking API

A C#/.NET 10 REST API built as a technical assignment. It manages conference rooms, bookings and rental pricing.

[Original assignment (Ukrainian)](docs/assignment.uk.md)

## What it does

- Searches rooms by time slot and capacity.
- Calculates rental costs across configurable tariff periods, including partial hours and bookings spanning midnight.
- Stores service names and prices with each booking to preserve its original cost.
- Provides revenue and room utilization reports.

The code is split into Domain, Application, Infrastructure and Web API layers. It uses EF Core with SQL Server, MediatR for commands and queries, and FluentValidation for input validation.

Pricing rules live in [appsettings.json](src/MainWeb/appsettings.json). For example, a booking from 11:00 to 15:00 combines the standard rate with the 12:00-14:00 peak rate.

## Run locally

Requires Docker with Compose and Git. Ports 80, 443 and 14330 must be available.

```sh
git clone https://github.com/SaveGex/conference-room-booking-api.git
cd conference-room-booking-api
cp .env.example .env
docker compose up --build
```

In PowerShell, use `Copy-Item .env.example .env` instead of `cp`.

Open [Swagger UI](https://localhost/swagger). Compose starts SQL Server, the API and an Nginx proxy. It generates a self-signed certificate for localhost, so the browser will report it as untrusted. The API creates the database schema and seeds sample rooms and services on first launch.

The credentials in `.env.example` are for local development only.

Stop the containers while keeping database data:

```sh
docker compose down
```

## Tests

With the .NET 10 SDK installed:

```sh
dotnet test src/MainWeb.slnx
```

Unit tests cover pricing boundaries, booking validation, room operations and reports. They do not require a running SQL Server.

A [Postman collection](tests/Postman/Conference%20Room%20Booking%20API.postman_collection.json) is also included for manual API checks.

## Current limitations

- The overlap check and booking insert are separate operations. Concurrent requests can still create conflicting bookings.
- Authentication and authorization are not implemented.
- Database initialization uses `EnsureCreatedAsync`; schema upgrades are not applied automatically.
