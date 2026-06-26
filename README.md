# InsightEngine User Service

![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)

A user management and identity service built on .NET and PostgreSQL. The User Service provides comprehensive user authentication, authorization, role management, and organizational hierarchy capabilities for the InsightEngine platform.

## Table of Contents
- [System Context](#system-context)
- [Overview](#overview)
- [Governance and Origin](#governance-and-origin)
- [Key Features](#key-features)
- [Architecture](#architecture)
- [Technology Stack](#technology-stack)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Running the Application](#running-the-application)
- [Database Setup](#database-setup)
- [API Documentation](#api-documentation)
- [Authentication & Authorization](#authentication--authorization)
- [Email Integration](#email-integration)
- [Deployment](#deployment)
- [Contributing](#contributing)
- [Development Practices](#development-practices)
- [License](#license)


## System Context

This repository is one of four core services that comprise the InsightEngine platform:

- **User Service (`C-brAIn-InsightEngine-user-service` - this repository)** — authentication, authorization, identity management, and organizational access control
- **Backend API Service (`C-brAIn-InsightEngine-api`)** — core application backend and service orchestration
- **Web Frontend (`C-brAIn-InsightEngine-web`)** — user-facing application and research workspace
- **AI Service (`C-brAIn-InsightEngine-ai`)**  — scientific reasoning, retrieval, and hypothesis evaluation

The User Service acts as the platform identity provider and access management layer, enabling secure authentication and authorization across all platform services.

## Overview

The User Service provides identity, authentication, authorization, organizational management, and user lifecycle functionality for the InsightEngine platform. It provides a unified API for managing user accounts, authentication workflows, organizational hierarchies, and role-based access control (RBAC). The service integrates with IdentityServer for advanced OAuth 2.0 and OpenID Connect flows, ensuring secure inter-service communication.

## Governance and Origin

This project is part of the InsightEngine platform, a multi-service system for biomedical research and AI-assisted scientific workflows.

The platform was initiated and funded through the Consortium for Biomedical Research & AI in Neurodegeneration (C-BRAIN) and developed through a collaboration between Arti Analytics Inc., 387Labs, and collaborating academic researchers and research institutions.

This repository provides identity, authentication, authorization, and organizational management services for the broader C-BRAIN ecosystem.

**Primary Responsibilities:**
- User account creation, management, and lifecycle
- JWT token generation and validation
- Role and permission management
- Organization and team management
- Email-based user communications (confirmations, password resets, notifications)
- Authentication and authorization for platform microservices

## Key Features

### Authentication & Security
- **JWT-based Authentication**: Secure token-based authentication with configurable expiration
- **IdentityServer Integration**: Support for OAuth 2.0, OpenID Connect, and custom grant types
- **Role-Based Access Control (RBAC)**: Hierarchical role system (SuperAdmin, Admin, OrganizationAdmin, User)
- **Multi-Tenant Support**: Organization-based data isolation and role inheritance
- **Configurable JWT Keys**: Support for multiple signing and validation keys

### User Management
- User registration and profile management
- Email verification workflow
- Temporary and permanent password management
- EULA acceptance tracking
- User suspension and activation capabilities
- Subscription management and expiration tracking

### Email Communication
- **Brevo Integration**: Transactional email delivery and notification services
- **Templated Emails**: HTML email templates for various user events:
  - Account confirmation
  - Password reset notifications
  - Account approval notifications
  - Subscription expiration warnings
  - Temporary password notifications
- **Customizable Email Content**: Data-driven email templates with user context

### Organization Management
- Organization hierarchy and team structure
- Role assignment per organization
- Organization-specific configurations
- Multi-organizational user support

### Localization
- Multi-language support (en-US, bs-BA, hr-HR, de-DE)
- Configurable default culture
- Culture-specific error messages

## Architecture

### System Design

```
┌─────────────────────────────────────────────────────────────┐
│                    API Consumers                            │
├─────────────────────────────────────────────────────────────┤
│     Proxy / API Gateway (Nginx/Azure Container Apps)        │
├─────────────────────────────────────────────────────────────┤
│                   User Service API                          │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         ASP.NET Core Controllers                     │   │ 
│  │  [Auth] [Accounts] [Users] [Tokens] [Organization]   │   │
│  └─────────────────────────┬────────────────────────────┘   |
│                            │                                │
│  ┌─────────────────────────▼───────────────────────────┐    │
│  │        Application Services Layer                   │    │
│  │  [UserService] [AccountService] [TokenManager]      │    │
│  │  [EmailSender] [OrganizationService]                │    │
│  └─────────────────────────┬───────────────────────────┘    │
│                            │                                │
│  ┌─────────────────────────▼───────────────────────────┐    │
│  │    Data Access Layer (Entity Framework Core)        │    │
│  │         ApplicationDbContext                        │    │
│  └─────────────────────────┬───────────────────────────┘    │
│                            │                                │
├────────────────────────────┼────────────────────────────────┤
│  Database: PostgreSQL                                       │
│  Logging: Serilog Console/File                              │
│  Email: Brevo API                                           │
│  Auth: IdentityServer + JWT                                 │
└─────────────────────────────────────────────────────────────┘
```

## Technology Stack

| Technology | Purpose |
|------------|---------|
| .NET / ASP.NET Core | Application framework |
| Entity Framework Core | Data access and ORM |
| PostgreSQL | Primary database |
| Duende IdentityServer | Identity and authentication |
| JWT Authentication | API security |
| AutoMapper | Object mapping |
| Serilog | Logging |
| Swagger / OpenAPI | API documentation |
| Brevo | Transactional email delivery |

## Prerequisites

- **.NET SDK 10.0** or higher
- **PostgreSQL 12** or higher
- **Docker** (for containerized deployment)
- **Git** (for version control)
- **Brevo Account** (for email functionality)
- Text Editor or IDE (Visual Studio 2022, Visual Studio Code, JetBrains Rider recommended)

## Installation

### 1. Clone the Repository

```bash
git clone <repository-url>
cd C-brAIn-InsightEngine-user-service
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Verify Installation

```bash
dotnet --version
```

## Configuration

### Environment Variables

Create a `.env` file in the project root or set environment variables in your system:

```bash
# Database
USERS_DB_CONNECTION_STRING=Host=localhost;Port=5432;Database=your-database;Username=your-username;Password=your_password

# Email Service
BREVO_API_KEY=your_brevo_api_key_here

# External Authentication
EXTERNAL_AUTH_SECRET_KEY=your_secret_key_here

# Application Environment
ASPNETCORE_ENVIRONMENT=Development
```

### Configuration Files

The service uses tiered configuration:

1. **appsettings.json** - Base configuration (committed to repo)
2. **appsettings.{Environment}.json** - Environment-specific overrides
3. **Environment Variables** - Runtime overrides (highest priority)

#### Key Configuration Sections

**JwtSettings** - JWT Token Configuration
```json
{
  "JwtSettings": {
    "Issuer": "https://your-issuer-url",
    "Audience": "your-audience",
    "Audiences": ["api1", "api2"],
    "AccessExpirationInMinutes": 60,
    "RefreshExpirationInMinutes": 1000
  }
}
```

**Cors** - Cross-Origin Resource Sharing
```json
{
  "Cors": {
    "AllowedOrigins": ["https://your-frontend.com"],
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
    "AllowedHeaders": ["*"]
  }
}
```

**ConnectionStrings** - Database Connection
```json
{
  "ConnectionStrings": {
    "UsersDb": "Host=your-host;Port=5432;Database=your-database;Username=your-username;Password=your-password"
  }
}
```

**BrevoApi** - Email Service
```json
{
  "BrevoApi": {
    "ApiKey": "your_api_key",
    "SenderEmail": "noreply@yourdomain.com",
    "SenderName": "Your Organization"
  }
}
```

**IdentityServer** - OAuth 2.0 Configuration
```json
{
  "IdentityServer": {
    "Clients": [
      {
        "ClientId": "your_client_id",
        "ClientSecret": "your_client_secret",
        "AllowedGrantTypes": ["client_credentials"],
        "AllowedScopes": ["api1", "api2"]
      }
    ]
  }
}
```

### Secret Management

For sensitive configuration in production:

1. **Azure Key Vault** (Recommended for Azure deployments)
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "JwtSettings:Issuer" "https://your-issuer"
   ```

2. **Environment Variables** (For containerized deployments)
   - Set in container orchestration platform (Kubernetes, Azure Container Apps, etc.)

3. **Configuration Providers**
   - The application supports multiple configuration sources with environment variable override

## Running the Application

### Development

For local development, sensitive configuration values can be provided through environment variables, User Secrets, or application settings files.
```bash
# Navigate to project directory
cd UsersService

# Run with hot-reload enabled
dotnet run

# Or with debug logging
dotnet run --configuration Debug
```

The API will be available at `https://localhost:5001` (HTTPS) or `http://localhost:5000` (HTTP).

Access Swagger UI at: `https://localhost:5001/swagger/index.html`

### Production Build

```bash
# Build the project
dotnet build --configuration Release

# Publish to output directory
dotnet publish --configuration Release --output ./publish

# Run published application
./publish/UsersService.exe  # Windows
./publish/UsersService     # Linux
```

### Docker Deployment

```bash
# Build Docker image
docker build -t cbrain-user-service:latest .

# Run container
docker run -d \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e USERS_DB_CONNECTION_STRING="Host=db;Port=5432;Database=YourDatabase;Username=YourUsername;Password=YourPassword" \
  -e BREVO_API_KEY="your_api_key" \
  cbrain-user-service:latest

# For Docker Compose
docker-compose up -d
```

## Database Setup

### Database Initialization

The application uses Entity Framework Core with PostgreSQL. Migrations are automatically applied on startup.

### Manual Migration Management

```bash
# Navigate to UsersService directory
cd UsersService

# Create a new migration
dotnet ef migrations add "MigrationName" -o Migrations

# Apply migrations to database
dotnet ef database update

# List all migrations
dotnet ef migrations list

# Revert to previous migration
dotnet ef database update "PreviousMigrationName"
```

### Database Schema

Key entities:
- **ApplicationUser** - User accounts with email, phone, and profile data
- **ApplicationRole** - Role definitions (Admin, SuperAdmin, OrganizationAdmin)
- **Organization** - Multi-tenant organizational data
- **UserRole** - Role assignments and permissions
- **UserSessions** - Token and session tracking

See [DbContext/Configurations](UsersService/DbContext/Configurations/) for detailed entity configurations.

## API Documentation

### Interactive Documentation

Access Swagger UI at `/swagger` after running the application.

### API Endpoints Overview

#### Authentication
- `POST /auth/login` - User login and token generation
- `POST /auth/refresh-token` - Refresh JWT token
- `GET /auth/me` - Get current user information
- `POST /auth/logout` - Invalidate session

#### Account Management
- `POST /accounts/register` - Create new user account
- `POST /accounts/confirm-email` - Confirm email address
- `POST /accounts/forgot-password` - Initiate password reset
- `POST /accounts/reset-password` - Complete password reset
- `POST /accounts/change-password` - Change password for authenticated user

#### User Operations
- `GET /users/{id}` - Get user details (Admin only)
- `PUT /users/{id}` - Update user profile
- `DELETE /users/{id}` - Deactivate user account
- `GET /users` - List users with pagination (Admin only)
- `POST /users/{id}/roles` - Assign role to user (Admin only)

#### Organization Management
- `GET /organizations` - List organizations
- `POST /organizations` - Create new organization
- `GET /organizations/{id}` - Get organization details
- `PUT /organizations/{id}` - Update organization
- `DELETE /organizations/{id}` - Delete organization

#### Token Management
- `POST /tokens/validate` - Validate access token
- `POST /tokens/introspect` - Get token claims and metadata

### Request/Response Examples

**Login Request**
```http
POST /auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "YourPassword"
}
```



## Authentication & Authorization

### JWT Token Structure

The service generates JWT tokens with the following claims:
- `sub` - Subject (User ID)
- `email` - User email address
- `role` - User role
- `org_id` - Organization ID
- `iss` - Issuer
- `aud` - Audience
- `exp` - Expiration time
- `iat` - Issued at
- `nbf` - Not before

### Role Hierarchy

```
SuperAdmin (Full system access)
  ├── Admin (System administration)
  ├── OrganizationAdmin (Organization management)
  └── User (Standard user)
```

### Protected Endpoints

All endpoints except `/auth/login` and `/accounts/register` require a valid JWT token in the `Authorization` header:

```http
Authorization: Bearer your_jwt_token_here
```

### Custom Authorization Policies

Configure role-based authorization in controllers:
```csharp
[Authorize(Roles = "Admin,OrganizationAdmin")]
public async Task<IActionResult> GetUsers() { ... }
```

## Email Integration

### Brevo Setup

1. Create a Brevo account at [https://www.brevo.com/](https://www.brevo.com/)
2. Generate API key from account settings
3. Set `BREVO_API_KEY` environment variable
4. Configure sender email and name in `appsettings.json`

### Email Templates

The service includes pre-configured HTML email templates in the `Templates/` directory:
- **AccountApprovedEmailTemplate.html** - Account approval notification
- **ConfirmationEmailTemplate.html** - Email verification
- **ForgotPasswordEmailTemplate.html** - Password reset link
- **TemporaryPasswordEmailNotification.html** - Temporary password delivery
- **SubscriptionAboutToExpireTemplate.html** - Subscription expiration warning

### Customizing Email Templates

Edit HTML templates in the `Templates/` directory. Templates support variable substitution:
- `{UserName}` - User's full name
- `{ConfirmationLink}` - Email confirmation link
- `{ResetLink}` - Password reset link
- `{TemporaryPassword}` - Generated temporary password
- `{ExpirationDate}` - Subscription expiration date

## Deployment

### Azure Container Apps (Recommended)

```bash
# Build and push to container registry
az acr build --registry your-registry --image cbrain-user-service:latest .

# Deploy to Container Apps
az containerapp create \
  --name user-service \
  --resource-group your-rg \
  --image your-registry.azurecr.io/cbrain-user-service:latest \
  --environment your-container-env \
  --target-port 8080 \
  --env-vars ASPNETCORE_ENVIRONMENT=Production \
               USERS_DB_CONNECTION_STRING="..." \
               BREVO_API_KEY="..."
```

### Kubernetes Deployment

```bash
# Apply Kubernetes manifests
kubectl apply -f kubernetes/

# Check deployment status
kubectl get deployments -n cbrain
kubectl get pods -n cbrain
kubectl logs -f deployment/user-service -n cbrain
```

### Environment Variables for Deployment

| Variable | Example | Required |
|----------|---------|----------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Yes |
| `USERS_DB_CONNECTION_STRING` | `Host=YourHostname;...` | Yes |
| `BREVO_API_KEY` | `api_key_here` | Yes |
| `EXTERNAL_AUTH_SECRET_KEY` | `secret_key` | Conditional |
| `JwtSettings__Issuer` | `https://issuer-url` | Yes |
| `JwtSettings__Audience` | `audience-name` | Yes |

## Contributing

We welcome contributions from the community, research collaborators, and industry partners. This section outlines the contribution process.

### Getting Started

1. **Fork the Repository**

2. **Create a Feature Branch**
   ```bash
   git checkout -b feature/your-feature-name
   # or for bug fixes
   git checkout -b bugfix/your-bug-fix
   ```

3. **Make Changes Following Project Standards**
   - See [Development Practices](#development-practices) section

4. **Commit with Clear Messages**
   ```bash
   git commit -m "feat: add user suspension feature

   - Implement user suspension logic
   - Add suspension date tracking
   - Update validation logic
      ```

5. **Push and Create Pull Request**
   ```bash
   git push origin feature/your-feature-name
   ```

## Development Practices

### Coding Standards

- **Language**: C# 12.0+ features
- **Naming**: PascalCase for classes, camelCase for fields/properties
- **Async/Await**: Always use async for I/O operations
- **Null Safety**: Enable nullable reference types (`#nullable enable`)
- **Error Handling**: Use specific exception types with meaningful messages

### Project Organization

```csharp
// Controllers - Handle HTTP requests/responses
[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase { }

// Services - Business logic
public interface IUserService { }
public class UserService : IUserService { }

// Models - Data structures
public class UserDTO { }
public class ApplicationUser { }

// DbContext - Data access
public class ApplicationDbContext : DbContext { }
```

### Security Considerations

- Never log sensitive data (passwords, tokens, API keys)
- Validate all input from users
- Use parameterized queries (EF Core does this automatically)
- Implement rate limiting for authentication endpoints
- Keep dependencies updated

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.