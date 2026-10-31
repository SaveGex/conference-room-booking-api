# Conference Room Booking API

[![CI](https://github.com/SaveGex/conference-room-booking-api/actions/workflows/ci.yml/badge.svg)](https://github.com/SaveGex/conference-room-booking-api/actions/workflows/ci.yml)

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

To also run the concurrency tests against SQL Server, set `BOOKING_TEST_SQLSERVER` to a connection string with permission to create databases. Each test creates and removes its own uniquely named database. For Windows LocalDB:

```powershell
$env:BOOKING_TEST_SQLSERVER = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'
dotnet test src/MainWeb.slnx
```

Booking creation locks the room with `UPDLOCK, HOLDLOCK` inside a transaction before checking availability and saving. Concurrent requests for the same room wait for that transaction to finish. Any future operation that changes a booking's room or time must follow the same locking protocol.

A [Postman collection](tests/Postman/Conference%20Room%20Booking%20API.postman_collection.json) is also included for manual API checks.

## CI

GitHub Actions builds the solution, runs both unit-test suites and SQL Server concurrency tests, then builds and starts the API container and runs the Postman collection with Newman. Test reports and API logs are available as workflow artifacts.

Successful runs on `main` also save the tested Docker image as the `verified-image` artifact. Download and extract it, then run `docker load -i booking-api.tar.gz` to load `booking-api:ci`. The workflow does not deploy an application server or publish to a container registry.

## Current limitations

- Authentication and authorization are not implemented.
- Database initialization uses `EnsureCreatedAsync`; schema upgrades are not applied automatically.
