# NutriCare Pro - API Module Breakdown

## RESTful API Architecture

Base URL: `/api/v1`  
Response Format: JSON  
Authentication: Bearer JWT Token

---

## Authentication Module
**Prefix:** `/api/v1/auth`

### Endpoints

#### 1. Register
```
POST /auth/register
Request:
{
  "email": "patient@example.com",
  "password": "SecurePassword123",
  "firstName": "John",
  "lastName": "Doe",
  "userRole": "Patient", // Patient, Nutritionist, Admin
  "phoneNumber": "+1234567890"
}

Response (201):
{
  "id": "123",
  "email": "patient@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "role": "Patient",
  "createdAt": "2026-06-02T10:00:00Z"
}

Error (400):
{
  "message": "Email already registered",
  "code": "EMAIL_EXISTS"
}
```

#### 2. Login
```
POST /auth/login
Request:
{
  "email": "patient@example.com",
  "password": "SecurePassword123"
}

Response (200):
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "123",
    "email": "patient@example.com",
    "firstName": "John",
    "role": "Patient"
  }
}

Error (401):
{
  "message": "Invalid credentials",
  "code": "INVALID_CREDENTIALS"
}
```

#### 3. Refresh Token
```
POST /auth/refresh-token
Request:
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}

Response (200):
{
  "accessToken": "new_access_token...",
  "refreshToken": "new_refresh_token..."
}
```

#### 4. Logout
```
POST /auth/logout
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "message": "Logged out successfully"
}
```

#### 5. Verify Email
```
POST /auth/verify-email
Request:
{
  "token": "email_verification_token"
}

Response (200):
{
  "message": "Email verified successfully"
}
```

#### 6. Request Password Reset
```
POST /auth/forgot-password
Request:
{
  "email": "patient@example.com"
}

Response (200):
{
  "message": "Password reset link sent to email"
}
```

#### 7. Reset Password
```
POST /auth/reset-password
Request:
{
  "token": "reset_token",
  "newPassword": "NewPassword123"
}

Response (200):
{
  "message": "Password reset successfully"
}
```

---

## User Management Module
**Prefix:** `/api/v1/users`

### Endpoints

#### 1. Get Current User Profile
```
GET /users/me
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "id": "123",
  "email": "patient@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "profileImageUrl": "https://...",
  "role": "Patient",
  "isActive": true,
  "createdAt": "2026-01-01T10:00:00Z"
}
```

#### 2. Update Profile
```
PUT /users/{userId}
Headers: Authorization: Bearer {accessToken}
Request:
{
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890"
}

Response (200): Updated user object
```

#### 3. Upload Profile Image
```
POST /users/{userId}/profile-image
Headers: 
  Authorization: Bearer {accessToken}
  Content-Type: multipart/form-data

Response (200):
{
  "profileImageUrl": "https://nutricarepro.blob.core.windows.net/..."
}
```

#### 4. Change Password
```
POST /users/{userId}/change-password
Headers: Authorization: Bearer {accessToken}
Request:
{
  "currentPassword": "OldPassword123",
  "newPassword": "NewPassword123"
}

Response (200):
{
  "message": "Password changed successfully"
}
```

#### 5. Deactivate Account
```
DELETE /users/{userId}
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "message": "Account deactivated successfully"
}
```

#### 6. Get Patient Profile (Admin/Nutritionist)
```
GET /users/patients/{patientId}
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "id": "456",
  "email": "patient@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "patientProfile": {
    "dateOfBirth": "1990-05-15",
    "gender": "Male",
    "height": 180,
    "weight": 75,
    "bloodGroup": "O+",
    "medicalHistory": "Diabetes",
    "allergies": "Nuts, Gluten"
  }
}
```

#### 7. Get Nutritionist Profile
```
GET /users/nutritionists/{nutritionistId}

Response (200):
{
  "id": "789",
  "email": "nutritionist@example.com",
  "firstName": "Dr.",
  "lastName": "Smith",
  "nutritionistProfile": {
    "qualification": "MS Nutrition",
    "licenseNumber": "LIC123456",
    "specializations": ["Weight Loss", "Diabetes", "Heart Disease"],
    "experience": 10,
    "consultationFee": 50,
    "isVerified": true,
    "bio": "Expert nutritionist with 10 years experience"
  }
}
```

---

## Patient Profile Module
**Prefix:** `/api/v1/patient-profiles`

### Endpoints

#### 1. Create Patient Profile
```
POST /patient-profiles
Headers: Authorization: Bearer {accessToken}
Request:
{
  "dateOfBirth": "1990-05-15",
  "gender": "Male",
  "height": 180,
  "weight": 75,
  "bloodGroup": "O+",
  "medicalHistory": "Diabetes",
  "allergies": "Nuts",
  "currentMedications": "Insulin",
  "address": "123 Main St",
  "city": "New York",
  "state": "NY",
  "zipCode": "10001"
}

Response (201): Created profile object
```

#### 2. Get Patient Profile
```
GET /patient-profiles/{patientId}
Headers: Authorization: Bearer {accessToken}

Response (200): Patient profile object
```

#### 3. Update Patient Profile
```
PUT /patient-profiles/{patientId}
Headers: Authorization: Bearer {accessToken}
Request: Same as POST

Response (200): Updated profile object
```

---

## Appointment Module
**Prefix:** `/api/v1/appointments`

### Endpoints

#### 1. Get Available Slots
```
GET /appointments/available-slots?nutritionistId=789&date=2026-06-10&duration=30
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "slots": [
    {
      "time": "09:00",
      "available": true
    },
    {
      "time": "09:30",
      "available": true
    },
    {
      "time": "10:00",
      "available": false
    }
  ]
}
```

#### 2. Book Appointment
```
POST /appointments/book
Headers: Authorization: Bearer {accessToken}
Request:
{
  "nutritionistId": "789",
  "appointmentDate": "2026-06-10T09:00:00Z",
  "duration": 30,
  "type": "Initial", // Initial, Follow-up, Video, In-person
  "notes": "First consultation"
}

Response (201):
{
  "id": "APT001",
  "patientId": "123",
  "nutritionistId": "789",
  "appointmentDate": "2026-06-10T09:00:00Z",
  "duration": 30,
  "status": "Scheduled",
  "type": "Initial"
}
```

#### 3. Get My Appointments
```
GET /appointments/my-appointments?status=Scheduled&skip=0&take=10
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "data": [
    {
      "id": "APT001",
      "patientId": "123",
      "nutritionistId": "789",
      "appointmentDate": "2026-06-10T09:00:00Z",
      "status": "Scheduled",
      "nutritionist": {
        "id": "789",
        "firstName": "Dr.",
        "lastName": "Smith"
      }
    }
  ],
  "total": 5
}
```

#### 4. Cancel Appointment
```
DELETE /appointments/{appointmentId}
Headers: Authorization: Bearer {accessToken}
Request:
{
  "cancellationReason": "Schedule conflict"
}

Response (200):
{
  "message": "Appointment cancelled successfully",
  "id": "APT001"
}
```

#### 5. Reschedule Appointment
```
PUT /appointments/{appointmentId}/reschedule
Headers: Authorization: Bearer {accessToken}
Request:
{
  "newAppointmentDate": "2026-06-15T10:00:00Z"
}

Response (200): Updated appointment object
```

#### 6. Mark Appointment as Completed
```
PUT /appointments/{appointmentId}/complete
Headers: Authorization: Bearer {accessToken}
Request:
{
  "notes": "Patient doing well, continue current diet plan"
}

Response (200): Updated appointment object
```

#### 7. Get Appointment Details
```
GET /appointments/{appointmentId}
Headers: Authorization: Bearer {accessToken}

Response (200): Full appointment object with patient and nutritionist details
```

---

## Diet Plan Module
**Prefix:** `/api/v1/diet-plans`

### Endpoints

#### 1. Create Diet Plan
```
POST /diet-plans
Headers: Authorization: Bearer {accessToken}
Request:
{
  "patientId": "123",
  "title": "Weight Loss Plan",
  "description": "Balanced diet for healthy weight loss",
  "duration": 30,
  "startDate": "2026-06-03",
  "endDate": "2026-07-03",
  "objective": "Weight Loss",
  "dailyCalories": 1800,
  "protein": 150,
  "carbs": 180,
  "fats": 60,
  "meals": [
    {
      "mealType": "Breakfast",
      "time": "08:00",
      "items": ["Oatmeal", "Berries", "Greek Yogurt"],
      "calories": 400
    },
    {
      "mealType": "Lunch",
      "time": "12:30",
      "items": ["Grilled Chicken", "Brown Rice", "Vegetables"],
      "calories": 600
    }
  ]
}

Response (201): Created diet plan object
```

#### 2. Get My Diet Plans
```
GET /diet-plans/my-plans?status=Active&skip=0&take=10
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "data": [
    {
      "id": "DP001",
      "title": "Weight Loss Plan",
      "startDate": "2026-06-03",
      "endDate": "2026-07-03",
      "status": "Active",
      "dailyCalories": 1800
    }
  ],
  "total": 3
}
```

#### 3. Get Diet Plan Details
```
GET /diet-plans/{dietPlanId}
Headers: Authorization: Bearer {accessToken}

Response (200): Full diet plan with meals
```

#### 4. Update Diet Plan
```
PUT /diet-plans/{dietPlanId}
Headers: Authorization: Bearer {accessToken}
Request: Same structure as POST

Response (200): Updated diet plan object
```

#### 5. Download Diet Plan
```
GET /diet-plans/{dietPlanId}/download
Headers: Authorization: Bearer {accessToken}

Response: PDF file download
Content-Type: application/pdf
```

#### 6. Mark Diet Plan as Completed
```
PUT /diet-plans/{dietPlanId}/complete
Headers: Authorization: Bearer {accessToken}

Response (200): Updated diet plan
```

---

## Progress Tracking Module
**Prefix:** `/api/v1/progress`

### Endpoints

#### 1. Record Progress
```
POST /progress
Headers: Authorization: Bearer {accessToken}
Request:
{
  "recordDate": "2026-06-02",
  "weight": 72.5,
  "height": 180,
  "waistCircumference": 85,
  "chestCircumference": 95,
  "energyLevel": 8,
  "sleepQuality": 7,
  "exerciseMinutes": 60,
  "waterIntake": 2.5,
  "notes": "Feeling great, increased exercise"
}

Response (201): Created progress record
```

#### 2. Get Progress Records
```
GET /progress?skip=0&take=30
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "data": [
    {
      "id": "PR001",
      "recordDate": "2026-06-02",
      "weight": 72.5,
      "height": 180,
      "energyLevel": 8,
      "sleepQuality": 7
    }
  ],
  "total": 45
}
```

#### 3. Get Progress Summary
```
GET /progress/summary?startDate=2026-01-01&endDate=2026-06-02
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "startWeight": 80,
  "currentWeight": 72.5,
  "weightLoss": 7.5,
  "weightLossPercentage": 9.375,
  "averageEnergyLevel": 7.5,
  "averageSleepQuality": 7.2,
  "totalRecords": 45,
  "progressChart": [...]
}
```

#### 4. Get Progress Visualization Data
```
GET /progress/chart?metric=weight&startDate=2026-01-01&endDate=2026-06-02
Headers: Authorization: Bearer {accessToken}

Response (200): Time-series data for charting
```

---

## Medical Reports Module
**Prefix:** `/api/v1/medical-reports`

### Endpoints

#### 1. Upload Medical Report
```
POST /medical-reports
Headers: 
  Authorization: Bearer {accessToken}
  Content-Type: multipart/form-data

Form Data:
  file: [binary file]
  reportType: "Blood Test"
  reportDate: "2026-05-20"
  description: "Annual checkup results"

Response (201):
{
  "id": "MR001",
  "reportType": "Blood Test",
  "reportDate": "2026-05-20",
  "fileUrl": "https://nutricarepro.blob.core.windows.net/...",
  "fileName": "blood_test_2026.pdf",
  "uploadDate": "2026-06-02T10:00:00Z"
}
```

#### 2. Get Medical Reports
```
GET /medical-reports?skip=0&take=20
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "data": [
    {
      "id": "MR001",
      "reportType": "Blood Test",
      "reportDate": "2026-05-20",
      "fileUrl": "https://...",
      "uploadDate": "2026-06-02T10:00:00Z"
    }
  ],
  "total": 12
}
```

#### 3. Share Report with Nutritionist
```
PUT /medical-reports/{reportId}/share
Headers: Authorization: Bearer {accessToken}
Request:
{
  "nutritionistId": "789",
  "message": "Please review my latest test results"
}

Response (200): Updated report with sharing info
```

#### 4. Download Medical Report
```
GET /medical-reports/{reportId}/download
Headers: Authorization: Bearer {accessToken}

Response: File download
```

#### 5. Delete Medical Report
```
DELETE /medical-reports/{reportId}
Headers: Authorization: Bearer {accessToken}

Response (200): Deletion confirmation
```

---

## Payment Module
**Prefix:** `/api/v1/payments`

### Endpoints

#### 1. Create Payment Intent
```
POST /payments/create-intent
Headers: Authorization: Bearer {accessToken}
Request:
{
  "amount": 5000,
  "currency": "USD",
  "paymentMethod": "Stripe", // Stripe, JazzCash, EasyPaisa
  "description": "Consultation fee",
  "relatedToType": "Appointment",
  "relatedToId": "APT001"
}

Response (201):
{
  "clientSecret": "pi_xxx_secret_xxx",
  "paymentMethodId": "pm_xxx",
  "amount": 5000
}
```

#### 2. Confirm Payment
```
POST /payments/confirm
Headers: Authorization: Bearer {accessToken}
Request:
{
  "paymentIntentId": "pi_xxx",
  "paymentMethodId": "pm_xxx"
}

Response (200):
{
  "id": "PAY001",
  "status": "Completed",
  "amount": 5000,
  "transactionId": "txn_xxx",
  "completedAt": "2026-06-02T10:15:00Z"
}
```

#### 3. Get Payment History
```
GET /payments/history?status=Completed&skip=0&take=20
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "data": [
    {
      "id": "PAY001",
      "amount": 5000,
      "currency": "USD",
      "status": "Completed",
      "paymentMethod": "Stripe",
      "transactionId": "txn_xxx",
      "createdAt": "2026-06-02T10:15:00Z"
    }
  ],
  "total": 15
}
```

#### 4. Refund Payment
```
POST /payments/{paymentId}/refund
Headers: Authorization: Bearer {accessToken}
Request:
{
  "reason": "Appointment cancelled"
}

Response (200):
{
  "id": "PAY001",
  "status": "Refunded",
  "refundedAmount": 5000,
  "refundedAt": "2026-06-02T11:00:00Z"
}
```

---

## Chat Module
**Prefix:** `/api/v1/chat` + SignalR Hub

### REST Endpoints

#### 1. Get Chat History
```
GET /chat/history/{recipientId}?skip=0&take=50
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "data": [
    {
      "id": "CH001",
      "senderId": "123",
      "recipientId": "789",
      "message": "Hi, I wanted to discuss my diet plan",
      "fileUrl": null,
      "isRead": true,
      "createdAt": "2026-06-02T09:30:00Z"
    }
  ],
  "total": 150
}
```

#### 2. Mark Messages as Read
```
PUT /chat/mark-read
Headers: Authorization: Bearer {accessToken}
Request:
{
  "senderId": "789"
}

Response (200):
{
  "message": "Messages marked as read"
}
```

### SignalR Hub: ChatHub

#### Hub Methods

```csharp
// Client calls these methods:
1. SendMessage(message: string, recipientId: string, file?: File)
2. TypingIndicator(recipientId: string)
3. JoinChat(recipientId: string)
4. LeaveChat(recipientId: string)

// Server sends these events:
1. ReceiveMessage(senderId, message, timestamp)
2. UserTyping(senderId, isTyping)
3. UserOnline(userId)
4. UserOffline(userId)
```

---

## Video Consultation Module
**Prefix:** `/api/v1/video-consultations` + SignalR Hub

### Endpoints

#### 1. Generate Video Room
```
POST /video-consultations/{appointmentId}/generate-room
Headers: Authorization: Bearer {accessToken}

Response (201):
{
  "id": "VC001",
  "roomName": "nutricare-2026-06-10-apt001",
  "joinToken": "token_xxx_xxx",
  "expiresAt": "2026-06-10T09:30:00Z"
}
```

#### 2. Get Video Consultation Details
```
GET /video-consultations/{consultationId}
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "id": "VC001",
  "appointmentId": "APT001",
  "roomName": "nutricare-2026-06-10-apt001",
  "callStartTime": "2026-06-10T09:00:00Z",
  "callEndTime": "2026-06-10T09:30:00Z",
  "status": "Completed",
  "duration": 1800,
  "recordingUrl": "https://..."
}
```

#### 3. End Video Consultation
```
PUT /video-consultations/{consultationId}/end
Headers: Authorization: Bearer {accessToken}
Request:
{
  "notes": "Consultation completed successfully"
}

Response (200): Updated consultation object
```

---

## Blog Module
**Prefix:** `/api/v1/blogs`

### Endpoints

#### 1. Create Blog Post
```
POST /blogs
Headers: Authorization: Bearer {accessToken}
Request:
{
  "title": "10 Tips for Healthy Weight Loss",
  "slug": "10-tips-healthy-weight-loss",
  "content": "Markdown content here...",
  "category": "Weight Loss",
  "tags": ["health", "nutrition", "weight-loss"],
  "featuredImageUrl": "https://..."
}

Response (201): Created blog post
```

#### 2. Get All Blog Posts
```
GET /blogs?category=Weight Loss&tags=health&skip=0&take=10
Headers: (Optional) Authorization: Bearer {accessToken}

Response (200):
{
  "data": [
    {
      "id": "BL001",
      "title": "10 Tips for Healthy Weight Loss",
      "slug": "10-tips-healthy-weight-loss",
      "author": "Dr. Smith",
      "excerpt": "First 150 characters...",
      "viewCount": 1250,
      "publishedDate": "2026-05-15T10:00:00Z"
    }
  ],
  "total": 45
}
```

#### 3. Get Blog Post
```
GET /blogs/{slug}

Response (200): Full blog post with comments
```

#### 4. Update Blog Post
```
PUT /blogs/{blogId}
Headers: Authorization: Bearer {accessToken}
Request: Same as POST

Response (200): Updated blog post
```

#### 5. Publish Blog Post
```
PUT /blogs/{blogId}/publish
Headers: Authorization: Bearer {accessToken}

Response (200): Published blog post
```

#### 6. Add Comment
```
POST /blogs/{blogId}/comments
Headers: Authorization: Bearer {accessToken}
Request:
{
  "comment": "Great article! Very helpful."
}

Response (201):
{
  "id": "BC001",
  "blogPostId": "BL001",
  "userId": "123",
  "comment": "Great article! Very helpful.",
  "status": "Pending",
  "createdAt": "2026-06-02T10:00:00Z"
}
```

#### 7. Get Blog Comments
```
GET /blogs/{blogId}/comments?status=Approved&skip=0&take=20

Response (200): Paginated comments
```

#### 8. Like Blog Post
```
POST /blogs/{blogId}/like
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "likes": 125,
  "userLiked": true
}
```

---

## Notification Module
**Prefix:** `/api/v1/notifications`

### Endpoints

#### 1. Get Notifications
```
GET /notifications?isRead=false&skip=0&take=20
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "data": [
    {
      "id": "NOT001",
      "type": "Appointment",
      "title": "Appointment Reminder",
      "message": "Your appointment with Dr. Smith is in 1 hour",
      "isRead": false,
      "createdAt": "2026-06-02T08:00:00Z"
    }
  ],
  "total": 12,
  "unreadCount": 5
}
```

#### 2. Mark as Read
```
PUT /notifications/{notificationId}/read
Headers: Authorization: Bearer {accessToken}

Response (200): Updated notification
```

#### 3. Mark All as Read
```
PUT /notifications/mark-all-read
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "message": "All notifications marked as read"
}
```

#### 4. Delete Notification
```
DELETE /notifications/{notificationId}
Headers: Authorization: Bearer {accessToken}

Response (200): Deletion confirmation
```

---

## Admin Module
**Prefix:** `/api/v1/admin`

### Endpoints

#### 1. Get Dashboard Stats
```
GET /admin/dashboard/stats
Headers: Authorization: Bearer {accessToken}

Response (200):
{
  "totalUsers": 1250,
  "totalPatients": 1000,
  "totalNutritionists": 150,
  "totalAppointments": 5000,
  "totalRevenue": 150000,
  "appointmentsThisMonth": 450,
  "newUsersThisMonth": 120
}
```

#### 2. Get All Users (Admin Only)
```
GET /admin/users?role=Patient&status=Active&skip=0&take=20
Headers: Authorization: Bearer {accessToken}

Response (200): Paginated users list
```

#### 3. Verify Nutritionist
```
PUT /admin/nutritionists/{nutritionistId}/verify
Headers: Authorization: Bearer {accessToken}

Response (200): Updated nutritionist with verified flag
```

#### 4. Get System Reports
```
GET /admin/reports?type=Revenue&startDate=2026-01-01&endDate=2026-06-02
Headers: Authorization: Bearer {accessToken}

Response (200): Report data
```

---

## Response Format

### Success Response
```json
{
  "success": true,
  "data": {...},
  "message": "Operation completed successfully"
}
```

### Error Response
```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid request data",
    "details": [
      {
        "field": "email",
        "message": "Email is invalid"
      }
    ]
  }
}
```

### Paginated Response
```json
{
  "success": true,
  "data": [...],
  "pagination": {
    "skip": 0,
    "take": 10,
    "total": 100,
    "pageNumber": 1,
    "totalPages": 10
  }
}
```

---

## HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | OK - Successful GET, PUT |
| 201 | Created - Successful POST |
| 204 | No Content - Successful DELETE |
| 400 | Bad Request - Validation error |
| 401 | Unauthorized - Missing/Invalid token |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource doesn't exist |
| 409 | Conflict - Resource already exists |
| 500 | Internal Server Error |
| 503 | Service Unavailable |

---

## Rate Limiting

- **Default:** 100 requests per minute per user
- **Authentication:** 5 login attempts per minute
- **Public Endpoints:** 1000 requests per minute per IP
- **Response Headers:** 
  - `X-RateLimit-Limit`
  - `X-RateLimit-Remaining`
  - `X-RateLimit-Reset`

