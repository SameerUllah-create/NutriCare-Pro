import type {
  AdminNutritionist,
  AdminPatient,
  AdminSummary,
  AdminUser,
  AppointmentAnalytics,
  AuditLog,
  Blog,
  RevenueAnalytics,
  UpsertBlog
} from "@/types/admin-dashboard";

import { api as request } from "@/lib/api";

export function getAdminSummary() {
  return request<AdminSummary>("/api/admin/summary");
}

export function getRevenueAnalytics() {
  return request<RevenueAnalytics>("/api/admin/analytics/revenue");
}

export function getAppointmentAnalytics() {
  return request<AppointmentAnalytics>("/api/admin/analytics/appointments");
}

export function getAdminUsers(role?: string) {
  const query = role ? `?role=${encodeURIComponent(role)}` : "";
  return request<AdminUser[]>(`/api/admin/users${query}`);
}

export function updateUserStatus(id: number, isActive: boolean) {
  return request<AdminUser>(`/api/admin/users/${id}/status`, {
    method: "PUT",
    body: JSON.stringify({ isActive })
  });
}

export function getAdminPatients() {
  return request<AdminPatient[]>("/api/admin/patients");
}

export function getAdminNutritionists() {
  return request<AdminNutritionist[]>("/api/admin/nutritionists");
}

export function updateNutritionistVerification(id: number, isVerified: boolean) {
  return request<AdminNutritionist>(`/api/admin/nutritionists/${id}/verification`, {
    method: "PUT",
    body: JSON.stringify({ isVerified })
  });
}

export function getAdminBlogs() {
  return request<Blog[]>("/api/admin/blogs");
}

export function createBlog(input: UpsertBlog) {
  return request<Blog>("/api/admin/blogs", {
    method: "POST",
    body: JSON.stringify(input)
  });
}

export function updateBlog(id: number, input: UpsertBlog) {
  return request<Blog>(`/api/admin/blogs/${id}`, {
    method: "PUT",
    body: JSON.stringify(input)
  });
}

export function deleteBlog(id: number) {
  return request<boolean>(`/api/admin/blogs/${id}`, {
    method: "DELETE"
  });
}

export function getAuditLogs() {
  return request<AuditLog[]>("/api/admin/audit-logs");
}
