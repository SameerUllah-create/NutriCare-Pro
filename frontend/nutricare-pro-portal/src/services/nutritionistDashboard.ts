import { api } from "@/lib/api";
import type { NutritionistDashboard } from "@/types/nutritionist-dashboard";
export const getNutritionistDashboard = () => api<NutritionistDashboard>("/api/nutritionist-dashboard");
