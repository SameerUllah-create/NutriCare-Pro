import type {
  JoinVideoConsultation,
  ScheduleVideoConsultationInput,
  VideoConsultation
} from "@/types/video-consultation";

import { api as request } from "@/lib/api";

export function scheduleVideoConsultation(input: ScheduleVideoConsultationInput) {
  return request<VideoConsultation>("/api/VideoConsultations/schedule", {
    method: "POST",
    body: JSON.stringify(input)
  });
}

export function getVideoConsultationHistory(status?: string) {
  const query = status ? `?status=${encodeURIComponent(status)}` : "";
  return request<VideoConsultation[]>(`/api/VideoConsultations/history${query}`);
}

export function joinVideoConsultation(id: number) {
  return request<JoinVideoConsultation>(`/api/VideoConsultations/${id}/join`, {
    method: "POST"
  });
}

export function completeVideoConsultation(id: number, notes?: string, recordingUrl?: string) {
  return request<VideoConsultation>(`/api/VideoConsultations/${id}/complete`, {
    method: "PUT",
    body: JSON.stringify({ notes, recordingUrl })
  });
}
