# Restaurant Management System

**ASP.NET Core MVC + PostgreSQL**

This project is a full-stack **Restaurant Management System** developed as part of an internship project.  
It is designed to model real-world restaurant operations including orders, tables, kitchen workflow, payments, reservations, reporting, and administrative management.

## 🧱 Technology Stack

- **Backend:** ASP.NET Core MVC
- **Target Framework:** .NET 10 SDK
- **Database:** PostgreSQL 18
- **Data Access:** Npgsql (ADO.NET, repository pattern)
- **Frontend:** Razor Views, Bootstrap, Vanilla JS
- **Charts & Reports:** Chart.js, ClosedXML (Excel export)
- **Authentication:** Cookie-based auth
- **Security:** PBKDF2 password hashing with per-user salt

## 🏗️ Architecture Overview

The project follows a clear separation of responsibilities:
Controllers → Services → Repositories → Database

**Key architectural decisions:**
**Repository Pattern**
All database access is isolated in repository classes using raw SQL via Npgsql.

**Service Layer**
Business rules, validation, and orchestration live in services.

**ViewModels & DTOs**
Strong separation between domain models and UI/data-transfer models.

**OperationResult Pattern**
Standardized success/failure handling without throwing control-flow exceptions.

**Idempotent Seeding**
Database seed operations can safely run multiple times.

## 🔐 Authentication & Authorization

- Cookie-based authentication
- Role-based authorization using a bit-flag enum
- Passwords are never stored in plain text

## 🚀 Step-by-step Setup (Windows)

### 0) Requirements (install once)

You need:

1. **.NET 10 SDK**
   - Check:
     ```bash
     dotnet --version
     ```
     If this command fails, install .NET 10 SDK.

2. **PostgreSQL 18**
   - Install PostgreSQL 18 and remember your **postgres** password.

3. **pgAdmin 4** (usually installed with PostgreSQL)
   - We will use pgAdmin to run SQL scripts.

### 1) Clone the repository

Open a terminal in the folder where you want the project:

```bash
git clone https://github.com/dlgonur/RestaurantWeb.git
cd RestaurantWeb
```

If you already downloaded the zip, just extract it and cd into that folder.

### 2) Create a PostgreSQL database

Open pgAdmin 4:

- 1. Connect to your server (PostgreSQL 18).
- 2. Right-click Databases → Create → Database
- 3. Database name:
     restaurant
- 4. Owner:
     postgres
- 5. Click Save
- ✅ You now have an empty database.

### 3) Run the database schema (create tables)

Still in pgAdmin:

- 1. Click the database restaurant (select it)
- 2. Go to Tools → Query Tool
- 3. Open the file:
     database/schema.sql
- 4. Press Run (▶)
- ✅ If everything is OK you should see “Query returned successfully”.

### 4) Configure appsettings.json (connection string)

In the project folder:

- 1. Go to:
     RestaurantWeb/appsettings.example.json
- 2. Copy it and create:
     RestaurantWeb/appsettings.json
- 3. Open RestaurantWeb/appsettings.json and edit the connection string:
     "PostgreSqlConnection": "Host=localhost;Port=5432;Database=restaurant;Username=postgres;Password=YOUR_PASSWORD"
     Replace YOUR_PASSWORD with your PostgreSQL password.
- ✅ Save the file.

### 5) Run the application

- In the repo root, run:

```bash
  dotnet run --project RestaurantWeb
```

Wait until you see something like:

- Now listening on: http://localhost:5280
- Open your browser and go to:
  http://localhost:5280

### 6) First login (AUTO admin seed)

- On the first startup, if there is no user in the database:
- A default admin user is created automatically.
- The password is printed to the application logs (terminal output).
- You will see a log line like:

- [SeedAdmin] Created admin user. Username=admin Password=XXXXXX

- ✅ After login, you can create additional users from the UI (as admin).

### 7) 🧪 Optional: Use demo database (sample data)

If you want a ready-to-explore database with sample data:

- 1. Create an empty database named restaurant (same as above).
- 2. Restore demo backup using:

```bash
     pg_restore -h localhost -p 5432 -U postgres -d restaurant -c database/demo.backup
```

- 3. Then run the app normally:

```bash
     dotnet run --project RestaurantWeb
```

## 📄 Documentation

A detailed project document is available under:
docs/Restoran Yönetim Sistemi.pdf
This document explains:

- System design
- Data model
- Business rules
- Screens and workflows

**Author: Onur**
**Context: Internship Project**
