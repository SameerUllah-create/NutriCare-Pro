export type ApiResponse<T> = {
  success: boolean;
  message: string;
  data: T;
  statusCode: number;
};

export type PatientDashboard = {
  overview: PatientDashboardOverview;
  upcomingAppointments: PatientDashboardAppointment[];
  dietPlanSummary?: PatientDashboardDietPlan;
  weightProgress: PatientDashboardWeightProgress;
  recentReports: PatientDashboardReport[];
  notifications: PatientDashboardNotification[];
  messages: PatientDashboardMessageSummary;
};

export type PatientDashboardOverview = {
  patientName: string;
  currentWeight: number;
  targetWeight?: number;
  upcomingAppointmentCount: number;
  unreadNotificationCount: number;
  unreadMessageCount: number;
  latestBMI?: number;
};

export type PatientDashboardAppointment = {
  id: number;
  appointmentDateTime: string;
  duration: number;
  status: string;
  appointmentType: string;
  nutritionistName: string;
};

export type PatientDashboardDietPlan = {
  id: number;
  title: string;
  status: string;
  dailyCalories: number;
  proteinGrams: number;
  carbsGrams: number;
  fatsGrams: number;
  waterIntakeLiters?: number;
  supplements?: string;
  instructions?: string;
  startDate: string;
  endDate: string;
  daysRemaining: number;
  averageCompliancePercentage: number;
};

export type PatientDashboardWeightProgress = {
  startWeight?: number;
  currentWeight?: number;
  weightChange?: number;
  latestBMI?: number;
  points: PatientDashboardWeightPoint[];
};

export type PatientDashboardWeightPoint = {
  date: string;
  weight: number;
  bmi?: number;
};

export type PatientDashboardReport = {
  id: number;
  reportType: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  status: string;
  uploadedAt: string;
};

export type PatientDashboardNotification = {
  id: number;
  type: string;
  title: string;
  message: string;
  isRead: boolean;
  channel: string;
  status: string;
  createdAt: string;
};

export type PatientDashboardMessageSummary = {
  totalUnread: number;
  recentMessages: PatientDashboardMessage[];
};

export type PatientDashboardMessage = {
  id: number;
  senderId: number;
  senderName: string;
  messageBody: string;
  messageType: string;
  isRead: boolean;
  createdAt: string;
};
