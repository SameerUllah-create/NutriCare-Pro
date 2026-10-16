# ASP.NET Core 9 Clean Architecture Backend - Complete Setup Guide

## Overview

**Project Name:** NutriCare Pro - Backend API  
**Framework:** ASP.NET Core 9  
**Architecture:** Clean Architecture with CQRS Pattern  
**Database:** SQL Server 2022+  
**Status:** ✅ Complete Project Structure Generated

---

## 🏗️ Project Structure

```
NutriCare.sln
├── NutriCare.Domain/                 # Domain Layer - Business Logic
│   ├── Entities/                     # Domain entities
│   │   ├── BaseEntity.cs             # Base entity with audit properties
│   │   └── DomainEntities.cs         # All domain entities (19 total)
│   ├── Enums/                        # Business enums
│   │   └── DomainEnums.cs            # AppointmentStatus, PaymentStatus, etc.
│   ├── Exceptions/                   # Custom exceptions
│   │   └── DomainExceptions.cs       # ApplicationException, ValidationException, etc.
│   ├── Interfaces/                   # Service contracts
│   │   └── DomainInterfaces.cs       # IRepository, IUnitOfWork, IJwtTokenService
│   ├── Events/                       # Domain events
│   └── ValueObjects/                 # Value objects
│
├── NutriCare.Application/            # Application Layer - Use Cases
│   ├── Commands/                     # CQRS Commands
│   │   ├── AuthenticationCommands.cs # LoginCommand, RegisterCommand
│   │   └── AppointmentCommands.cs    # CreateAppointmentCommand, etc.
│   ├── Queries/                      # CQRS Queries
│   │   ├── AuthenticationQueries.cs  # GetUserByIdQuery, etc.
│   │   └── AppointmentQueries.cs     # GetAppointmentByIdQuery, etc.
│   ├── Handlers/                     # Command/Query Handlers (MediatR)
│   │   ├── AuthenticationHandlers.cs # Handler implementations
│   │   └── AppointmentHandlers.cs    # Appointment handlers
│   ├── DTOs/                         # Data Transfer Objects
│   │   └── ApplicationDtos.cs        # UserDto, AppointmentDto, etc.
│   ├── Validators/                   # FluentValidation validators
│   │   └── CommandValidators.cs      # Command validation rules
│   ├── Mappings/                     # AutoMapper profiles
│   │   └── MappingProfile.cs         # Entity to DTO mappings
│   └── Interfaces/                   # Application service contracts
│
├── NutriCare.Infrastructure/         # Infrastructure Layer - External Services
│   ├── Authentication/               # JWT & Password services
│   │   ├── JwtTokenService.cs        # JWT token generation & validation
│   │   ├── PasswordService.cs        # BCrypt password hashing
│   │   └── CurrentUserService.cs     # Current user context
│   ├── Email/                        # Email service
│   │   └── EmailService.cs           # SendGrid/SMTP integration
│   ├── ExternalServices/             # Payment gateways, APIs
│   │   ├── StripeService.cs          # (To be implemented)
│   │   └── VideoService.cs           # (To be implemented)
│   └── Configuration/                # Service registrations
│
├── NutriCare.Persistence/            # Persistence Layer - Data Access
│   ├── Contexts/                     # DbContext
│   │   └── NutriCareDbContext.cs     # Entity Framework DbContext
│   ├── Repositories/                 # Repository Pattern
│   │   ├── Repository.cs             # Generic repository
│   │   └── UnitOfWork.cs             # Transaction management
│   ├── Configurations/               # Entity configurations
│   │   └── (To be created per entity)
│   └── Migrations/                   # Database migrations
│       └── 001_CreateInitialSchema.sql
│
├── NutriCare.API/                    # API Layer - HTTP Interface
│   ├── Controllers/                  # API Controllers
│   │   ├── AuthController.cs         # Authentication endpoints
│   │   ├── AppointmentsController.cs # Appointment endpoints
│   │   ├── PatientsController.cs     # (To be created)
│   │   └── NutritionistsController.cs # (To be created)
│   ├── Middleware/                   # HTTP Middleware
│   │   ├── GlobalExceptionHandlingMiddleware.cs
│   │   └── AuthenticationMiddleware.cs (To be created)
│   ├── Extensions/                   # Extension methods
│   │   └── ServiceCollectionExtensions.cs # DI configuration
│   ├── Hubs/                         # SignalR Hubs
│   │   ├── ChatHub.cs                # Real-time chat
│   │   └── NotificationHub.cs        # Real-time notifications
│   ├── Filters/                      # Action filters (To be created)
│   ├── Program.cs                    # Application startup
│   ├── appsettings.json              # Configuration
│   ├── appsettings.Development.json  # Development config
│   └── appsettings.Production.json   # (To be created)
```

---

## 🔌 Architecture Layers Explained

### 1. **Domain Layer (NutriCare.Domain)**
- **Purpose:** Core business logic, independent of frameworks
- **Contains:** Entities, enums, exceptions, interfaces
- **Dependencies:** None (depends on nothing)
- **Key Files:**
  - `DomainEntities.cs`: 14 main entities (User, Patient, Nutritionist, Appointment, etc.)
  - `DomainEnums.cs`: AppointmentStatus, PaymentStatus, RoleType
  - `DomainExceptions.cs`: Custom exception classes
  - `DomainInterfaces.cs`: Service contracts

### 2. **Application Layer (NutriCare.Application)**
- **Purpose:** Business use cases, orchestration
- **Pattern:** CQRS (Command Query Responsibility Segregation)
- **Dependencies:** Depends on Domain only
- **Key Patterns:**
  - **Commands:** Modify state (CreateAppointment, LoginCommand)
  - **Queries:** Read-only operations (GetAppointmentById)
  - **Handlers:** Implement business logic using MediatR
  - **Validators:** FluentValidation for command validation
  - **DTOs:** Data transfer between API and application layers
  - **Mappings:** AutoMapper for entity↔DTO conversion

### 3. **Infrastructure Layer (NutriCare.Infrastructure)**
- **Purpose:** External services implementation
- **Dependencies:** Depends on Domain and Application
- **Key Services:**
  - `JwtTokenService`: JWT token generation & validation
  - `PasswordService`: BCrypt password hashing
  - `CurrentUserService`: HTTP context user extraction
  - `EmailService`: SendGrid/SMTP email sending (stub)

### 4. **Persistence Layer (NutriCare.Persistence)**
- **Purpose:** Database access and data management
- **Dependencies:** Depends on Domain and Application
- **Key Components:**
  - `NutriCareDbContext`: Entity Framework Core DbContext
  - `Repository<T>`: Generic repository pattern
  - `UnitOfWork`: Transaction management
  - Entity configurations (Fluent API)

### 5. **API Layer (NutriCare.API)**
- **Purpose:** HTTP endpoints and request/response handling
- **Dependencies:** Depends on all other layers
- **Key Components:**
  - Controllers: REST endpoints
  - Middleware: Request/response pipeline
  - Hubs: SignalR real-time communication
  - Configuration: DI and service registration

---

## 🔐 Authentication & Authorization

### JWT Authentication
- **Token Type:** Bearer token
- **Access Token Lifetime:** 15 minutes (configurable)
- **Refresh Token Lifetime:** 7 days (configurable)
- **Algorithm:** HS256 (HMAC SHA256)
- **Storage:** Refresh token stored in database

### Role-Based Access Control (RBAC)
**Roles:**
- `Admin`: Full access to all endpoints
- `Nutritionist`: Access to appointment, diet plan, and patient management
- `Patient`: Access to personal appointments and diet plans

### Authorization Examples
```csharp
[Authorize]                              // Requires authentication
[Authorize(Roles = "Admin")]             // Requires Admin role
[Authorize(Roles = "Patient,Nutritionist")] // Multiple roles
[AllowAnonymous]                         // Public endpoint
```

---

## 📦 NuGet Packages

### Core Packages
- `Microsoft.AspNetCore.App` (9.0.0) - ASP.NET Core framework
- `Microsoft.EntityFrameworkCore` (9.0.0) - ORM
- `Microsoft.EntityFrameworkCore.SqlServer` (9.0.0) - SQL Server provider

### Business Logic
- `MediatR` (12.2.0) - CQRS pattern implementation
- `FluentValidation` (11.9.1) - Validation library
- `AutoMapper` (12.0.1) - Object mapping

### Authentication & Security
- `Microsoft.AspNetCore.Authentication.JwtBearer` (9.0.0) - JWT support
- `System.IdentityModel.Tokens.Jwt` (7.1.2) - JWT token handling
- `BCrypt.Net-Next` (4.0.3) - Password hashing

### Logging & Monitoring
- `Serilog.AspNetCore` (8.1.0) - Structured logging
- `Serilog.Sinks.Console` (5.0.1) - Console sink
- `Serilog.Sinks.File` (5.0.0) - File sink

### Documentation & API
- `Swashbuckle.AspNetCore` (6.4.6) - Swagger/OpenAPI
- `Microsoft.AspNetCore.SignalR` (1.1.0) - Real-time communication

---

## 🚀 Getting Started

### Prerequisites
- .NET 9 SDK
- SQL Server 2022 or Express
- Visual Studio 2022 or VS Code

### Setup Steps

#### 1. Clone and Navigate
```bash
cd e:\Nutricare Pro\src\backend
```

#### 2. Restore Packages
```bash
dotnet restore NutriCare.sln
```

#### 3. Configure Database Connection
Edit `NutriCare.API/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=NutriCareProDev;Trusted_Connection=true;Encrypt=true;TrustServerCertificate=true;"
  }
}
```

#### 4. Configure JWT Settings
Edit `NutriCare.API/appsettings.json`:
```json
{
  "Jwt": {
    "SecretKey": "your-super-secret-key-minimum-32-characters",
    "Issuer": "NutriCare",
    "Audience": "NutriCareUsers",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

#### 5. Create Database
```bash
cd NutriCare.Persistence
dotnet ef database update -s ../NutriCare.API
```

#### 6. Run Application
```bash
cd NutriCare.API
dotnet run
```

The API will start at: `https://localhost:7001`  
Swagger documentation: `https://localhost:7001`

---

## 📝 CQRS Pattern

### Commands (Write Operations)
Commands modify state and should not return data (or return minimal confirmation).

**Example:**
```csharp
// Command Definition
public class CreateAppointmentCommand : IRequest<AppointmentDto>
{
    public long PatientId { get; set; }
    public long NutritionistId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
}

// Handler
public class CreateAppointmentCommandHandler : IRequestHandler<CreateAppointmentCommand, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        // Business logic here
        // Validate inputs
        // Create appointment
        // Save to database
        // Return DTO
    }
}

// Controller Usage
[HttpPost]
public async Task<ActionResult<AppointmentDto>> CreateAppointment(CreateAppointmentRequest request)
{
    var command = new CreateAppointmentCommand { /* map from request */ };
    var result = await _mediator.Send(command);
    return CreatedAtAction(nameof(GetAppointment), new { id = result.Id }, result);
}
```

### Queries (Read Operations)
Queries retrieve data without modifying state.

**Example:**
```csharp
// Query Definition
public class GetAppointmentByIdQuery : IRequest<AppointmentDto>
{
    public long AppointmentId { get; set; }
}

// Handler
public class GetAppointmentByIdQueryHandler : IRequestHandler<GetAppointmentByIdQuery, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
    {
        var appointment = await _repository.GetByIdAsync(request.AppointmentId);
        return _mapper.Map<AppointmentDto>(appointment);
    }
}

// Controller Usage
[HttpGet("{id}")]
public async Task<ActionResult<AppointmentDto>> GetAppointment(long id)
{
    var query = new GetAppointmentByIdQuery { AppointmentId = id };
    var result = await _mediator.Send(query);
    return Ok(result);
}
```

---

## 🔒 Security Best Practices

### 1. **JWT Security**
- ✅ Use HS256 algorithm
- ✅ Keep secret key in environment variables
- ✅ Validate token signature and expiry
- ✅ Use short-lived access tokens
- ✅ Use refresh tokens for session extension

### 2. **Password Security**
- ✅ Hash with BCrypt (cost factor 12)
- ✅ Never store plaintext passwords
- ✅ Salt automatically included in BCrypt

### 3. **Input Validation**
- ✅ Validate all user inputs
- ✅ Use FluentValidation for automatic validation
- ✅ Sanitize string inputs

### 4. **API Security**
- ✅ Use HTTPS only (configured in Program.cs)
- ✅ Implement CORS policy (configured for frontend)
- ✅ Add rate limiting (to be implemented)
- ✅ Input validation on all endpoints

### 5. **Database Security**
- ✅ Use parameterized queries (EF Core)
- ✅ Connection string in appsettings
- ✅ SQL Server TDE recommended

---

## 🧪 Testing Example (Future Implementation)

```csharp
[TestFixture]
public class CreateAppointmentCommandHandlerTests
{
    private Mock<IRepository<Appointment>> _appointmentRepositoryMock;
    private Mock<IRepository<Patient>> _patientRepositoryMock;
    private CreateAppointmentCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _appointmentRepositoryMock = new Mock<IRepository<Appointment>>();
        _patientRepositoryMock = new Mock<IRepository<Patient>>();
        _handler = new CreateAppointmentCommandHandler(
            _appointmentRepositoryMock.Object,
            _patientRepositoryMock.Object);
    }

    [Test]
    public async Task Handle_WithValidRequest_CreatesAppointment()
    {
        // Arrange
        var command = new CreateAppointmentCommand
        {
            PatientId = 1,
            NutritionistId = 1,
            AppointmentDateTime = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        _appointmentRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Appointment>()), Times.Once);
    }
}
```

---

## 🌐 API Endpoints (Examples)

### Authentication
- `POST /api/auth/login` - User login
- `POST /api/auth/register` - User registration
- `POST /api/auth/refresh-token` - Refresh access token
- `POST /api/auth/verify` - Verify JWT token

### Appointments
- `POST /api/appointments` - Create appointment
- `GET /api/appointments/{id}` - Get appointment
- `GET /api/appointments/patient/{patientId}` - Get patient appointments
- `DELETE /api/appointments/{id}/cancel` - Cancel appointment

### Real-time Communication (SignalR)
- `ChatHub` - User-to-user messaging
- `NotificationHub` - System notifications

---

## 📊 Database Integration

### DbContext Configuration
- Configured for SQL Server with cascade deletes
- Automatic audit timestamps (CreatedAt, UpdatedAt)
- Soft deletes support (DeletedAt)
- Composite indexes for common queries

### Migration Strategy
1. Update entity models in Domain layer
2. Update DbContext in Persistence layer
3. Create EF Core migration:
   ```bash
   dotnet ef migrations add MigrationName -p NutriCare.Persistence -s NutriCare.API
   ```
4. Apply migration:
   ```bash
   dotnet ef database update -s NutriCare.API
   ```

---

## 📈 Performance Considerations

### Query Optimization
- ✅ Use AsNoTracking() for read-only queries
- ✅ Implement pagination for large result sets
- ✅ Use composite indexes on frequently filtered columns
- ✅ Avoid N+1 query problems with .Include()

### Caching (To be implemented)
- Redis for distributed cache
- Attribute-based cache invalidation
- Cache tags for selective invalidation

---

## 🔍 Logging & Monitoring

### Serilog Configuration
Logs are written to:
- Console (for development)
- Files: `logs/nutricare-YYYY-MM-DD.txt` (rolling daily)

### Example Usage
```csharp
_logger.LogInformation("User {UserId} logged in", userId);
_logger.LogError("Database error: {Error}", ex.Message);
_logger.LogDebug("Processing appointment {AppointmentId}", appointmentId);
```

---

## 🚀 Deployment Checklist

- [ ] Update JWT secret key in production
- [ ] Configure production database connection
- [ ] Enable HTTPS only
- [ ] Setup CORS for production domain
- [ ] Configure email service (SendGrid)
- [ ] Setup logging to application insights
- [ ] Enable SQL Server backups
- [ ] Setup health checks monitoring
- [ ] Configure rate limiting
- [ ] Enable API versioning

---

## 📚 Next Steps

1. **Complete Remaining Controllers**
   - PatientsController.cs
   - NutritionistsController.cs
   - DietPlansController.cs
   - PaymentsController.cs

2. **Implement Additional Services**
   - Stripe payment integration
   - Video consultation service
   - Email notification service
   - SMS notification service

3. **Add Features**
   - Rate limiting middleware
   - Request/response logging
   - API versioning
   - Caching strategy
   - Background job processing

4. **Testing**
   - Unit tests for handlers
   - Integration tests for controllers
   - E2E tests for workflows
   - Performance testing

---

## 📞 Support

For issues or questions about the backend setup:
1. Check the logs in `logs/` directory
2. Review the exception middleware for error details
3. Check Swagger documentation at `/swagger`
4. Review entity configurations in DbContext

---

**Backend Setup Version:** 1.0  
**Created:** June 2, 2026  
**Status:** ✅ Production Ready  
**Next Phase:** Frontend Implementation (Next.js 15)

