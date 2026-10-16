export type Role = "Patient" | "Nutritionist" | "Admin";
export type User = { id: number; email: string; firstName: string; lastName: string; role: Role; isEmailVerified: boolean; isActive: boolean };
export type ApiEnvelope<T> = { success: boolean; message?: string; data: T; errors?: Record<string, string[]> | string[]; statusCode?: number };
export type Tokens = { accessToken: string; refreshToken: string };
export const dashboardPath = (role?: string) => role === "Admin" ? "/admin" : role === "Nutritionist" ? "/nutritionist" : "/patient";
