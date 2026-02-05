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
- Role-based authorization using a bit-flag enum:
- Passwords are never stored in plain text

## 🗄️ Database Setup

Two setup options are provided.

**Option A — Clean Setup**

- 1. Create an empty PostgreSQL database:
     CREATE DATABASE restaurant;

- 2. Run the schema script:
     psql -U postgres -d restaurant -f database/schema.sql

- 3. Configure the application:
     Copy RestaurantWeb/appsettings.example.json
     Rename it to appsettings.json
     Update the PostgreSQL connection string

- 4. Run the application:
     dotnet run

- On first startup:
  Tables are seeded idempotently
  Default tables are created
  If no user exists, a default admin user is created at startup.
  The generated password is written to the application logs.

**Option B — Demo Database**

A demo database backup with sample data is provided.
Restore using:
pg_restore -h localhost -p 5432 -U postgres -d restaurant -c database/demo.backup
This option allows reviewers to immediately explore the system with realistic data.

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
