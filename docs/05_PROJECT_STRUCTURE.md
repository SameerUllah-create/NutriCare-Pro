# NutriCare Pro - Complete Project Structure

## Root Directory Structure

```
Nutricare Pro/
├── backend/                          # ASP.NET Core Backend
├── frontend/                         # Next.js Frontend
├── docs/                            # Documentation
├── deployment/                      # Deployment configurations
└── README.md                        # Project overview
```

---

## Backend Structure

```
backend/
│
├── NutriCare.API/                   # Presentation Layer
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── UsersController.cs
│   │   ├── PatientsController.cs
│   │   ├── AppointmentsController.cs
│   │   ├── DietPlansController.cs
│   │   ├── ProgressTrackingController.cs
│   │   ├── PaymentsController.cs
│   │   ├── ChatController.cs
│   │   ├── BlogController.cs
│   │   ├── MedicalReportsController.cs
│   │   ├── NotificationsController.cs
│   │   ├── VideoConsultationController.cs
│   │   └── AdminController.cs
│   │
│   ├── Middleware/
│   │   ├── ErrorHandlingMiddleware.cs
│   │   ├── JwtAuthenticationMiddleware.cs
│   │   ├── RequestLoggingMiddleware.cs
│   │   ├── RateLimitingMiddleware.cs
│   │   ├── CorsMiddleware.cs
│   │   └── ExceptionHandlingMiddleware.cs
│   │
│   ├── Hubs/
│   │   ├── ChatHub.cs              # Real-time chat
│   │   ├── NotificationHub.cs      # Real-time notifications
│   │   └── VideoConsultationHub.cs # Video consultation signaling
│   │
│   ├── Extensions/
│   │   ├── ServiceCollectionExtensions.cs
│   │   ├── ApplicationBuilderExtensions.cs
│   │   ├── AuthenticationExtensions.cs
│   │   ├── SwaggerExtensions.cs
│   │   └── CorsExtensions.cs
│   │
│   ├── Program.cs                  # Application entry point
│   ├── appsettings.json           # Configuration
│   ├── appsettings.Development.json
│   ├── appsettings.Production.json
│   ├── appsettings.Staging.json
│   ├── Dockerfile
│   ├── .dockerignore
│   └── NutriCare.API.csproj
│
├── NutriCare.Application/           # Application Layer
│   ├── DTOs/
│   │   ├── Auth/
│   │   │   ├── RegisterRequest.cs
│   │   │   ├── LoginRequest.cs
│   │   │   ├── LoginResponse.cs
│   │   │   ├── RefreshTokenRequest.cs
│   │   │   └── TokenResponse.cs
│   │   ├── User/
│   │   │   ├── UserDto.cs
│   │   │   ├── CreateUserRequest.cs
│   │   │   ├── UpdateUserRequest.cs
│   │   │   └── UserProfileDto.cs
│   │   ├── Patient/
│   │   │   ├── PatientProfileDto.cs
│   │   │   ├── CreatePatientProfileRequest.cs
│   │   │   ├── UpdatePatientProfileRequest.cs
│   │   │   └── PatientDashboardDto.cs
│   │   ├── Appointment/
│   │   │   ├── AppointmentDto.cs
│   │   │   ├── BookAppointmentRequest.cs
│   │   │   ├── AppointmentSlotDto.cs
│   │   │   └── AppointmentResponseDto.cs
│   │   ├── DietPlan/
│   │   │   ├── DietPlanDto.cs
│   │   │   ├── CreateDietPlanRequest.cs
│   │   │   ├── MealDto.cs
│   │   │   └── DietPlanDetailDto.cs
│   │   ├── Payment/
│   │   │   ├── PaymentDto.cs
│   │   │   ├── CreatePaymentIntentRequest.cs
│   │   │   ├── PaymentHistoryDto.cs
│   │   │   └── PaymentResponseDto.cs
│   │   ├── Progress/
│   │   │   ├── ProgressTrackingDto.cs
│   │   │   ├── CreateProgressRecordRequest.cs
│   │   │   └── ProgressSummaryDto.cs
│   │   ├── Chat/
│   │   │   ├── ChatMessageDto.cs
│   │   │   ├── SendMessageRequest.cs
│   │   │   └── ChatHistoryDto.cs
│   │   ├── Blog/
│   │   │   ├── BlogPostDto.cs
│   │   │   ├── CreateBlogPostRequest.cs
│   │   │   ├── BlogCommentDto.cs
│   │   │   └── BlogListItemDto.cs
│   │   ├── Notification/
│   │   │   ├── NotificationDto.cs
│   │   │   └── NotificationListDto.cs
│   │   └── Common/
│   │       ├── ApiResponse.cs
│   │       ├── PaginatedResponse.cs
│   │       ├── ErrorResponse.cs
│   │       └── PagedRequest.cs
│   │
│   ├── Services/
│   │   ├── Authentication/
│   │   │   ├── AuthenticationService.cs
│   │   │   ├── JwtTokenService.cs
│   │   │   ├── RefreshTokenService.cs
│   │   │   └── IAuthenticationService.cs
│   │   ├── User/
│   │   │   ├── UserService.cs
│   │   │   ├── UserProfileService.cs
│   │   │   ├── PatientService.cs
│   │   │   ├── NutritionistService.cs
│   │   │   └── IUserService.cs
│   │   ├── Appointment/
│   │   │   ├── AppointmentService.cs
│   │   │   ├── AppointmentSchedulingService.cs
│   │   │   ├── AvailabilityService.cs
│   │   │   └── IAppointmentService.cs
│   │   ├── DietPlan/
│   │   │   ├── DietPlanService.cs
│   │   │   ├── MealPlanningService.cs
│   │   │   ├── NutritionCalculatorService.cs
│   │   │   └── IDietPlanService.cs
│   │   ├── Payment/
│   │   │   ├── PaymentService.cs
│   │   │   ├── StripePaymentService.cs
│   │   │   ├── JazzCashPaymentService.cs
│   │   │   ├── EasyPaisaPaymentService.cs
│   │   │   └── IPaymentService.cs
│   │   ├── Progress/
│   │   │   ├── ProgressTrackingService.cs
│   │   │   ├── AnalyticsService.cs
│   │   │   ├── ReportGenerationService.cs
│   │   │   └── IProgressTrackingService.cs
│   │   ├── Chat/
│   │   │   ├── ChatService.cs
│   │   │   ├── MessageService.cs
│   │   │   └── IChatService.cs
│   │   ├── Blog/
│   │   │   ├── BlogService.cs
│   │   │   ├── BlogCommentService.cs
│   │   │   ├── IBlogService.cs
│   │   │   └── BlogContentValidationService.cs
│   │   ├── Notification/
│   │   │   ├── NotificationService.cs
│   │   │   ├── ReminderService.cs
│   │   │   ├── INotificationService.cs
│   │   │   └── NotificationScheduler.cs
│   │   ├── File/
│   │   │   ├── FileUploadService.cs
│   │   │   ├── AzureBlobStorageService.cs
│   │   │   └── IFileUploadService.cs
│   │   ├── Email/
│   │   │   ├── EmailService.cs
│   │   │   ├── SendGridEmailService.cs
│   │   │   └── IEmailService.cs
│   │   ├── SMS/
│   │   │   ├── SmsService.cs
│   │   │   ├── TwilioSmsService.cs
│   │   │   └── ISmsService.cs
│   │   └── Common/
│   │       ├── CacheService.cs
│   │       └── DateTimeService.cs
│   │
│   ├── Validators/
│   │   ├── Auth/
│   │   │   ├── RegisterRequestValidator.cs
│   │   │   ├── LoginRequestValidator.cs
│   │   │   └── RefreshTokenRequestValidator.cs
│   │   ├── User/
│   │   │   ├── CreateUserRequestValidator.cs
│   │   │   └── UpdateUserRequestValidator.cs
│   │   ├── Appointment/
│   │   │   ├── BookAppointmentRequestValidator.cs
│   │   │   └── RescheduleAppointmentRequestValidator.cs
│   │   ├── Payment/
│   │   │   ├── CreatePaymentIntentValidator.cs
│   │   │   └── PaymentAmountValidator.cs
│   │   └── DietPlan/
│   │       ├── CreateDietPlanRequestValidator.cs
│   │       └── MealValidator.cs
│   │
│   ├── Mappings/
│   │   ├── UserMappingProfile.cs
│   │   ├── AppointmentMappingProfile.cs
│   │   ├── DietPlanMappingProfile.cs
│   │   ├── PaymentMappingProfile.cs
│   │   ├── ProgressMappingProfile.cs
│   │   ├── ChatMappingProfile.cs
│   │   └── BlogMappingProfile.cs
│   │
│   ├── Exceptions/
│   │   ├── ApplicationException.cs
│   │   ├── ValidationException.cs
│   │   ├── NotFoundException.cs
│   │   ├── UnauthorizedException.cs
│   │   ├── ConflictException.cs
│   │   └── BusinessLogicException.cs
│   │
│   └── NutriCare.Application.csproj
│
├── NutriCare.Core/                  # Domain Layer
│   ├── Entities/
│   │   ├── User.cs                 # Base user entity
│   │   ├── PatientProfile.cs
│   │   ├── NutritionistProfile.cs
│   │   ├── Appointment.cs
│   │   ├── DietPlan.cs
│   │   ├── Meal.cs
│   │   ├── Payment.cs
│   │   ├── ProgressTracking.cs
│   │   ├── MedicalReport.cs
│   │   ├── ChatMessage.cs
│   │   ├── BlogPost.cs
│   │   ├── BlogComment.cs
│   │   ├── Notification.cs
│   │   ├── Reminder.cs
│   │   ├── VideoConsultation.cs
│   │   ├── RefreshToken.cs
│   │   └── BaseEntity.cs           # Base class for all entities
│   │
│   ├── Interfaces/
│   │   ├── IRepository.cs          # Generic repository interface
│   │   ├── IUnitOfWork.cs          # Unit of work pattern
│   │   ├── IAuthenticationService.cs
│   │   ├── IUserRepository.cs
│   │   ├── IAppointmentRepository.cs
│   │   ├── IDietPlanRepository.cs
│   │   ├── IPaymentRepository.cs
│   │   ├── IProgressTrackingRepository.cs
│   │   ├── IMedicalReportRepository.cs
│   │   ├── IChatMessageRepository.cs
│   │   ├── IBlogRepository.cs
│   │   ├── INotificationRepository.cs
│   │   ├── IFileStorage.cs
│   │   ├── IEmailService.cs
│   │   ├── ISmsService.cs
│   │   ├── IPaymentGateway.cs
│   │   ├── IEventBus.cs
│   │   └── IRepository.cs
│   │
│   ├── Enums/
│   │   ├── UserRole.cs             # Patient, Nutritionist, Admin
│   │   ├── AppointmentStatus.cs    # Scheduled, Completed, Cancelled, NoShow
│   │   ├── AppointmentType.cs      # Initial, Follow-up, Video, In-person
│   │   ├── PaymentStatus.cs        # Pending, Completed, Failed, Refunded
│   │   ├── PaymentMethod.cs        # Stripe, JazzCash, EasyPaisa
│   │   ├── DietPlanStatus.cs       # Active, Archived, Completed
│   │   ├── Gender.cs               # Male, Female, Other
│   │   ├── NotificationType.cs     # Appointment, Message, Payment, System
│   │   ├── ReminderType.cs         # Appointment, DietPlan, Medication
│   │   └── BlogPostStatus.cs       # Draft, Published, Archived
│   │
│   ├── Constants/
│   │   ├── AppConstants.cs         # Application-wide constants
│   │   ├── ValidationConstants.cs  # Validation rules
│   │   ├── RegexPatterns.cs       # Regex patterns
│   │   ├── ErrorMessages.cs       # Error message constants
│   │   └── BusinessRules.cs       # Business logic constants
│   │
│   ├── Events/
│   │   ├── DomainEvent.cs          # Base domain event
│   │   ├── User/
│   │   │   ├── UserRegisteredEvent.cs
│   │   │   ├── UserProfileUpdatedEvent.cs
│   │   │   └── UserDeactivatedEvent.cs
│   │   ├── Appointment/
│   │   │   ├── AppointmentBookedEvent.cs
│   │   │   ├── AppointmentCancelledEvent.cs
│   │   │   └── AppointmentCompletedEvent.cs
│   │   ├── Payment/
│   │   │   ├── PaymentProcessedEvent.cs
│   │   │   ├── PaymentRefundedEvent.cs
│   │   │   └── PaymentFailedEvent.cs
│   │   ├── DietPlan/
│   │   │   ├── DietPlanCreatedEvent.cs
│   │   │   └── DietPlanUpdatedEvent.cs
│   │   └── Notification/
│   │       ├── NotificationSentEvent.cs
│   │       └── ReminderCreatedEvent.cs
│   │
│   ├── Specifications/  (Optional)
│   │   ├── BaseSpecification.cs
│   │   ├── AppointmentSpecifications.cs
│   │   ├── DietPlanSpecifications.cs
│   │   └── PaymentSpecifications.cs
│   │
│   └── NutriCare.Core.csproj
│
├── NutriCare.Infrastructure/        # Infrastructure Layer
│   ├── ExternalServices/
│   │   ├── Payment/
│   │   │   ├── StripePaymentService.cs
│   │   │   ├── JazzCashPaymentService.cs
│   │   │   ├── EasyPaisaPaymentService.cs
│   │   │   └── PaymentGatewayFactory.cs
│   │   ├── Video/
│   │   │   ├── VideoConferenceService.cs
│   │   │   └── TwilioVideoService.cs
│   │   └── Analytics/
│   │       ├── AnalyticsService.cs
│   │       └── ApplicationInsightsService.cs
│   │
│   ├── Authentication/
│   │   ├── JwtTokenGenerator.cs
│   │   ├── TokenValidationService.cs
│   │   ├── RefreshTokenHandler.cs
│   │   └── PasswordHasher.cs
│   │
│   ├── Storage/
│   │   ├── AzureBlobStorageService.cs
│   │   ├── BlobStorageConfiguration.cs
│   │   └── FileValidationService.cs
│   │
│   ├── Notifications/
│   │   ├── SendGridEmailProvider.cs
│   │   ├── TwilioSmsProvider.cs
│   │   ├── NotificationTemplateService.cs
│   │   └── PushNotificationService.cs
│   │
│   ├── Events/
│   │   ├── EventBusService.cs      # Azure Service Bus
│   │   ├── EventHandlerRegistry.cs
│   │   └── IntegrationEventPublisher.cs
│   │
│   └── NutriCare.Infrastructure.csproj
│
├── NutriCare.Persistence/           # Data Access Layer
│   ├── Context/
│   │   ├── NutriCareDbContext.cs    # Main DbContext
│   │   ├── DesignTimeDbContextFactory.cs
│   │   └── DbContextConfiguration.cs
│   │
│   ├── Migrations/
│   │   ├── 20260602000000_InitialCreate.cs
│   │   ├── 20260605000000_AddAppointmentFields.cs
│   │   ├── 20260610000000_AddPaymentModule.cs
│   │   └── [Version]_[Description].cs
│   │
│   ├── Repositories/
│   │   ├── Base/
│   │   │   ├── Repository.cs       # Generic repository
│   │   │   ├── RepositoryBase.cs
│   │   │   └── IRepository.cs
│   │   ├── UnitOfWork.cs           # Unit of Work pattern
│   │   ├── User/
│   │   │   ├── UserRepository.cs
│   │   │   ├── PatientProfileRepository.cs
│   │   │   └── NutritionistProfileRepository.cs
│   │   ├── Appointment/
│   │   │   ├── AppointmentRepository.cs
│   │   │   ├── AppointmentFollowUpRepository.cs
│   │   │   └── AvailabilityRepository.cs
│   │   ├── DietPlan/
│   │   │   ├── DietPlanRepository.cs
│   │   │   └── MealRepository.cs
│   │   ├── Payment/
│   │   │   ├── PaymentRepository.cs
│   │   │   └── InvoiceRepository.cs
│   │   ├── Progress/
│   │   │   └── ProgressTrackingRepository.cs
│   │   ├── Medical/
│   │   │   └── MedicalReportRepository.cs
│   │   ├── Chat/
│   │   │   └── ChatMessageRepository.cs
│   │   ├── Blog/
│   │   │   ├── BlogRepository.cs
│   │   │   └── BlogCommentRepository.cs
│   │   ├── Notification/
│   │   │   ├── NotificationRepository.cs
│   │   │   ├── ReminderRepository.cs
│   │   │   └── RefreshTokenRepository.cs
│   │   └── Specifications/
│   │       ├── SpecificationEvaluator.cs
│   │       └── ISpecificationEvaluator.cs
│   │
│   ├── Seeds/
│   │   ├── DatabaseSeeder.cs
│   │   ├── AdminUserSeeder.cs
│   │   ├── SampleNutritionistsSeeder.cs
│   │   ├── SamplePatientsSeeder.cs
│   │   ├── SampleDietPlansSeeder.cs
│   │   ├── ConfigurationDataSeeder.cs
│   │   └── BlogPostSeeder.cs
│   │
│   ├── Configuration/
│   │   ├── UserConfiguration.cs
│   │   ├── PatientProfileConfiguration.cs
│   │   ├── NutritionistProfileConfiguration.cs
│   │   ├── AppointmentConfiguration.cs
│   │   ├── DietPlanConfiguration.cs
│   │   ├── PaymentConfiguration.cs
│   │   ├── ProgressTrackingConfiguration.cs
│   │   ├── MedicalReportConfiguration.cs
│   │   ├── ChatMessageConfiguration.cs
│   │   ├── BlogConfiguration.cs
│   │   ├── NotificationConfiguration.cs
│   │   ├── ReminderConfiguration.cs
│   │   ├── VideoConsultationConfiguration.cs
│   │   └── RefreshTokenConfiguration.cs
│   │
│   └── NutriCare.Persistence.csproj
│
├── NutriCare.sln                   # Solution file
├── .gitignore
├── .editorconfig
├── global.json                      # .NET version specification
└── Directory.Build.props            # Shared project properties
```

---

## Frontend Structure

```
frontend/nutricare-pro-portal/
│
├── src/
│   ├── app/                         # Next.js App Router
│   │   ├── layout.tsx              # Root layout
│   │   ├── page.tsx                # Home page
│   │   ├── globals.css             # Global styles
│   │   │
│   │   ├── (auth)/                 # Authentication routes
│   │   │   ├── login/
│   │   │   │   └── page.tsx
│   │   │   ├── register/
│   │   │   │   └── page.tsx
│   │   │   ├── forgot-password/
│   │   │   │   └── page.tsx
│   │   │   └── reset-password/
│   │   │       └── page.tsx
│   │   │
│   │   ├── (dashboard)/             # Protected routes
│   │   │   ├── layout.tsx
│   │   │   │
│   │   │   ├── patient/
│   │   │   │   ├── layout.tsx
│   │   │   │   ├── dashboard/
│   │   │   │   │   └── page.tsx
│   │   │   │   ├── appointments/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   ├── book/
│   │   │   │   │   │   └── page.tsx
│   │   │   │   │   └── [id]/
│   │   │   │   │       └── page.tsx
│   │   │   │   ├── diet-plans/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   └── [id]/
│   │   │   │   │       └── page.tsx
│   │   │   │   ├── medical-reports/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   └── upload/
│   │   │   │   │       └── page.tsx
│   │   │   │   ├── progress/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   └── add-record/
│   │   │   │   │       └── page.tsx
│   │   │   │   ├── chat/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   └── [id]/
│   │   │   │   │       └── page.tsx
│   │   │   │   ├── payments/
│   │   │   │   │   └── page.tsx
│   │   │   │   ├── profile/
│   │   │   │   │   └── page.tsx
│   │   │   │   └── settings/
│   │   │   │       └── page.tsx
│   │   │   │
│   │   │   ├── nutritionist/
│   │   │   │   ├── layout.tsx
│   │   │   │   ├── dashboard/
│   │   │   │   │   └── page.tsx
│   │   │   │   ├── patients/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   └── [id]/
│   │   │   │   │       ├── page.tsx
│   │   │   │   │       ├── progress/
│   │   │   │   │       │   └── page.tsx
│   │   │   │   │       └── prescribe-diet/
│   │   │   │   │           └── page.tsx
│   │   │   │   ├── appointments/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   └── [id]/
│   │   │   │   │       └── page.tsx
│   │   │   │   ├── diet-plans/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   └── create/
│   │   │   │   │       └── page.tsx
│   │   │   │   ├── consultations/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   └── [id]/
│   │   │   │   │       └── page.tsx
│   │   │   │   ├── schedule/
│   │   │   │   │   └── page.tsx
│   │   │   │   ├── earnings/
│   │   │   │   │   └── page.tsx
│   │   │   │   ├── blog/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   ├── create/
│   │   │   │   │   │   └── page.tsx
│   │   │   │   │   └── [id]/
│   │   │   │   │       └── page.tsx
│   │   │   │   └── profile/
│   │   │   │       └── page.tsx
│   │   │   │
│   │   │   └── admin/
│   │   │       ├── layout.tsx
│   │   │       ├── dashboard/
│   │   │       │   └── page.tsx
│   │   │       ├── users/
│   │   │       │   ├── page.tsx
│   │   │       │   └── [id]/
│   │   │       │       └── page.tsx
│   │   │       ├── nutritionists/
│   │   │       │   ├── page.tsx
│   │   │       │   └── verify/
│   │   │       │       └── page.tsx
│   │   │       ├── payments/
│   │   │       │   └── page.tsx
│   │   │       ├── reports/
│   │   │       │   └── page.tsx
│   │   │       ├── settings/
│   │   │       │   └── page.tsx
│   │   │       └── blog-moderation/
│   │   │           └── page.tsx
│   │   │
│   │   ├── blog/                    # Public blog pages
│   │   │   ├── page.tsx
│   │   │   └── [slug]/
│   │   │       └── page.tsx
│   │   │
│   │   └── error.tsx               # Error boundary
│   │
│   ├── components/
│   │   ├── Common/
│   │   │   ├── Header.tsx
│   │   │   ├── Sidebar.tsx
│   │   │   ├── Footer.tsx
│   │   │   ├── Navigation.tsx
│   │   │   ├── LoadingSpinner.tsx
│   │   │   ├── ErrorBoundary.tsx
│   │   │   ├── Modal.tsx
│   │   │   ├── Alert.tsx
│   │   │   ├── Badge.tsx
│   │   │   └── Breadcrumb.tsx
│   │   │
│   │   ├── Layout/
│   │   │   ├── DashboardLayout.tsx
│   │   │   ├── AuthLayout.tsx
│   │   │   ├── PublicLayout.tsx
│   │   │   └── AdminLayout.tsx
│   │   │
│   │   ├── Auth/
│   │   │   ├── LoginForm.tsx
│   │   │   ├── RegisterForm.tsx
│   │   │   ├── ForgotPasswordForm.tsx
│   │   │   ├── ResetPasswordForm.tsx
│   │   │   ├── ProtectedRoute.tsx
│   │   │   └── RoleBasedRenderer.tsx
│   │   │
│   │   ├── Patient/
│   │   │   ├── Dashboard/
│   │   │   │   ├── DashboardOverview.tsx
│   │   │   │   ├── UpcomingAppointments.tsx
│   │   │   │   ├── QuickStats.tsx
│   │   │   │   └── RecentActivity.tsx
│   │   │   ├── Appointments/
│   │   │   │   ├── AppointmentList.tsx
│   │   │   │   ├── BookAppointment.tsx
│   │   │   │   ├── AppointmentCard.tsx
│   │   │   │   ├── AppointmentDetails.tsx
│   │   │   │   ├── NutritionistSelection.tsx
│   │   │   │   └── TimeSlotSelector.tsx
│   │   │   ├── DietPlans/
│   │   │   │   ├── DietPlanList.tsx
│   │   │   │   ├── DietPlanDetail.tsx
│   │   │   │   ├── DietPlanCard.tsx
│   │   │   │   ├── MealPlanViewer.tsx
│   │   │   │   └── DietPlanDownload.tsx
│   │   │   ├── Progress/
│   │   │   │   ├── ProgressDashboard.tsx
│   │   │   │   ├── ProgressForm.tsx
│   │   │   │   ├── ProgressChart.tsx
│   │   │   │   ├── WeightChart.tsx
│   │   │   │   ├── MetricsChart.tsx
│   │   │   │   └── ProgressSummary.tsx
│   │   │   ├── MedicalReports/
│   │   │   │   ├── MedicalReportList.tsx
│   │   │   │   ├── ReportUpload.tsx
│   │   │   │   ├── ReportViewer.tsx
│   │   │   │   └── ReportCard.tsx
│   │   │   ├── Chat/
│   │   │   │   ├── ChatWindow.tsx
│   │   │   │   ├── ChatMessage.tsx
│   │   │   │   ├── ChatInput.tsx
│   │   │   │   ├── ChatList.tsx
│   │   │   │   └── ChatNotification.tsx
│   │   │   ├── Payments/
│   │   │   │   ├── PaymentHistory.tsx
│   │   │   │   ├── PaymentMethod.tsx
│   │   │   │   ├── StripeCheckout.tsx
│   │   │   │   └── PaymentReceipt.tsx
│   │   │   ├── Profile/
│   │   │   │   ├── ProfileForm.tsx
│   │   │   │   ├── ProfileImage.tsx
│   │   │   │   ├── HealthInfo.tsx
│   │   │   │   └── PreferencesForm.tsx
│   │   │   └── Settings/
│   │   │       ├── NotificationSettings.tsx
│   │   │       ├── PrivacySettings.tsx
│   │   │       ├── SecuritySettings.tsx
│   │   │       └── AccountSettings.tsx
│   │   │
│   │   ├── Nutritionist/
│   │   │   ├── Dashboard/
│   │   │   │   ├── NutritionistDashboard.tsx
│   │   │   │   ├── PatientStats.tsx
│   │   │   │   ├── EarningsChart.tsx
│   │   │   │   └── AppointmentCalendar.tsx
│   │   │   ├── Patients/
│   │   │   │   ├── PatientList.tsx
│   │   │   │   ├── PatientCard.tsx
│   │   │   │   ├── PatientDetail.tsx
│   │   │   │   └── PatientNotes.tsx
│   │   │   ├── DietPlans/
│   │   │   │   ├── DietPlanBuilder.tsx
│   │   │   │   ├── MealEditor.tsx
│   │   │   │   ├── NutritionCalculator.tsx
│   │   │   │   └── TemplateSelector.tsx
│   │   │   ├── Appointments/
│   │   │   │   ├── AppointmentCalendar.tsx
│   │   │   │   ├── ScheduleManager.tsx
│   │   │   │   └── AppointmentActions.tsx
│   │   │   ├── Consultations/
│   │   │   │   ├── VideoConsultation.tsx
│   │   │   │   ├── ConsultationNotes.tsx
│   │   │   │   └── RecordingPlayer.tsx
│   │   │   ├── Blog/
│   │   │   │   ├── BlogEditor.tsx
│   │   │   │   ├── BlogPreview.tsx
│   │   │   │   └── BlogList.tsx
│   │   │   └── Profile/
│   │   │       ├── ProfessionalInfo.tsx
│   │   │       ├── Specializations.tsx
│   │   │       └── Availability.tsx
│   │   │
│   │   ├── Admin/
│   │   │   ├── Dashboard/
│   │   │   │   ├── AdminDashboard.tsx
│   │   │   │   ├── SystemStats.tsx
│   │   │   │   └── Analytics.tsx
│   │   │   ├── Users/
│   │   │   │   ├── UserManagement.tsx
│   │   │   │   ├── UserTable.tsx
│   │   │   │   └── UserActions.tsx
│   │   │   ├── Nutritionists/
│   │   │   │   ├── NutritionistVerification.tsx
│   │   │   │   └── NutritionistList.tsx
│   │   │   ├── Payments/
│   │   │   │   ├── PaymentManagement.tsx
│   │   │   │   └── PaymentStats.tsx
│   │   │   └── Reports/
│   │   │       ├── ReportGenerator.tsx
│   │   │       └── ReportViewer.tsx
│   │   │
│   │   ├── Blog/
│   │   │   ├── BlogCard.tsx
│   │   │   ├── BlogGrid.tsx
│   │   │   ├── BlogDetail.tsx
│   │   │   ├── BlogComments.tsx
│   │   │   └── BlogSearch.tsx
│   │   │
│   │   └── VideoConsultation/
│   │       ├── VideoRoom.tsx
│   │       ├── VideoControls.tsx
│   │       ├── ParticipantList.tsx
│   │       └── ScreenShare.tsx
│   │
│   ├── hooks/
│   │   ├── useAuth.ts              # Authentication hook
│   │   ├── useApi.ts               # API call hook
│   │   ├── useFetch.ts             # Fetch data hook
│   │   ├── useLocalStorage.ts      # Local storage hook
│   │   ├── useSession.ts           # Session management
│   │   ├── useUser.ts              # User context
│   │   ├── useNotifications.ts     # Notification hook
│   │   ├── useChat.ts              # Chat real-time hook
│   │   ├── useAppointment.ts       # Appointment management
│   │   ├── useDietPlan.ts          # Diet plan hook
│   │   ├── useProgress.ts          # Progress tracking hook
│   │   ├── usePayment.ts           # Payment hook
│   │   ├── useForm.ts              # Form handling
│   │   ├── useModal.ts             # Modal management
│   │   └── useTheme.ts             # Theme management
│   │
│   ├── services/
│   │   ├── api.ts                  # Axios/Fetch setup
│   │   ├── auth.service.ts         # Auth API calls
│   │   ├── user.service.ts         # User API calls
│   │   ├── appointment.service.ts  # Appointment API calls
│   │   ├── diet-plan.service.ts    # Diet plan API calls
│   │   ├── payment.service.ts      # Payment API calls
│   │   ├── progress.service.ts     # Progress API calls
│   │   ├── chat.service.ts         # Chat API calls
│   │   ├── blog.service.ts         # Blog API calls
│   │   ├── notification.service.ts # Notification API calls
│   │   ├── medical-report.service.ts
│   │   ├── video-consultation.service.ts
│   │   ├── storage.service.ts      # Local/Session storage
│   │   └── signalr.service.ts      # SignalR connection
│   │
│   ├── store/                       # State Management (Redux/Zustand)
│   │   ├── auth/
│   │   │   ├── authSlice.ts
│   │   │   └── authThunks.ts
│   │   ├── user/
│   │   │   ├── userSlice.ts
│   │   │   └── userThunks.ts
│   │   ├── appointments/
│   │   │   ├── appointmentSlice.ts
│   │   │   └── appointmentThunks.ts
│   │   ├── notifications/
│   │   │   └── notificationSlice.ts
│   │   ├── ui/
│   │   │   ├── uiSlice.ts
│   │   │   └── modalSlice.ts
│   │   ├── store.ts
│   │   └── rootReducer.ts
│   │
│   ├── lib/
│   │   ├── axios.ts                # Axios configuration
│   │   ├── constants.ts            # App constants
│   │   ├── localStorage.ts         # LocalStorage utilities
│   │   ├── sessionStorage.ts       # Session storage utilities
│   │   ├── validation.ts           # Validation functions
│   │   ├── date-utils.ts           # Date manipulation
│   │   ├── format-utils.ts         # Formatting functions
│   │   ├── signalr-connection.ts   # SignalR setup
│   │   └── config.ts               # Configuration
│   │
│   ├── types/
│   │   ├── api.types.ts           # API response types
│   │   ├── auth.types.ts          # Auth types
│   │   ├── user.types.ts          # User types
│   │   ├── appointment.types.ts   # Appointment types
│   │   ├── diet-plan.types.ts     # Diet plan types
│   │   ├── payment.types.ts       # Payment types
│   │   ├── progress.types.ts      # Progress types
│   │   ├── chat.types.ts          # Chat types
│   │   ├── blog.types.ts          # Blog types
│   │   ├── notification.types.ts  # Notification types
│   │   ├── common.types.ts        # Common types
│   │   └── index.ts               # Type exports
│   │
│   ├── utils/
│   │   ├── logger.ts              # Logging utility
│   │   ├── error-handler.ts       # Error handling
│   │   ├── auth-helper.ts         # Auth utilities
│   │   ├── date-formatter.ts      # Date formatting
│   │   ├── currency-formatter.ts  # Currency formatting
│   │   ├── notifications.ts       # Notification helpers
│   │   ├── validators.ts          # Form validators
│   │   └── helpers.ts             # General helpers
│   │
│   ├── middleware/
│   │   ├── auth.middleware.ts     # Auth middleware
│   │   ├── error.middleware.ts    # Error handling middleware
│   │   ├── logging.middleware.ts  # Request logging
│   │   └── api.middleware.ts      # API request interceptor
│   │
│   └── styles/
│       ├── globals.css            # Global styles
│       ├── variables.css          # CSS variables/theme
│       ├── tailwind.css           # Tailwind imports
│       └── animations.css         # Animations
│
├── public/
│   ├── images/
│   │   ├── logo.png
│   │   ├── hero.jpg
│   │   ├── icons/
│   │   └── illustrations/
│   ├── fonts/
│   └── manifest.json
│
├── .env.local                      # Environment variables (local)
├── .env.development               # Development variables
├── .env.production                # Production variables
├── .env.example                   # Example env file
│
├── next.config.js                 # Next.js configuration
├── tailwind.config.js             # Tailwind CSS config
├── postcss.config.js              # PostCSS config
├── tsconfig.json                  # TypeScript config
├── .eslintrc.json                 # ESLint config
├── .prettierrc                    # Prettier config
├── package.json                   # Dependencies
├── package-lock.json              # Lock file
│
├── Dockerfile                     # Docker configuration
├── .dockerignore
├── docker-compose.yml
│
└── README.md                      # Frontend documentation
```

---

## Documentation Structure

```
docs/
├── 01_ARCHITECTURE_OVERVIEW.md
├── 02_DATABASE_DESIGN.md
├── 03_API_MODULE_BREAKDOWN.md
├── 04_MICROSERVICES_READINESS_PLAN.md
├── 05_PROJECT_STRUCTURE.md (this file)
├── 06_SETUP_GUIDE.md
├── 07_DEVELOPMENT_STANDARDS.md
├── 08_DEPLOYMENT_GUIDE.md
├── 09_TESTING_STRATEGY.md
├── 10_SECURITY_GUIDELINES.md
├── 11_PERFORMANCE_OPTIMIZATION.md
├── 12_TROUBLESHOOTING.md
└── GLOSSARY.md
```

---

## Deployment Structure

```
deployment/
├── kubernetes/
│   ├── namespaces/
│   │   └── nutricare-namespace.yaml
│   ├── secrets/
│   │   ├── auth-secrets.yaml
│   │   ├── db-secrets.yaml
│   │   └── payment-secrets.yaml
│   ├── configmaps/
│   │   └── app-config.yaml
│   ├── backend/
│   │   ├── api-deployment.yaml
│   │   ├── api-service.yaml
│   │   └── api-ingress.yaml
│   ├── frontend/
│   │   ├── frontend-deployment.yaml
│   │   ├── frontend-service.yaml
│   │   └── frontend-ingress.yaml
│   ├── database/
│   │   ├── sql-server-deployment.yaml
│   │   └── sql-server-pvc.yaml
│   └── monitoring/
│       ├── prometheus-config.yaml
│       └── grafana-config.yaml
│
├── docker/
│   ├── backend.dockerfile
│   ├── frontend.dockerfile
│   └── docker-compose.yml
│
├── terraform/
│   ├── main.tf
│   ├── variables.tf
│   ├── outputs.tf
│   ├── azure/
│   │   ├── app-service.tf
│   │   ├── sql-database.tf
│   │   ├── storage.tf
│   │   └── key-vault.tf
│   └── environments/
│       ├── dev.tfvars
│       ├── staging.tfvars
│       └── prod.tfvars
│
├── scripts/
│   ├── deploy.sh
│   ├── rollback.sh
│   ├── backup.sh
│   ├── migrate-database.sh
│   └── health-check.sh
│
├── cicd/
│   ├── .github/
│   │   └── workflows/
│   │       ├── backend-ci.yml
│   │       ├── frontend-ci.yml
│   │       ├── deploy-dev.yml
│   │       ├── deploy-staging.yml
│   │       └── deploy-production.yml
│   └── azure-pipelines/
│       ├── backend-pipeline.yml
│       ├── frontend-pipeline.yml
│       └── deployment-pipeline.yml
│
└── environments/
    ├── dev/
    │   └── .env
    ├── staging/
    │   └── .env
    └── production/
        └── .env
```

---

## Key Characteristics

### Backend Architecture
- **Clean Architecture** with clear separation of concerns
- **DDD** (Domain-Driven Design) principles
- **Repository Pattern** for data access
- **Unit of Work** for transaction management
- **Dependency Injection** for loose coupling
- **Event-Driven** architecture ready

### Frontend Architecture
- **App Router** (Next.js 13+)
- **Server Components** for performance
- **Client Components** for interactivity
- **Type-Safe** with TypeScript
- **Component-Based** architecture
- **State Management** (Redux/Zustand)
- **Custom Hooks** for logic reuse

### Scalability
- Modular design for easy feature addition
- Service-oriented structure
- Database indexes for performance
- Caching strategies
- Microservices-ready architecture

### Maintainability
- Clear separation of concerns
- Naming conventions
- Documentation
- Testing structure prepared
- Configuration management

