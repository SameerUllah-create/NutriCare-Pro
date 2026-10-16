export type ApiResponse<T> = {
  success: boolean;
  message: string;
  data: T;
  statusCode: number;
};

export type VideoConsultation = {
  id: number;
  appointmentId: number;
  provider: string;
  providerMeetingId?: string;
  roomName: string;
  meetingUrl?: string;
  hostUrl?: string;
  scheduledStartTime: string;
  scheduledEndTime: string;
  callStartTime?: string;
  callEndTime?: string;
  duration?: number;
  recordingUrl?: string;
  recordingStatus?: string;
  status: string;
  notes?: string;
  createdAt: string;
  updatedAt: string;
};

export type JoinVideoConsultation = {
  consultationId: number;
  appointmentId: number;
  provider: string;
  meetingUrl: string;
  hostUrl?: string;
  status: string;
  isHost: boolean;
  scheduledStartTime: string;
  scheduledEndTime: string;
};

export type ScheduleVideoConsultationInput = {
  appointmentId: number;
  provider: "GoogleMeet" | "Zoom";
  notes?: string;
};
