# NutriCare Pro Backend - Complete Implementation Guide

## 📋 Project Summary

**Solution Name:** NutriCare.sln  
**Target Framework:** .NET 9.0  
**Architecture Pattern:** Clean Architecture  
**Design Patterns:** CQRS, Repository, Unit of Work, MediatR  
**Database:** SQL Server 2022+  
**Status:** ✅ **COMPLETE - Ready for Development**

---

## 🎯 Implementation Checklist

### ✅ Completed Components

#### Domain Layer (NutriCare.Domain)
- ✅ BaseEntity.cs - Audit properties (CreatedAt, UpdatedAt, DeletedAt)
- ✅ DomainEntities.cs - 14 entities with complete properties
- ✅ DomainEnums.cs - Role types, appointment status, payment status
- ✅ DomainExceptions.cs - 8 custom exception types
- ✅ DomainInterfaces.cs - Service contracts

#### Application Layer (NutriCare.Application)
- ✅ Commands:
  - LoginCommand, RegisterCommand, RefreshTokenCommand, LogoutCommand
  - CreateAppointmentCommand, UpdateAppointmentCommand, CancelAppointmentCommand, CompleteAppointmentCommand
- ✅ Queries:
  - GetUserByIdQuery, GetUserByEmailQuery, GetAllUsersQuery, VerifyTokenQuery
  - GetAppointmentByIdQuery, GetPatientAppointmentsQuery, GetNutritionistAppointmentsQuery, GetAvailableSlotsQuery
- ✅ Handlers: 8 handlers implementing CQRS commands and queries
- ✅ Validators: 4 FluentValidation validators for commands
- ✅ DTOs: 20+ data transfer objects
- ✅ MappingProfile.cs - AutoMapper configuration for entity conversions

#### Infrastructure Layer (NutriCare.Infrastructure)
- ✅ JwtTokenService.cs - JWT generation, validation, refresh tokens
- ✅ PasswordService.cs - BCrypt password hashing
- ✅ CurrentUserService.cs - HTTP context user extraction
- ✅ EmailService.cs - Email service stub (ready for SendGrid/SMTP)

#### Persistence Layer (NutriCare.Persistence)
- ✅ NutriCareDbContext.cs - Entity Framework DbContext with 14 DbSets
- ✅ Repository.cs - Generic repository pattern implementation
- ✅ UnitOfWork.cs - Transaction management

#### API Layer (NutriCare.API)
- ✅ AuthController.cs - Authentication endpoints (login, register, refresh, verify)
- ✅ AppointmentsController.cs - Appointment endpoints
- ✅ PatientsController.cs - Stub for patient management
- ✅ NutritionistsController.cs - Stub for nutritionist management
- ✅ DietPlansController.cs - Stub for diet plan management
- ✅ PaymentsController.cs - Stub for payment processing
- ✅ GlobalExceptionHandlingMiddleware.cs - Exception handling
- ✅ ServiceCollectionExtensions.cs - Dependency injection configuration
- ✅ ChatHub.cs & NotificationHub.cs - SignalR real-time communication
- ✅ Program.cs - Application startup and configuration
- ✅ appsettings.json - Configuration with JWT, database, SMTP
- ✅ appsettings.Development.json - Development-specific settings

#### Configuration Files
- ✅ .gitignore - Git ignore patterns
- ✅ BACKEND_SETUP_COMPLETE.md - Comprehensive setup documentation
- ✅ README.md - Quick start guide
- ✅ IMPLEMENTATION_GUIDE.md - This file

---

## 📁 File Structure Reference

```
NutriCare.sln
├── NutriCare.Domain/
│   ├── BaseEntity.cs                    [✅ Complete]
│   ├── DomainEntities.cs               [✅ Complete - 14 entities]
│   ├── DomainEnums.cs                  [✅ Complete - 8 enum types]
│   ├── DomainExceptions.cs             [✅ Complete - 8 exception types]
│   ├── DomainInterfaces.cs             [✅ Complete - 6 interfaces]
│   └── NutriCare.Domain.csproj
│
├── NutriCare.Application/
│   ├── Commands/
│   │   ├── AuthenticationCommands.cs    [✅ Complete - 4 commands]
│   │   └── AppointmentCommands.cs       [✅ Complete - 4 commands]
│   ├── Queries/
│   │   ├── AuthenticationQueries.cs     [✅ Complete - 4 queries]
│   │   └── AppointmentQueries.cs        [✅ Complete - 4 queries]
│   ├── Handlers/
│   │   ├── AuthenticationHandlers.cs    [✅ Complete - 4 handlers]
│   │   └── AppointmentHandlers.cs       [✅ Complete - 4 handlers]
│   ├── Validators/
│   │   └── CommandValidators.cs         [✅ Complete - 4 validators]
│   ├── DTOs/
│   │   └── ApplicationDtos.cs           [✅ Complete - 20+ DTOs]
│   ├── Mappings/
│   │   └── MappingProfile.cs            [✅ Complete - 10+ mappings]
│   └── NutriCare.Application.csproj
│
├── NutriCare.Infrastructure/
│   ├── Authentication/
│   │   ├── JwtTokenService.cs           [✅ Complete]
│   │   ├── PasswordService.cs           [✅ Complete]
│   │   └── CurrentUserService.cs        [✅ Complete]
│   ├── Email/
│   │   └── EmailService.cs              [✅ Complete - Stub]
│   └── NutriCare.Infrastructure.csproj
│
├── NutriCare.Persistence/
│   ├── Contexts/
│   │   └── NutriCareDbContext.cs        [✅ Complete]
│   ├── Repositories/
│   │   ├── Repository.cs                [✅ Complete]
│   │   └── UnitOfWork.cs                [✅ Complete]
│   └── NutriCare.Persistence.csproj
│
├── NutriCare.API/
│   ├── Controllers/
│   │   ├── AuthController.cs            [✅ Complete]
│   │   ├── AppointmentsController.cs    [✅ Complete]
│   │   └── AdditionalControllers.cs     [✅ Complete - Stubs]
│   ├── Middleware/
│   │   └── GlobalExceptionHandlingMiddleware.cs [✅ Complete]
│   ├── Extensions/
│   │   └── ServiceCollectionExtensions.cs [✅ Complete]
│   ├── Hubs/
│   │   └── CommunicationHubs.cs         [✅ Complete - Chat & Notifications]
│   ├── Program.cs                       [✅ Complete]
│   ├── appsettings.json                 [✅ Complete]
│   ├── appsettings.Development.json     [✅ Complete]
│   └── NutriCare.API.csproj
│
├── .gitignore                            [✅ Complete]
├── README.md                             [✅ Complete]
└── BACKEND_SETUP_COMPLETE.md            [✅ Complete]
```

---

## 🔧 Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Framework | ASP.NET Core | 9.0 |
| Database | SQL Server | 2022+ |
| ORM | Entity Framework Core | 9.0.0 |
| CQRS | MediatR | 12.2.0 |
| Validation | FluentValidation | 11.9.1 |
| Mapping | AutoMapper | 12.0.1 |
| Authentication | JWT Bearer | 9.0.0 |
| Password Hashing | BCrypt.Net-Next | 4.0.3 |
| Logging | Serilog | 8.1.0 |
| API Documentation | Swashbuckle | 6.4.6 |
| Real-time | SignalR | 1.1.0 |

---

## 🚀 API Endpoints Overview

### Authentication
```
POST   /api/auth/login              - User login
POST   /api/auth/register           - User registration
POST   /api/auth/refresh-token      - Refresh access token
GET    /api/auth/verify             - Verify JWT token
```

### Appointments
```
POST   /api/appointments            - Create appointment
GET    /api/appointments/{id}       - Get appointment by ID
GET    /api/appointments/patient/{patientId}  - Get patient appointments
DELETE /api/appointments/{id}/cancel - Cancel appointment
```

### Patients (Stubs - to be completed)
```
GET    /api/patients/{id}           - Get patient profile
PUT    /api/patients/{id}           - Update patient profile
GET    /api/patients/{id}/weight-history - Get weight tracking history
```

### Nutritionists (Stubs - to be completed)
```
GET    /api/nutritionists           - Get all nutritionists
GET    /api/nutritionists/{id}      - Get nutritionist profile
GET    /api/nutritionists/{id}/available-slots - Get available appointment slots
GET    /api/nutritionists/{id}/reviews - Get nutritionist reviews
```

### Diet Plans (Stubs - to be completed)
```
GET    /api/diet-plans/{id}         - Get diet plan
GET    /api/diet-plans/patient/{patientId} - Get patient's diet plans
```

### Payments (Stubs - to be completed)
```
POST   /api/payments/process        - Process payment
GET    /api/payments/patient/{patientId} - Get patient payment history
```

### Health & System
```
GET    /api/health                  - Health check
GET    /swagger                     - API documentation
```

### Real-time Communication (SignalR)
```
ws://localhost:7001/hubs/chat              - Chat Hub
ws://localhost:7001/hubs/notifications     - Notification Hub
```

---

## 🔐 Authentication Flow

### 1. **Registration**
```
POST /api/auth/register
↓
RegisterCommandHandler validates input
↓
Check if email exists (duplicate prevention)
↓
Hash password with BCrypt
↓
Create User entity
↓
Save to database via Repository & UnitOfWork
↓
Return UserDto with created user info
```

### 2. **Login**
```
POST /api/auth/login (email + password)
↓
LoginCommandHandler validates credentials
↓
Retrieve user by email
↓
Verify password hash with BCrypt
↓
Generate JWT access token (15 min expiry)
↓
Generate refresh token (7 days expiry)
↓
Save refresh token to database
↓
Return LoginResponse with both tokens & UserDto
```

### 3. **Token Refresh**
```
POST /api/auth/refresh-token (refresh token)
↓
RefreshTokenCommandHandler validates token
↓
Check if refresh token exists in database
↓
Check if refresh token not expired
↓
Generate new access token
↓
Generate new refresh token
↓
Update tokens in database
↓
Return RefreshTokenResponse with new tokens
```

### 4. **Protected Request**
```
GET /api/appointments/1 (with Authorization: Bearer <access_token>)
↓
JWT Bearer middleware validates token
↓
Extract claims: uid (user ID), email, role
↓
Set HttpContext.User with claims
↓
Controller accesses current user via ICurrentUserService
↓
Handler executes with current user context
↓
Return response
```

---

## 🏛️ CQRS Implementation Pattern

### Command Pattern (Write Operations)
```csharp
// 1. Define Command
public class CreateAppointmentCommand : IRequest<AppointmentDto>
{
    public long PatientId { get; set; }
    public long NutritionistId { get; set; }
    // ... other properties
}

// 2. Define Validator
public class CreateAppointmentCommandValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentCommandValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.NutritionistId).GreaterThan(0);
        // ... other rules
    }
}

// 3. Implement Handler
public class CreateAppointmentCommandHandler : IRequestHandler<CreateAppointmentCommand, AppointmentDto>
{
    private readonly IRepository<Appointment> _repository;
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<AppointmentDto> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        // Validate business rules
        // Create entity
        // Save to database
        // Return DTO
    }
}

// 4. Register in DI
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(...));

// 5. Use in Controller
[HttpPost]
public async Task<ActionResult<AppointmentDto>> CreateAppointment(CreateAppointmentRequest request)
{
    var command = new CreateAppointmentCommand { /* map from request */ };
    var result = await _mediator.Send(command);
    return CreatedAtAction(nameof(GetAppointment), new { id = result.Id }, result);
}
```

### Query Pattern (Read Operations)
```csharp
// 1. Define Query
public class GetAppointmentByIdQuery : IRequest<AppointmentDto>
{
    public long AppointmentId { get; set; }
}

// 2. Implement Handler
public class GetAppointmentByIdQueryHandler : IRequestHandler<GetAppointmentByIdQuery, AppointmentDto>
{
    private readonly IRepository<Appointment> _repository;
    private readonly IMapper _mapper;
    
    public async Task<AppointmentDto> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
    {
        var appointment = await _repository.GetByIdAsync(request.AppointmentId);
        if (appointment == null)
            throw new EntityNotFoundException(nameof(Appointment), request.AppointmentId);
        
        return _mapper.Map<AppointmentDto>(appointment);
    }
}

// 3. Use in Controller
[HttpGet("{id}")]
public async Task<ActionResult<AppointmentDto>> GetAppointment(long id)
{
    var query = new GetAppointmentByIdQuery { AppointmentId = id };
    var result = await _mediator.Send(query);
    return Ok(result);
}
```

---

## 🗄️ Database Schema Overview

### Core Tables (19 total)

| Entity | Purpose | Key Relationships |
|--------|---------|-------------------|
| **Roles** | System roles | ← Users (1:Many) |
| **Users** | User accounts | → Role (Many:1), → Patient (1:1), → Nutritionist (1:1) |
| **Patients** | Patient profiles | ← Appointments, ← DietPlans, ← WeightTracking, ← Payments |
| **Nutritionists** | Nutritionist profiles | ← Appointments, ← DietPlans, ← Reviews, ← Payments |
| **Appointments** | Consultation appointments | → Patient, → Nutritionist, → VideoConsultation |
| **VideoConsultations** | Video session details | ← Appointment (1:1) |
| **DietPlans** | Nutrition plans | → Patient, → Nutritionist, → DietPlanItems |
| **DietPlanItems** | Daily meals in plan | ← DietPlan |
| **WeightTracking** | Weight measurements | → Patient |
| **Payments** | Transaction records | → Patient, → Nutritionist |
| **Messages** | User messages | → Sender, → Receiver |
| **Notifications** | System notifications | → User |
| **Reviews** | Patient reviews | → Nutritionist, → Patient, → Appointment |
| **AuditLogs** | System audit trail | → User |

---

## 💾 Data Access Pattern

### Repository Pattern
```csharp
// Interface (Domain Layer)
public interface IRepository<T>
{
    Task<T?> GetByIdAsync(long id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> GetAsync(Func<IQueryable<T>, IQueryable<T>>? predicate = null);
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(long id);
    Task<bool> ExistsAsync(long id);
}

// Implementation (Persistence Layer)
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly NutriCareDbContext _context;
    protected readonly DbSet<T> _dbSet;

    // Implementation uses EF Core under the hood
    public async Task<T?> GetByIdAsync(long id) => await _dbSet.FindAsync(id);
    
    public async Task<IEnumerable<T>> GetAsync(Func<IQueryable<T>, IQueryable<T>>? predicate = null)
    {
        IQueryable<T> query = _dbSet;
        if (predicate != null) query = predicate(query);
        return await query.ToListAsync();
    }
}

// Usage in Handler
public async Task<AppointmentDto> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
{
    var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
}

// Usage with predicates
var appointments = await _appointmentRepository.GetAsync(q => 
    q.Where(a => a.PatientId == patientId && a.Status == AppointmentStatus.Scheduled)
     .OrderByDescending(a => a.AppointmentDateTime));
```

### Unit of Work Pattern
```csharp
// Transaction handling
using (var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>())
{
    try
    {
        await unitOfWork.BeginTransactionAsync();
        
        // Multiple operations
        await appointmentRepository.AddAsync(appointment);
        await paymentRepository.AddAsync(payment);
        
        await unitOfWork.CommitAsync();
    }
    catch (Exception)
    {
        await unitOfWork.RollbackAsync();
        throw;
    }
}
```

---

## 📊 Response Format

All API responses follow a consistent wrapper format:

### Success Response
```json
{
  "success": true,
  "message": "Operation completed successfully",
  "data": {
    "id": 1,
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe"
  },
  "errors": {},
  "statusCode": 200
}
```

### Error Response
```json
{
  "success": false,
  "message": "Operation failed",
  "data": null,
  "errors": {
    "email": ["Email is required"],
    "password": ["Password must be at least 8 characters"]
  },
  "statusCode": 400
}
```

### Implementation
```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T? Data { get; set; }
    public Dictionary<string, string[]> Errors { get; set; }
    public int StatusCode { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Success", int statusCode = 200)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            StatusCode = statusCode
        };
    }

    public static ApiResponse<T> FailureResponse(string message, int statusCode = 400)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            StatusCode = statusCode
        };
    }
}
```

---

## 🔄 Dependency Injection Setup

All services are registered in `ServiceCollectionExtensions.cs`:

```csharp
// Database
services.AddDbContext<NutriCareDbContext>();

// Repositories & Unit of Work
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
services.AddScoped<IUnitOfWork, UnitOfWork>();

// Authentication
services.AddScoped<IJwtTokenService, JwtTokenService>();
services.AddScoped<IPasswordService, PasswordService>();
services.AddScoped<ICurrentUserService, CurrentUserService>();
services.AddScoped<IEmailService, EmailService>();

// AutoMapper
services.AddAutoMapper(typeof(MappingProfile));

// MediatR
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(...));

// Validation
services.AddValidatorsFromAssemblyContaining<LoginCommandValidator>();

// HTTP Context
services.AddHttpContextAccessor();
```

---

## 🧪 Testing Strategy (Future)

### Unit Test Template
```csharp
[TestFixture]
public class CreateAppointmentCommandHandlerTests
{
    private Mock<IRepository<Appointment>> _appointmentRepositoryMock;
    private Mock<IRepository<Patient>> _patientRepositoryMock;
    private Mock<IUnitOfWork> _unitOfWorkMock;
    private CreateAppointmentCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _appointmentRepositoryMock = new Mock<IRepository<Appointment>>();
        _patientRepositoryMock = new Mock<IRepository<Patient>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        
        _handler = new CreateAppointmentCommandHandler(
            _appointmentRepositoryMock.Object,
            _patientRepositoryMock.Object,
            _unitOfWorkMock.Object);
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
        _appointmentRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Appointment>()), Times.Once);
    }

    [Test]
    [ExpectedException(typeof(EntityNotFoundException))]
    public async Task Handle_WithInvalidPatient_ThrowsException()
    {
        // Arrange
        _patientRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<long>()))
            .ReturnsAsync((Patient)null!);
        
        var command = new CreateAppointmentCommand
        {
            PatientId = 999,
            NutritionistId = 1,
            AppointmentDateTime = DateTime.UtcNow.AddDays(1)
        };

        // Act & Assert
        await _handler.Handle(command, CancellationToken.None);
    }
}
```

---

## 📝 Next Implementation Steps

### High Priority (Core Features)
1. ⬜ Implement remaining commands for Patient, DietPlan, Payment
2. ⬜ Implement remaining queries for all entities
3. ⬜ Create remaining handlers
4. ⬜ Create remaining validators
5. ⬜ Complete AdditionalControllers.cs stubs

### Medium Priority (Integration)
1. ⬜ Implement Stripe payment integration
2. ⬜ Implement SendGrid email service
3. ⬜ Setup Azure Table Storage for file storage
4. ⬜ Implement Twilio SMS notifications
5. ⬜ Complete all SignalR Hub functionality

### Lower Priority (Enhancement)
1. ⬜ Add caching with Redis
2. ⬜ Implement rate limiting
3. ⬜ Add API versioning
4. ⬜ Implement background jobs with Hangfire
5. ⬜ Add comprehensive logging/monitoring

---

## 🎓 Learning Resources

- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [CQRS Pattern](https://www.martinfowler.com/bliki/CQRS.html)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [Entity Framework Core](https://learn.microsoft.com/ef/core/)
- [JWT Authentication](https://jwt.io/introduction)
- [ASP.NET Core Security](https://learn.microsoft.com/aspnet/core/security/)

---

**Document Version:** 1.0  
**Last Updated:** June 2, 2026  
**Status:** ✅ Complete Backend Implementation Guide
