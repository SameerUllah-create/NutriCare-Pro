# NutriCare Pro - Enterprise Architecture Overview

## Executive Summary

NutriCare Pro is a comprehensive nutrition consultation platform built on a clean, enterprise-grade architecture designed for scalability, maintainability, and microservice readiness.

---

## Architecture Pattern: Clean Architecture with DDD

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│        (Web API + SignalR Hubs + API Controllers)           │
│                   NutriCare.API                             │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────┴────────────────────────────────────────┐
│              Application Layer                               │
│  (Services, DTOs, Validators, Mappings, Use Cases)          │
│                  NutriCare.Application                       │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────┴────────────────────────────────────────┐
│                 Domain Layer (Core)                          │
│  (Entities, Interfaces, Enums, Value Objects, Rules)        │
│                   NutriCare.Core                            │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────┴────────────────────────────────────────┐
│             Infrastructure Layer                             │
│   (External Services, Auth, Storage, Notifications)         │
│                NutriCare.Infrastructure                     │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────┴────────────────────────────────────────┐
│              Persistence Layer                               │
│    (EF Core, DbContext, Repositories, Migrations)           │
│                  NutriCare.Persistence                      │
└─────────────────────────────────────────────────────────────┘
```

---

## Layer Responsibilities

### 1. **NutriCare.Core** (Domain Layer)
- **Purpose:** Contains business logic and domain rules
- **Contents:**
  - Domain Entities (User, Patient, Nutritionist, Appointment, etc.)
  - Value Objects
  - Domain Interfaces
  - Enums (UserRole, AppointmentStatus, etc.)
  - Constants
  - Business Rules and Specifications
- **Dependencies:** None (Independent)
- **Key Principle:** No external dependencies

### 2. **NutriCare.Application** (Application/Business Logic Layer)
- **Purpose:** Orchestrates application logic and workflows
- **Contents:**
  - Application Services
  - DTOs (Data Transfer Objects)
  - Validators (FluentValidation)
  - AutoMapper Profiles
  - Use Cases / Commands / Queries (CQRS ready)
  - Exceptions
- **Dependencies:** NutriCare.Core only
- **Key Principle:** Implements use cases, doesn't know about UI or database

### 3. **NutriCare.Infrastructure** (Infrastructure Layer)
- **Purpose:** Implements external integrations and services
- **Contents:**
  - Azure Blob Storage implementation
  - JWT Authentication service
  - Payment gateway integration (Stripe, JazzCash, EasyPaisa)
  - Email service (SendGrid)
  - SMS service (Twilio)
  - SignalR hub implementations
  - File upload handlers
- **Dependencies:** NutriCare.Core
- **Key Principle:** Abstracts external dependencies

### 4. **NutriCare.Persistence** (Data Access Layer)
- **Purpose:** Manages data persistence
- **Contents:**
  - DbContext
  - Entity Configurations (Fluent API)
  - Repository Implementations
  - Migrations
  - Database Seeds
  - Unit of Work Pattern
- **Dependencies:** NutriCare.Core, Entity Framework Core
- **Key Principle:** Implements data access abstraction

### 5. **NutriCare.API** (Presentation Layer)
- **Purpose:** Exposes API endpoints and real-time communication
- **Contents:**
  - API Controllers (REST endpoints)
  - SignalR Hubs (Real-time communication)
  - Middleware (Auth, Error handling, CORS, Logging)
  - Extensions (Dependency Injection, Configuration)
  - Startup configuration
- **Dependencies:** All other layers
- **Key Principle:** Handles HTTP requests and real-time communication

---

## Key Architectural Patterns

### 1. **Repository Pattern**
- Abstracts data access logic
- Enables unit testing
- Supports multiple data sources

### 2. **Unit of Work Pattern**
- Manages transactions across multiple repositories
- Ensures data consistency
- Implements IUnitOfWork interface

### 3. **Dependency Injection**
- Loose coupling between layers
- Better testability
- Configured in Program.cs

### 4. **Data Transfer Objects (DTOs)**
- Decouples API from domain models
- Improves security and performance
- Enables selective data exposure

### 5. **AutoMapper**
- Automatic DTO ↔ Entity mapping
- Reduces boilerplate code
- Centralized mapping configuration

### 6. **Specification Pattern** (Optional)
- Encapsulates domain logic
- Reusable across services
- Supports complex queries

### 7. **SignalR for Real-time**
- Chat functionality
- Live notifications
- Appointment updates
- Progress tracking updates

---

## Cross-Cutting Concerns

### Authentication & Authorization
- **JWT Tokens:** Short-lived access tokens (15-30 minutes)
- **Refresh Tokens:** Long-lived tokens (7-30 days)
- **Role-Based Access Control:** Patient, Nutritionist, Admin
- **Claims-Based Authorization:** Fine-grained permissions

### Error Handling
- Global exception middleware
- Standardized error responses
- Custom exceptions for business logic

### Logging
- Structured logging with Serilog
- Request/Response logging
- Performance monitoring
- Error tracking

### Validation
- FluentValidation for request DTOs
- Domain-level validation in entities
- Custom validation rules

### CORS & Security
- Configurable CORS policy
- HTTPS enforcement
- Rate limiting
- SQL Injection prevention via EF Core

---

## Technology Stack Details

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Framework** | ASP.NET Core 9 | Web API Framework |
| **ORM** | Entity Framework Core | Data Access |
| **Database** | SQL Server | Primary Data Store |
| **Authentication** | JWT | Token-based Auth |
| **Real-time** | SignalR | WebSocket Communication |
| **Storage** | Azure Blob Storage | File Management |
| **Email** | SendGrid | Email Notifications |
| **SMS** | Twilio | SMS Notifications |
| **Payment** | Stripe, JazzCash, EasyPaisa | Payment Processing |
| **Deployment** | Azure App Service | Hosting |
| **Frontend** | Next.js 15 | Web UI |
| **Styling** | Tailwind CSS | CSS Framework |
| **UI Components** | Shadcn UI | Reusable Components |

---

## Module Breakdown

### 1. **Authentication & User Management Module**
- User registration/login
- JWT token generation/refresh
- Role assignment
- Password reset
- User profile management

### 2. **Appointment Management Module**
- Appointment booking
- Appointment scheduling
- Appointment status tracking
- Appointment reminders
- Cancellation handling

### 3. **Diet Plan Module**
- Create diet plans
- Assign diet plans to patients
- Update diet plans
- Download diet plans
- Track adherence

### 4. **Medical Reports Module**
- Upload medical reports
- Store in Azure Blob Storage
- Generate medical history
- Download reports
- Share with nutritionist

### 5. **Payment Module**
- Process payments via Stripe
- Process payments via JazzCash
- Process payments via EasyPaisa
- Track payment history
- Generate invoices

### 6. **Progress Tracking Module**
- Record patient weight/measurements
- Track progress metrics
- Generate progress reports
- Visualize progress

### 7. **Chat & Communication Module**
- Real-time chat (SignalR)
- Chat history
- Notifications for new messages
- File sharing in chat

### 8. **Video Consultation Module**
- Schedule video calls
- Record consultations
- Store recordings
- Share consultation notes

### 9. **Blog Management Module**
- Create blog posts
- Publish/unpublish
- Category management
- Comment system
- Like/share functionality

### 10. **Notification Module**
- Email notifications (SendGrid)
- SMS notifications (Twilio)
- Push notifications
- Notification preferences
- Notification history

### 11. **Admin Dashboard Module**
- User management
- System monitoring
- Report generation
- Configuration management

---

## Microservice Readiness Plan

### Phase 1: Monolithic (Current)
- Single ASP.NET Core application
- Shared database
- All modules in one codebase

### Phase 2: Modular Monolith
- Separate class libraries per module
- Clear module boundaries
- Event-driven communication between modules
- Prepared for extraction

### Phase 3: Microservices (Future)
Potential services:
- **Authentication Service**
- **User Service**
- **Appointment Service**
- **Diet Plan Service**
- **Payment Service**
- **Notification Service**
- **Chat Service**
- **File Storage Service**
- **Reporting Service**
- **Blog Service**

### Communication Strategy
- REST APIs for synchronous calls
- Message Queue (Azure Service Bus) for async
- gRPC for service-to-service (internal)
- Event Bus for domain events

---

## Security Considerations

1. **Authentication:** JWT with refresh tokens
2. **Authorization:** Role-based + claim-based
3. **Data Protection:** Encryption at rest and in transit
4. **API Security:** Rate limiting, HTTPS
5. **Input Validation:** Comprehensive validation
6. **SQL Injection:** EF Core parameterized queries
7. **CORS:** Whitelist trusted domains
8. **Sensitive Data:** Never log passwords/tokens
9. **Database:** Principle of least privilege

---

## Performance Optimization

1. **Caching:**
   - Response caching for GET requests
   - Distributed caching (Redis) for frequently accessed data
   - Cache invalidation strategy

2. **Database:**
   - Indexing on frequently queried columns
   - Query optimization
   - Connection pooling

3. **API:**
   - Pagination for large datasets
   - Compression (gzip)
   - Async/await throughout

4. **Frontend:**
   - Code splitting
   - Image optimization
   - Lazy loading

---

## Deployment Architecture

```
┌─────────────────┐
│  Azure DevOps   │ (CI/CD Pipeline)
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────┐
│   Azure Container Registry          │ (Docker Images)
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│   Azure App Service / AKS           │ (Hosting)
├─────────────────────────────────────┤
│  - Backend API                      │
│  - Frontend (Static Hosting)        │
│  - SignalR Service                  │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│   Azure SQL Database                │ (Data)
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│   Azure Blob Storage                │ (Files)
└─────────────────────────────────────┘
```

---

## Development Workflow

1. **Feature Development:**
   - Create feature branch from develop
   - Implement in respective layer
   - Write unit tests
   - Maintain clean architecture principles

2. **Code Review:**
   - Pull request with detailed description
   - Architecture validation
   - Code quality check
   - Test coverage verification

3. **Testing Strategy:**
   - Unit Tests (xUnit)
   - Integration Tests
   - API Tests
   - End-to-End Tests

4. **Deployment:**
   - Dev environment → Staging → Production
   - Database migrations handled automatically
   - Rollback strategy in place

---

## Scalability Plan

1. **Database:** Partitioning for large tables (Appointments, Chat history)
2. **Caching:** Implement Redis for session data
3. **Storage:** Azure Blob Storage handles file scaling
4. **API:** Horizontal scaling with load balancing
5. **SignalR:** Use Azure SignalR Service for scaling
6. **Message Queue:** Azure Service Bus for async processing

---

## Next Steps

1. Create database schema and relationships
2. Setup Entity Framework Core models
3. Implement repository pattern
4. Configure dependency injection
5. Create API endpoints
6. Implement authentication
7. Implement real-time features
8. Deploy to Azure

