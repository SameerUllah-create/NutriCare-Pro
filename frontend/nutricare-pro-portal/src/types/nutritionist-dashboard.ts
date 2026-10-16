export type ApiResponse<T> = {
  success: boolean;
  message: string;
  data: T;
  statusCode: number;
};

export type NutritionistDashboard = {
  overview: NutritionistDashboardOverview;
  todaysAppointments: NutritionistAppointment[];
  pendingConsultations: NutritionistAppointment[];
  patientProgressOverview: NutritionistPatientProgress[];
  revenueSummary: NutritionistRevenueSummary;
  recentMessages: NutritionistMessageSummary;
};

export type NutritionistDashboardOverview = {
  nutritionistName: string;
  todaysAppointmentCount: number;
  pendingConsultationCount: number;
  activePatientCount: number;
  unreadMessageCount: number;
  monthRevenue: number;
  ratingAverage: number;
};

export type NutritionistAppointment = {
  id: number;
  patientId: number;
  patientName: string;
  appointmentDateTime: string;
  duration: number;
  status: string;
  appointmentType: string;
  notes?: string;
};

export type NutritionistPatientProgress = {
  patientId: number;
  patientName: string;
  startWeight?: number;
  currentWeight?: number;
  weightChange?: number;
  latestBMI?: number;
  lastRecordDate?: string;
  progressRecordCount: number;
};

export type NutritionistRevenueSummary = {
  todayRevenue: number;
  weekRevenue: number;
  monthRevenue: number;
  totalRevenue: number;
  refundedAmount: number;
  revenueByDay: NutritionistRevenuePoint[];
};

export type NutritionistRevenuePoint = {
  date: string;
  amount: number;
};

export type NutritionistMessageSummary = {
  totalUnread: number;
  messages: NutritionistMessage[];
};

export type NutritionistMessage = {
  id: number;
  senderId: number;
  senderName: string;
  messageBody: string;
  messageType: string;
  isRead: boolean;
  createdAt: string;
};
