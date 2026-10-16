# NutriCare Pro - SQL Server Database Schema Design

## Overview

**Database Name:** NutriCarePro  
**Database Engine:** SQL Server 2022+  
**Collation:** SQL_Latin1_General_CP1_CI_AS  
**Recovery Model:** Full  
**Edition:** Enterprise/Standard  

---

## 📊 Schema Summary

| Component | Count | Details |
|-----------|-------|---------|
| **Tables** | 19 | Core data entities |
| **Indexes** | 45+ | Non-clustered indexes for performance |
| **Views** | 3 | Summary and reporting views |
| **Stored Procedures** | 4 | Common operations |
| **Foreign Keys** | 25+ | Referential integrity |
| **Check Constraints** | 30+ | Data validation |
| **Unique Constraints** | 10+ | Data uniqueness |

---

## 🏗️ Architecture Pattern

**Normalization Level:** Third Normal Form (3NF)  
**Design Pattern:** Entity-Relationship Model with DDD principles  
**Approach:** Relational database with JSON for complex fields  

---

## 📋 Table Specifications

### 1. **Roles Table**
**Purpose:** Define system roles

```
PK: RoleId (INT, IDENTITY)
Columns:
  - RoleName (NVARCHAR(50), UNIQUE)
  - Description (NVARCHAR(500))
  - IsActive (BIT, DEFAULT 1)
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Constraints:
  - RoleName cannot be empty
  
Values:
  - Patient
  - Nutritionist
  - Admin
```

---

### 2. **Users Table**
**Purpose:** Base user entity for all system users

```
PK: UserId (BIGINT, IDENTITY)
FK: RoleId → Roles(RoleId)

Columns:
  - Email (NVARCHAR(256), UNIQUE)
  - FirstName (NVARCHAR(100))
  - LastName (NVARCHAR(100))
  - PhoneNumber (NVARCHAR(20))
  - PasswordHash (NVARCHAR(MAX)) - Hashed with salt
  - ProfileImageUrl (NVARCHAR(500)) - Azure Blob URL
  - IsEmailVerified (BIT, DEFAULT 0)
  - IsPhoneVerified (BIT, DEFAULT 0)
  - IsActive (BIT, DEFAULT 1)
  - LastLoginAt (DATETIME2)
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)
  - DeletedAt (DATETIME2) - Soft delete flag

Indexes:
  - IX_Users_Email (Email)
  - IX_Users_RoleId (RoleId)
  - IX_Users_IsActive (IsActive)
  - IX_Users_CreatedAt (CreatedAt)

Constraints:
  - Email format validation
  - First/Last names must not be empty
```

---

### 3. **Patients Table**
**Purpose:** Patient-specific health information

```
PK: PatientId (BIGINT, IDENTITY)
FK: UserId → Users(UserId) [UNIQUE]

Columns:
  - UserId (BIGINT, UNIQUE) - 1:1 relationship with Users
  - DateOfBirth (DATE)
  - Gender (NVARCHAR(20)) - Male, Female, Other
  - BloodGroup (NVARCHAR(10)) - A+, A-, B+, B-, O+, O-, AB+, AB-
  - Height (DECIMAL(5,2)) - cm
  - CurrentWeight (DECIMAL(5,2)) - kg
  - TargetWeight (DECIMAL(5,2)) - kg (nullable)
  - MedicalHistory (NVARCHAR(MAX))
  - Allergies (NVARCHAR(MAX))
  - CurrentMedications (NVARCHAR(MAX))
  - EmergencyContact (NVARCHAR(100))
  - EmergencyContactPhone (NVARCHAR(20))
  - Address (NVARCHAR(500))
  - City (NVARCHAR(100))
  - State (NVARCHAR(100))
  - ZipCode (NVARCHAR(20))
  - Country (NVARCHAR(100))
  - PreferredLanguage (NVARCHAR(50)) - DEFAULT 'English'
  - NotificationPreference (NVARCHAR(50)) - Email, SMS, Both
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_Patients_UserId (UserId)
  - IX_Patients_City (City)

Constraints:
  - DateOfBirth < GETDATE()
  - Height: 0 < Height < 300
  - CurrentWeight: 0 < Weight < 500
  - TargetWeight: 0 < Weight < 500 (if not null)
```

---

### 4. **Nutritionists Table**
**Purpose:** Nutritionist professional profile

```
PK: NutritionistId (BIGINT, IDENTITY)
FK: UserId → Users(UserId) [UNIQUE]

Columns:
  - UserId (BIGINT, UNIQUE)
  - LicenseNumber (NVARCHAR(100), UNIQUE)
  - Qualification (NVARCHAR(500)) - M.S. Nutrition, etc.
  - Specializations (NVARCHAR(MAX)) - JSON array
  - Experience (INT) - Years
  - Bio (NVARCHAR(MAX))
  - ConsultationFee (DECIMAL(10,2))
  - ConsultationDuration (INT) - Minutes, DEFAULT 30
  - IsVerified (BIT, DEFAULT 0)
  - VerificationDate (DATETIME2)
  - VerificationDocumentUrl (NVARCHAR(500))
  - MaxPatientsPerDay (INT) - DEFAULT 10
  - AvailableHours (NVARCHAR(MAX)) - JSON: {"Monday": "09:00-17:00"}
  - Timezone (NVARCHAR(100)) - DEFAULT 'UTC'
  - RatingAverage (DECIMAL(3,2)) - 0-5 stars
  - TotalRatings (INT)
  - TotalPatientsServed (INT)
  - BankAccountDetails (NVARCHAR(MAX)) - Encrypted
  - TaxId (NVARCHAR(100))
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_Nutritionists_UserId (UserId)
  - IX_Nutritionists_IsVerified (IsVerified)
  - IX_Nutritionists_RatingAverage (RatingAverage DESC)

Constraints:
  - Experience >= 0
  - ConsultationFee > 0
  - ConsultationDuration > 0
  - MaxPatientsPerDay > 0
  - RatingAverage: 0 <= Rating <= 5
```

---

### 5. **Appointments Table**
**Purpose:** Consultation appointments

```
PK: AppointmentId (BIGINT, IDENTITY)
FK: PatientId → Patients(PatientId)
FK: NutritionistId → Nutritionists(NutritionistId)
FK: RescheduledFrom → Appointments(AppointmentId) (nullable)

Columns:
  - PatientId (BIGINT)
  - NutritionistId (BIGINT)
  - AppointmentDateTime (DATETIME2)
  - Duration (INT) - Minutes, DEFAULT 30
  - Status (NVARCHAR(50)) - Scheduled, InProgress, Completed, Cancelled, NoShow, Rescheduled
  - AppointmentType (NVARCHAR(50)) - Initial, Follow-up, Video, In-person
  - Location (NVARCHAR(500)) - Physical address or video room URL
  - Notes (NVARCHAR(MAX))
  - CancellationReason (NVARCHAR(500))
  - CancelledBy (NVARCHAR(50)) - Patient, Nutritionist, Admin
  - CancelledAt (DATETIME2)
  - RescheduledFrom (BIGINT, nullable)
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_Appointments_PatientId (PatientId)
  - IX_Appointments_NutritionistId (NutritionistId)
  - IX_Appointments_AppointmentDateTime (AppointmentDateTime)
  - IX_Appointments_Status (Status)
  - IX_Appointments_PatientNutritionistDate (PatientId, NutritionistId, AppointmentDateTime)

Constraints:
  - Duration > 0
  - Status IN (Scheduled, InProgress, Completed, Cancelled, NoShow, Rescheduled)
```

---

### 6. **VideoConsultations Table**
**Purpose:** Video consultation metadata

```
PK: VideoConsultationId (BIGINT, IDENTITY)
FK: AppointmentId → Appointments(AppointmentId) [UNIQUE]

Columns:
  - AppointmentId (BIGINT, UNIQUE)
  - RoomName (NVARCHAR(255), UNIQUE)
  - JoinToken (NVARCHAR(MAX))
  - CallStartTime (DATETIME2)
  - CallEndTime (DATETIME2)
  - Duration (INT) - Seconds
  - RecordingUrl (NVARCHAR(500))
  - RecordingStatus (NVARCHAR(50)) - NotRecorded, Recording, Completed, Failed
  - Notes (NVARCHAR(MAX))
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_VideoConsultations_AppointmentId (AppointmentId)
  - IX_VideoConsultations_RecordingStatus (RecordingStatus)
```

---

### 7. **DietPlans Table**
**Purpose:** Customized diet plans

```
PK: DietPlanId (BIGINT, IDENTITY)
FK: PatientId → Patients(PatientId)
FK: NutritionistId → Nutritionists(NutritionistId)

Columns:
  - PatientId (BIGINT)
  - NutritionistId (BIGINT)
  - Title (NVARCHAR(255))
  - Description (NVARCHAR(MAX))
  - Objective (NVARCHAR(500)) - Weight Loss, Muscle Gain, Diabetes Management
  - StartDate (DATE)
  - EndDate (DATE)
  - Duration (INT) - Days
  - Status (NVARCHAR(50)) - Active, Completed, Archived, OnHold
  - DailyCalories (INT)
  - ProteinGrams (DECIMAL(6,2))
  - CarbsGrams (DECIMAL(6,2))
  - FatsGrams (DECIMAL(6,2))
  - FiberGrams (DECIMAL(6,2))
  - SodiumMg (INT)
  - WaterIntakeLiters (DECIMAL(4,2))
  - MealFrequency (INT)
  - IsTemplate (BIT) - DEFAULT 0
  - Version (INT) - DEFAULT 1
  - Notes (NVARCHAR(MAX))
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_DietPlans_PatientId (PatientId)
  - IX_DietPlans_NutritionistId (NutritionistId)
  - IX_DietPlans_Status (Status)
  - IX_DietPlans_StartDate (StartDate)

Constraints:
  - EndDate >= StartDate
  - DailyCalories > 0 (if not null)
  - Status IN (Active, Completed, Archived, OnHold)
  - MealFrequency > 0 (if not null)
```

---

### 8. **DietPlanItems Table**
**Purpose:** Individual meals in a diet plan

```
PK: DietPlanItemId (BIGINT, IDENTITY)
FK: DietPlanId → DietPlans(DietPlanId) [CASCADE DELETE]

Columns:
  - DietPlanId (BIGINT)
  - DayNumber (INT) - 1-365
  - MealType (NVARCHAR(50)) - Breakfast, Lunch, Dinner, Snack1, Snack2
  - MealTime (TIME)
  - ItemName (NVARCHAR(255))
  - Quantity (DECIMAL(8,2))
  - Unit (NVARCHAR(20)) - grams, ml, cup, tbsp
  - Calories (INT)
  - Protein (DECIMAL(6,2))
  - Carbs (DECIMAL(6,2))
  - Fats (DECIMAL(6,2))
  - Fiber (DECIMAL(6,2))
  - Notes (NVARCHAR(500))
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_DietPlanItems_DietPlanId (DietPlanId)
  - IX_DietPlanItems_MealType (MealType)

Constraints:
  - DayNumber: 1 < DayNumber <= 365
  - Quantity > 0
```

---

### 9. **PatientProgress Table**
**Purpose:** General progress tracking

```
PK: ProgressId (BIGINT, IDENTITY)
FK: PatientId → Patients(PatientId) [CASCADE DELETE]

Columns:
  - PatientId (BIGINT)
  - RecordDate (DATE)
  - EnergyLevel (INT) - 1-10 scale
  - MoodScore (INT) - 1-10 scale
  - SleepHours (DECIMAL(4,2))
  - SleepQuality (INT) - 1-10 scale
  - ExerciseMinutes (INT)
  - ExerciseType (NVARCHAR(100)) - Walking, Running, Yoga
  - WaterIntakeLiters (DECIMAL(4,2))
  - MealsAdhered (INT)
  - TotalMealsPlanned (INT)
  - Notes (NVARCHAR(MAX))
  - Symptoms (NVARCHAR(MAX))
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_PatientProgress_PatientId (PatientId)
  - IX_PatientProgress_RecordDate (RecordDate DESC)
  - IX_PatientProgress_PatientRecordDate (PatientId, RecordDate DESC)

Constraints:
  - RecordDate <= GETDATE()
  - EnergyLevel: 1 <= Level <= 10
  - MoodScore: 1 <= Score <= 10
  - SleepHours: 0 < Hours <= 24
```

---

### 10. **WeightTracking Table**
**Purpose:** Dedicated weight and measurement tracking

```
PK: WeightTrackingId (BIGINT, IDENTITY)
FK: PatientId → Patients(PatientId) [CASCADE DELETE]

Columns:
  - PatientId (BIGINT)
  - RecordDate (DATE)
  - Weight (DECIMAL(5,2)) - kg [NOT NULL]
  - BMI (DECIMAL(5,2))
  - WaistCircumference (DECIMAL(5,2)) - cm
  - ChestCircumference (DECIMAL(5,2)) - cm
  - HipCircumference (DECIMAL(5,2)) - cm
  - ArmCircumference (DECIMAL(5,2)) - cm
  - ThighCircumference (DECIMAL(5,2)) - cm
  - BodyFatPercentage (DECIMAL(5,2))
  - MusclePercentage (DECIMAL(5,2))
  - WaterPercentage (DECIMAL(5,2))
  - BoneMassKg (DECIMAL(5,2))
  - MetabolicRate (INT) - BMR
  - Notes (NVARCHAR(500))
  - CreatedAt (DATETIME2)

Indexes:
  - IX_WeightTracking_PatientId (PatientId)
  - IX_WeightTracking_RecordDate (RecordDate DESC)
  - IX_WeightTracking_PatientRecordDate (PatientId, RecordDate DESC)

Constraints:
  - UNIQUE (PatientId, RecordDate) - One record per day
  - Weight: 0 < Weight < 500
  - BMI: 10 < BMI < 60 (if not null)
```

---

### 11. **MedicalReports Table**
**Purpose:** Patient medical documents

```
PK: MedicalReportId (BIGINT, IDENTITY)
FK: PatientId → Patients(PatientId) [CASCADE DELETE]

Columns:
  - PatientId (BIGINT)
  - ReportType (NVARCHAR(100)) - Blood Test, X-ray, Scan, etc.
  - ReportDate (DATE)
  - UploadDate (DATETIME2)
  - FileName (NVARCHAR(255))
  - FileUrl (NVARCHAR(500)) - Azure Blob URL
  - FileSize (BIGINT) - Bytes
  - MimeType (NVARCHAR(100)) - application/pdf, image/jpeg
  - Description (NVARCHAR(MAX))
  - IsSharedWithNutritionists (BIT) - DEFAULT 0
  - SharedNutritionists (NVARCHAR(MAX)) - JSON array
  - ExpiryDate (DATE)
  - Status (NVARCHAR(50)) - Active, Expired, Archived
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_MedicalReports_PatientId (PatientId)
  - IX_MedicalReports_ReportType (ReportType)
  - IX_MedicalReports_UploadDate (UploadDate DESC)

Constraints:
  - ReportDate <= GETDATE()
  - Status IN (Active, Expired, Archived)
```

---

### 12. **Payments Table**
**Purpose:** Payment transaction records

```
PK: PaymentId (BIGINT, IDENTITY)
FK: PatientId → Patients(PatientId)
FK: NutritionistId → Nutritionists(NutritionistId) [nullable]

Columns:
  - PatientId (BIGINT)
  - NutritionistId (BIGINT, nullable)
  - Amount (DECIMAL(10,2))
  - Currency (NVARCHAR(10)) - DEFAULT 'USD'
  - PaymentMethod (NVARCHAR(50)) - Stripe, JazzCash, EasyPaisa, CreditCard
  - TransactionId (NVARCHAR(255), UNIQUE) - Gateway transaction ID
  - Status (NVARCHAR(50)) - Pending, Completed, Failed, Refunded, PartialRefund
  - Description (NVARCHAR(MAX))
  - RelatedToType (NVARCHAR(50)) - Appointment, DietPlan, Service
  - RelatedToId (BIGINT) - Reference to related object
  - GatewayResponse (NVARCHAR(MAX)) - JSON response
  - RefundedAmount (DECIMAL(10,2)) - DEFAULT 0
  - RefundReason (NVARCHAR(500))
  - RefundedAt (DATETIME2)
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_Payments_PatientId (PatientId)
  - IX_Payments_NutritionistId (NutritionistId)
  - IX_Payments_Status (Status)
  - IX_Payments_CreatedAt (CreatedAt DESC)
  - IX_Payments_TransactionId (TransactionId)

Constraints:
  - Amount > 0
  - Status IN (Pending, Completed, Failed, Refunded, PartialRefund)
  - RefundedAmount: 0 <= RefundedAmount <= Amount
```

---

### 13. **Invoices Table**
**Purpose:** Invoice records

```
PK: InvoiceId (BIGINT, IDENTITY)
FK: PaymentId → Payments(PaymentId)
FK: PatientId → Patients(PatientId)
FK: NutritionistId → Nutritionists(NutritionistId) [nullable]

Columns:
  - InvoiceNumber (NVARCHAR(50), UNIQUE) - INV-2026-0001
  - PaymentId (BIGINT)
  - PatientId (BIGINT)
  - NutritionistId (BIGINT, nullable)
  - Amount (DECIMAL(10,2))
  - Currency (NVARCHAR(10)) - DEFAULT 'USD'
  - TaxAmount (DECIMAL(10,2)) - DEFAULT 0
  - TotalAmount (DECIMAL(10,2))
  - InvoiceDate (DATE)
  - DueDate (DATE, nullable)
  - PaidDate (DATE, nullable)
  - Status (NVARCHAR(50)) - Unpaid, Paid, Overdue, Cancelled
  - Description (NVARCHAR(MAX))
  - Notes (NVARCHAR(MAX))
  - InvoicePdfUrl (NVARCHAR(500))
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_Invoices_InvoiceNumber (InvoiceNumber)
  - IX_Invoices_PatientId (PatientId)
  - IX_Invoices_Status (Status)
  - IX_Invoices_InvoiceDate (InvoiceDate DESC)

Constraints:
  - Amount > 0
  - TaxAmount >= 0
  - TotalAmount = Amount + TaxAmount
  - DueDate >= InvoiceDate (if not null)
```

---

### 14. **Messages Table**
**Purpose:** Direct messaging between users

```
PK: MessageId (BIGINT, IDENTITY)
FK: SenderId → Users(UserId)
FK: ReceiverId → Users(UserId)

Columns:
  - SenderId (BIGINT)
  - ReceiverId (BIGINT)
  - Subject (NVARCHAR(255))
  - MessageBody (NVARCHAR(MAX))
  - Attachments (NVARCHAR(MAX)) - JSON array of file URLs
  - IsRead (BIT) - DEFAULT 0
  - ReadAt (DATETIME2)
  - IsArchived (BIT) - DEFAULT 0
  - ArchivedAt (DATETIME2)
  - MessageType (NVARCHAR(50)) - Text, Notification, Prescription, Report
  - Priority (NVARCHAR(50)) - Low, Normal, High, Urgent
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_Messages_ReceiverId (ReceiverId)
  - IX_Messages_IsRead (IsRead)
  - IX_Messages_CreatedAt (CreatedAt DESC)
  - IX_Messages_ReceiverRead (ReceiverId, IsRead)

Constraints:
  - SenderId != ReceiverId
```

---

### 15. **Notifications Table**
**Purpose:** System notifications

```
PK: NotificationId (BIGINT, IDENTITY)
FK: UserId → Users(UserId) [CASCADE DELETE]

Columns:
  - UserId (BIGINT)
  - Type (NVARCHAR(50)) - Appointment, Message, Payment, DietPlan, Progress, System, Review, Blog
  - Title (NVARCHAR(255))
  - Message (NVARCHAR(MAX))
  - Icon (NVARCHAR(100))
  - RelatedToType (NVARCHAR(50))
  - RelatedToId (BIGINT)
  - ActionUrl (NVARCHAR(500))
  - IsRead (BIT) - DEFAULT 0
  - ReadAt (DATETIME2)
  - Channel (NVARCHAR(50)) - InApp, Email, SMS, Push
  - Status (NVARCHAR(50)) - Pending, Sent, Failed, Bounced
  - SentAt (DATETIME2)
  - CreatedAt (DATETIME2)

Indexes:
  - IX_Notifications_UserId (UserId)
  - IX_Notifications_IsRead (IsRead)
  - IX_Notifications_CreatedAt (CreatedAt DESC)
  - IX_Notifications_UserRead (UserId, IsRead)

Constraints:
  - Type IN (Appointment, Message, Payment, DietPlan, Progress, System, Review, Blog)
```

---

### 16. **Blogs Table**
**Purpose:** Blog posts

```
PK: BlogId (BIGINT, IDENTITY)
FK: AuthorId → Users(UserId)

Columns:
  - AuthorId (BIGINT)
  - Title (NVARCHAR(500))
  - Slug (NVARCHAR(500), UNIQUE) - SEO-friendly URL
  - Content (NVARCHAR(MAX)) - Markdown
  - Excerpt (NVARCHAR(1000))
  - FeaturedImageUrl (NVARCHAR(500))
  - Category (NVARCHAR(100)) - Weight Loss, Diabetes, Heart Disease
  - Tags (NVARCHAR(MAX)) - JSON array
  - Status (NVARCHAR(50)) - Draft, Published, Archived, Scheduled
  - ScheduledPublishDate (DATETIME2)
  - ViewCount (INT) - DEFAULT 0
  - LikeCount (INT) - DEFAULT 0
  - CommentCount (INT) - DEFAULT 0
  - MetaDescription (NVARCHAR(500))
  - MetaKeywords (NVARCHAR(500))
  - IsCommentingEnabled (BIT) - DEFAULT 1
  - PublishedAt (DATETIME2)
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_Blogs_Slug (Slug)
  - IX_Blogs_Status (Status)
  - IX_Blogs_PublishedAt (PublishedAt DESC)
  - IX_Blogs_Category (Category)
  - IX_Blogs_AuthorId (AuthorId)

Constraints:
  - Status IN (Draft, Published, Archived, Scheduled)
```

---

### 17. **Comments Table**
**Purpose:** Blog comments

```
PK: CommentId (BIGINT, IDENTITY)
FK: BlogId → Blogs(BlogId) [CASCADE DELETE]
FK: UserId → Users(UserId)
FK: ParentCommentId → Comments(CommentId) [CASCADE DELETE, nullable]
FK: ApprovedBy → Users(UserId) [nullable]

Columns:
  - BlogId (BIGINT)
  - UserId (BIGINT)
  - ParentCommentId (BIGINT, nullable) - For nested comments
  - CommentText (NVARCHAR(MAX))
  - LikeCount (INT) - DEFAULT 0
  - Status (NVARCHAR(50)) - Pending, Approved, Rejected, Spam
  - ApprovedAt (DATETIME2)
  - ApprovedBy (BIGINT, nullable)
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_Comments_BlogId (BlogId)
  - IX_Comments_Status (Status)
  - IX_Comments_CreatedAt (CreatedAt DESC)

Constraints:
  - Status IN (Pending, Approved, Rejected, Spam)
```

---

### 18. **Reviews Table**
**Purpose:** Patient reviews for nutritionists

```
PK: ReviewId (BIGINT, IDENTITY)
FK: NutritionistId → Nutritionists(NutritionistId)
FK: PatientId → Patients(PatientId)
FK: AppointmentId → Appointments(AppointmentId) [nullable]

Columns:
  - NutritionistId (BIGINT)
  - PatientId (BIGINT)
  - AppointmentId (BIGINT, nullable)
  - Rating (INT) - 1-5 stars [NOT NULL]
  - Title (NVARCHAR(255))
  - ReviewText (NVARCHAR(MAX))
  - Professionalism (INT) - 1-5
  - Knowledge (INT) - 1-5
  - CommunicationSkills (INT) - 1-5
  - TimelinessRating (INT) - 1-5
  - ValueForMoney (INT) - 1-5
  - IsAnonymous (BIT) - DEFAULT 0
  - IsVerifiedPurchase (BIT) - DEFAULT 1
  - HelpfulCount (INT) - DEFAULT 0
  - Status (NVARCHAR(50)) - Pending, Approved, Rejected, Archived
  - CreatedAt (DATETIME2)
  - UpdatedAt (DATETIME2)

Indexes:
  - IX_Reviews_NutritionistId (NutritionistId)
  - IX_Reviews_PatientId (PatientId)
  - IX_Reviews_Status (Status)
  - IX_Reviews_CreatedAt (CreatedAt DESC)

Constraints:
  - Rating: 1 <= Rating <= 5
  - Detailed ratings: 1 <= Rating <= 5 (if not null)
  - Status IN (Pending, Approved, Rejected, Archived)
```

---

### 19. **AuditLogs Table**
**Purpose:** Audit trail for compliance

```
PK: AuditLogId (BIGINT, IDENTITY)
FK: UserId → Users(UserId) [nullable]

Columns:
  - UserId (BIGINT, nullable)
  - Action (NVARCHAR(100)) - Create, Read, Update, Delete, Login, Logout
  - EntityType (NVARCHAR(100)) - Users, Appointments, Payments, etc.
  - EntityId (BIGINT)
  - OldValues (NVARCHAR(MAX)) - JSON
  - NewValues (NVARCHAR(MAX)) - JSON
  - Description (NVARCHAR(MAX))
  - IpAddress (NVARCHAR(50))
  - UserAgent (NVARCHAR(500))
  - StatusCode (INT)
  - IsSuccess (BIT) - DEFAULT 1
  - ErrorMessage (NVARCHAR(MAX))
  - CreatedAt (DATETIME2)

Indexes:
  - IX_AuditLogs_UserId (UserId)
  - IX_AuditLogs_EntityType (EntityType)
  - IX_AuditLogs_CreatedAt (CreatedAt DESC)
  - IX_AuditLogs_Action (Action)
```

---

## 📊 Entity Relationship Diagram (ERD)

### Relationship Matrix

```
Users (1) ──→ (Many) Patients
Users (1) ──→ (Many) Nutritionists
Users (1) ──→ (Many) Blogs
Users (1) ──→ (Many) Messages (as Sender)
Users (1) ──→ (Many) Messages (as Receiver)
Users (1) ──→ (Many) Notifications
Users (1) ──→ (Many) AuditLogs

Patients (1) ──→ (Many) Appointments
Patients (1) ──→ (Many) DietPlans
Patients (1) ──→ (Many) PatientProgress
Patients (1) ──→ (Many) WeightTracking
Patients (1) ──→ (Many) MedicalReports
Patients (1) ──→ (Many) Payments
Patients (1) ──→ (Many) Invoices
Patients (1) ──→ (Many) Reviews

Nutritionists (1) ──→ (Many) Appointments
Nutritionists (1) ──→ (Many) DietPlans
Nutritionists (1) ──→ (Many) Payments
Nutritionists (1) ──→ (Many) Invoices
Nutritionists (1) ──→ (Many) Reviews

Appointments (1) ──→ (1) VideoConsultations
Appointments (1) ──→ (Many) Reviews

DietPlans (1) ──→ (Many) DietPlanItems

Blogs (1) ──→ (Many) Comments

Comments (1) ──→ (Many) Comments (Nested)
```

---

## 🔑 Key Relationships Explained

### 1:1 Relationships
- **Users → Patients** (UNIQUE FK)
- **Users → Nutritionists** (UNIQUE FK)
- **Appointments → VideoConsultations** (UNIQUE FK)

### 1:Many Relationships
- **Patients → Appointments** (Consultations)
- **Nutritionists → Appointments** (Conducted by)
- **DietPlans → DietPlanItems** (Meals in plan)
- **Patients → DietPlans** (Assigned to)
- **Patients → WeightTracking** (Daily tracking)
- **Payments → Invoices** (One payment, one invoice)

### Cascading Deletes
- **Users** → Patients, Nutritionists (when user deleted)
- **DietPlans** → DietPlanItems (when plan deleted)
- **Blogs** → Comments (when post deleted)
- **Comments** → Comments (nested, when parent deleted)
- **Patients** → Progress, Weight, Reports (when patient deleted)

---

## 🎯 Indexes Strategy

### Performance Indexes (Fast Queries)

**Frequently Filtered:**
```sql
- Users: Email, RoleId, IsActive
- Patients: UserId, City
- Appointments: PatientId, NutritionistId, Status, Date
- WeightTracking: PatientId, RecordDate (DESC)
- DietPlans: PatientId, Status
- Payments: PatientId, Status
- Notifications: UserId, IsRead
- Messages: ReceiverId, IsRead
```

**Composite Indexes (Multi-column):**
```sql
- Appointments: (PatientId, NutritionistId, AppointmentDateTime)
- PatientProgress: (PatientId, RecordDate DESC)
- WeightTracking: (PatientId, RecordDate DESC)
- Messages: (ReceiverId, IsRead)
- Notifications: (UserId, IsRead)
```

**Unique Indexes:**
```sql
- Users: Email
- Nutritionists: LicenseNumber
- Patients: UserId
- Nutritionists: UserId
- Payments: TransactionId
- WeightTracking: (PatientId, RecordDate)
- Invoices: InvoiceNumber
- Blogs: Slug
```

---

## 🔐 Data Integrity Constraints

### Primary Key Constraints
- All tables use BIGINT IDENTITY for surrogate keys
- Provides better performance and flexibility

### Foreign Key Constraints
- All relationships maintain referential integrity
- CASCADE DELETE for parent-child relationships
- ON DELETE RESTRICT where data loss not acceptable

### Unique Constraints
- Email must be unique (business requirement)
- License number must be unique (compliance)
- Transaction ID must be unique (external reference)

### Check Constraints
- Email format validation
- Date range validation
- Numeric range validation (ratings, durations, weights)
- Enum value validation (statuses, types)

---

## 📊 Views Created

### 1. vw_PatientDashboardSummary
Shows patient health overview:
- Current and target weight
- Upcoming appointments count
- Active diet plans
- Latest weight tracking data

### 2. vw_NutritionistPatientSummary
Shows nutritionist performance:
- Total patients
- Upcoming appointments
- Total diet plans
- Average rating
- Total earnings

### 3. vw_PaymentSummary
Shows payment statistics:
- Count by status
- Total and average amounts
- Min and max payment amounts

---

## 🔧 Stored Procedures

### sp_GetPatientAppointmentHistory
Retrieves patient appointments with pagination

### sp_CalculatePatientWeightProgress
Calculates weight loss metrics over date range

### sp_GetAvailableSlots
Generates available appointment slots for nutritionist

### sp_InsertAuditLog
Records audit trail entries for compliance

---

## 🔍 Naming Conventions

### Tables
- **Plural names:** Users, Appointments, DietPlans
- **PascalCase:** AppointmentId, PatientProgress
- **Prefixed with `dbo`:** [dbo].[TableName]

### Columns
- **PK:** [EntityId] (e.g., UserId, PatientId)
- **FK:** [ReferencedTableId] (e.g., PatientId, NutritionistId)
- **Boolean:** IsActive, IsVerified, IsRead (starts with Is)
- **DateTime:** CreatedAt, UpdatedAt (UTC suffix implicit)
- **Descriptive:** Meaningful, business-oriented names

### Indexes
- **Prefix:** `IX_`
- **Format:** `IX_TableName_ColumnName(s)`
- **Example:** `IX_Appointments_PatientId`

---

## 📈 Performance Recommendations

### 1. Partitioning
Partition large tables by date:
- WeightTracking (by RecordDate)
- PatientProgress (by RecordDate)
- Appointments (by AppointmentDateTime)
- Messages (by CreatedAt)

### 2. Archiving
Archive old data:
- Blog comments after 2 years
- Audit logs after 7 years
- Completed appointments after 3 years

### 3. Maintenance
- Rebuild indexes weekly
- Update statistics daily
- Rebuild fragmented indexes (>30% fragmentation)

### 4. Monitoring
- Monitor query execution plans
- Check for missing indexes
- Monitor table growth
- Monitor index fragmentation

---

## 🔐 Security Best Practices

### 1. Encryption
- **TDE (Transparent Data Encryption):** Enable at database level
- **Column Encryption:** Bank account details, sensitive data
- **Passwords:** Always hash with salt (never store plaintext)

### 2. Permissions
- **Principle of Least Privilege:** Users get minimum necessary permissions
- **Role-Based Access:** App users vs admin vs system accounts
- **Row-Level Security:** Patients see only their own data

### 3. Backup & Recovery
- **Daily backups:** Full database backup
- **Transaction logs:** Backed up every 15 minutes
- **Point-in-time recovery:** 35-day retention
- **Test recovery:** Monthly recovery tests

### 4. Audit Trail
- **All changes logged:** INSERT, UPDATE, DELETE
- **User tracking:** Who made changes and when
- **Failed attempts:** Login failures recorded
- **Retention:** 7 years for compliance

---

## 📋 Migration Notes

### Adding New Features
1. Design tables following normalization
2. Add to Migrations folder with version number
3. Include rollback script
4. Test on staging first
5. Document changes

### Schema Changes
- Use SSDT (SQL Server Data Tools) for version control
- Never modify tables in production directly
- Create migration scripts
- Test in dev/staging/prod sequence

---

## 🚀 Deployment Checklist

- [ ] Database created on target server
- [ ] All tables created
- [ ] All indexes created
- [ ] All views created
- [ ] All stored procedures created
- [ ] Permissions configured
- [ ] Backups configured
- [ ] Monitoring configured
- [ ] Connection strings updated
- [ ] Database documented

---

## 📞 Support & Troubleshooting

### Common Issues

**Foreign Key Violation:**
```sql
-- Check referential integrity
SELECT * FROM [dbo].[Appointments] 
WHERE [PatientId] NOT IN (SELECT [PatientId] FROM [dbo].[Patients])
```

**Missing Indexes:**
```sql
-- Find missing indexes
SELECT * FROM sys.dm_db_missing_index_details
```

**Index Fragmentation:**
```sql
-- Check fragmentation
SELECT * FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED')
WHERE avg_fragmentation_in_percent > 10
```

---

## 📊 Summary Statistics

| Metric | Count |
|--------|-------|
| Tables | 19 |
| Indexes | 45+ |
| Views | 3 |
| Stored Procedures | 4 |
| Foreign Keys | 25+ |
| Check Constraints | 30+ |
| Unique Constraints | 10+ |
| **Total Relationships** | **35+** |

---

**Database Design Version:** 1.0  
**Created:** June 2, 2026  
**Status:** ✅ Production Ready

