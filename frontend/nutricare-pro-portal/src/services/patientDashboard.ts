import { api } from "@/lib/api";
import type { PatientDashboard } from "@/types/patient-dashboard";
export const getPatientDashboard = () => api<PatientDashboard>("/api/patient-dashboard");
