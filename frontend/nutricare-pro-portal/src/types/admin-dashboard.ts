export type ApiResponse<T> = {
  success: boolean;
  message: string;
  data: T;
  statusCode: number;
};

export type AdminSummary = {
  totalUsers: number;
  activeUsers: number;
  totalPatients: number;
  totalNutritionists: number;
  verifiedNutritionists: number;
  totalAppointments: number;
  upcomingAppointments: number;
  completedAppointments: number;
  publishedBlogs: number;
  totalRevenue: number;
  refundedRevenue: number;
  netRevenue: number;
};

export type AdminChartPoint = {
  label: string;
  date?: string;
  value: number;
};

export type RevenueAnalytics = {
  fromDate: string;
  toDate: string;
  grossRevenue: number;
  refundedRevenue: number;
  netRevenue: number;
  revenueByDay: AdminChartPoint[];
  revenueByProvider: AdminChartPoint[];
};

export type AppointmentAnalytics = {
  fromDate: string;
  toDate: string;
  totalAppointments: number;
  appointmentsByDay: AdminChartPoint[];
  appointmentsByStatus: AdminChartPoint[];
  appointmentsByType: AdminChartPoint[];
};

export type AdminUser = {
  id: number;
  email: string;
  fullName: string;
  role: string;
  isActive: boolean;
  isEmailVerified: boolean;
  createdAt: string;
  lastLoginAt?: string;
};

export type AdminPatient = {
  id: number;
  userId: number;
  fullName: string;
  email: string;
  currentWeight: number;
  targetWeight?: number;
  city?: string;
  appointmentCount: number;
  createdAt: string;
};

export type AdminNutritionist = {
  id: number;
  userId: number;
  fullName: string;
  email: string;
  licenseNumber: string;
  qualification?: string;
  consultationFee: number;
  isVerified: boolean;
  ratingAverage: number;
  appointmentCount: number;
  createdAt: string;
};

export type Blog = {
  rowVersion: string;
  id: number;
  authorUserId: number;
  authorName: string;
  title: string;
  slug: string;
  summary: string;
  content: string;
  coverImageUrl?: string;
  category: string;
  tags: string;
  status: "Draft" | "Published";
  publishedAt?: string;
  createdAt: string;
  updatedAt: string;
};

export type UpsertBlog = {
  expectedVersion?: string;
  title: string;
  slug?: string;
  summary: string;
  content: string;
  coverImageUrl?: string;
  category: string;
  tags: string;
  status: "Draft" | "Published";
};

export type AuditLog = {
  id: number;
  userId?: number;
  action: string;
  entityType: string;
  entityId: number;
  description?: string;
  ipAddress?: string;
  statusCode?: number;
  isSuccess: boolean;
  errorMessage?: string;
  createdAt: string;
};
