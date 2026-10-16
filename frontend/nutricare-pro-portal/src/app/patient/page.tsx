"use client";

import {
  Bell,
  CalendarClock,
  Droplets,
  FileText,
  Loader2,
  MessageSquare,
  RefreshCw,
  Scale,
  Stethoscope,
  Target,
  Utensils
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { getPatientDashboard } from "@/services/patientDashboard";
import type {
  PatientDashboard,
  PatientDashboardAppointment,
  PatientDashboardNotification,
  PatientDashboardReport
} from "@/types/patient-dashboard";

export default function PatientDashboardPage() {
  const [dashboard, setDashboard] = useState<PatientDashboard | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    void loadDashboard();
  }, []);

  const maxWeight = useMemo(() => {
    const points = dashboard?.weightProgress.points ?? [];
    return Math.max(...points.map((point) => point.weight), 1);
  }, [dashboard]);

  async function loadDashboard() {
    setLoading(true);
    setError("");

    try {
      const data = await getPatientDashboard();
      setDashboard(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load patient dashboard");
    } finally {
      setLoading(false);
    }
  }


  const overview = dashboard?.overview;
  const plan = dashboard?.dietPlanSummary;
  const weight = dashboard?.weightProgress;

  return (
    <main className="page">
      <header className="topbar">
        <div className="brand">
          <Stethoscope size={22} />
          NutriCare Pro Patient
        </div>
        <div className="actions">
          <button className="icon-button" onClick={loadDashboard} title="Refresh dashboard" type="button">
            {loading ? <Loader2 size={17} /> : <RefreshCw size={17} />}
          </button>
        </div>
      </header>

      <div className="patient-shell">
        <aside className="grid">

          <section className="panel profile-panel">
            <div className="avatar">{initials(overview?.patientName)}</div>
            <div>
              <h2>{overview?.patientName || "Patient Dashboard"}</h2>
              <div className="meta">
                {overview?.currentWeight ?? 0} kg current
                {overview?.targetWeight ? ` | ${overview.targetWeight} kg target` : ""}
              </div>
            </div>
          </section>
        </aside>

        <section className="grid">
          {error ? <div className="error">{error}</div> : null}

          <section className="stats-grid">
            <Stat icon={<CalendarClock size={18} />} label="Upcoming" value={overview?.upcomingAppointmentCount ?? 0} detail="appointments" />
            <Stat icon={<Scale size={18} />} label="Weight" value={`${weight?.currentWeight ?? overview?.currentWeight ?? 0} kg`} detail={formatChange(weight?.weightChange)} />
            <Stat icon={<Target size={18} />} label="BMI" value={overview?.latestBMI?.toFixed(1) ?? "-"} detail="latest reading" />
            <Stat icon={<MessageSquare size={18} />} label="Unread" value={(overview?.unreadMessageCount ?? 0) + (overview?.unreadNotificationCount ?? 0)} detail="messages and alerts" />
          </section>

          <section className="patient-dashboard-grid">
            <Card title="Upcoming Appointments" icon={<CalendarClock size={17} />}>
              <div className="list">
                {dashboard?.upcomingAppointments.length === 0 ? <Empty text="No upcoming appointments." /> : null}
                {dashboard?.upcomingAppointments.map((appointment) => (
                  <AppointmentRow appointment={appointment} key={appointment.id} />
                ))}
              </div>
            </Card>

            <Card title="Diet Plan Summary" icon={<Utensils size={17} />}>
              {plan ? (
                <div className="diet-summary">
                  <div>
                    <strong>{plan.title}</strong>
                    <div className="meta">{plan.daysRemaining} days remaining | {plan.averageCompliancePercentage}% compliance</div>
                  </div>
                  <div className="macro-grid">
                    <Macro label="Calories" value={plan.dailyCalories} />
                    <Macro label="Protein" value={`${plan.proteinGrams}g`} />
                    <Macro label="Carbs" value={`${plan.carbsGrams}g`} />
                    <Macro label="Fats" value={`${plan.fatsGrams}g`} />
                  </div>
                  <div className="metric-row compact">
                    <span>Compliance</span>
                    <div className="metric-track">
                      <div className="metric-fill" style={{ width: `${Math.min(plan.averageCompliancePercentage, 100)}%` }} />
                    </div>
                    <strong>{plan.averageCompliancePercentage}%</strong>
                  </div>
                  <div className="water-pill">
                    <Droplets size={15} />
                    {plan.waterIntakeLiters ?? 0} L water target
                  </div>
                </div>
              ) : (
                <Empty text="No active diet plan assigned." />
              )}
            </Card>

            <Card title="Weight Progress" icon={<Scale size={17} />}>
              <div className="weight-chart">
                {(weight?.points.length ?? 0) === 0 ? <Empty text="No weight tracking records yet." /> : null}
                {weight?.points.map((point) => (
                  <div className="weight-column" key={`${point.date}-${point.weight}`}>
                    <div className="weight-track">
                      <div className="weight-fill" style={{ height: `${Math.max((point.weight / maxWeight) * 100, 4)}%` }} />
                    </div>
                    <span>{new Date(point.date).toLocaleDateString(undefined, { month: "short", day: "numeric" })}</span>
                    <strong>{point.weight}</strong>
                  </div>
                ))}
              </div>
            </Card>

            <Card title="Recent Reports" icon={<FileText size={17} />}>
              <div className="list">
                {dashboard?.recentReports.length === 0 ? <Empty text="No reports uploaded." /> : null}
                {dashboard?.recentReports.map((report) => (
                  <ReportRow report={report} key={report.id} />
                ))}
              </div>
            </Card>

            <Card title="Notifications" icon={<Bell size={17} />}>
              <div className="list">
                {dashboard?.notifications.length === 0 ? <Empty text="No notifications." /> : null}
                {dashboard?.notifications.map((notification) => (
                  <NotificationRow notification={notification} key={notification.id} />
                ))}
              </div>
            </Card>

            <Card title="Messages" icon={<MessageSquare size={17} />}>
              <div className="list">
                <div className="notice">{dashboard?.messages.totalUnread ?? 0} unread messages</div>
                {dashboard?.messages.recentMessages.length === 0 ? <Empty text="No recent messages." /> : null}
                {dashboard?.messages.recentMessages.map((message) => (
                  <article className="table-row" key={message.id}>
                    <div>
                      <strong>{message.senderName || `User #${message.senderId}`}</strong>
                      <div className="meta">{message.messageBody}</div>
                    </div>
                    <span className={`status ${message.isRead ? "Completed" : "InProgress"}`}>
                      {message.isRead ? "Read" : "Unread"}
                    </span>
                  </article>
                ))}
              </div>
            </Card>
          </section>
        </section>
      </div>
    </main>
  );
}

function Card({ title, icon, children }: { title: string; icon: ReactNode; children: ReactNode }) {
  return (
    <section className="panel dashboard-card">
      <div className="card-heading">
        <div>
          {icon}
          <h2>{title}</h2>
        </div>
      </div>
      {children}
    </section>
  );
}

function Stat({ icon, label, value, detail }: { icon: ReactNode; label: string; value: number | string; detail: string }) {
  return (
    <article className="stat">
      <div className="stat-icon">{icon}</div>
      <div>
        <div className="meta">{label}</div>
        <strong>{value}</strong>
        <span>{detail}</span>
      </div>
    </article>
  );
}

function AppointmentRow({ appointment }: { appointment: PatientDashboardAppointment }) {
  return (
    <article className="table-row">
      <div>
        <strong>{appointment.nutritionistName || "Nutritionist"}</strong>
        <div className="meta">
          {new Date(appointment.appointmentDateTime).toLocaleString()} | {appointment.duration} min | {appointment.appointmentType}
        </div>
      </div>
      <span className={`status ${appointment.status}`}>{appointment.status}</span>
    </article>
  );
}

function ReportRow({ report }: { report: PatientDashboardReport }) {
  return (
    <article className="table-row">
      <div>
        <strong>{report.originalFileName}</strong>
        <div className="meta">{report.reportType} | {formatBytes(report.fileSizeBytes)} | {new Date(report.uploadedAt).toLocaleDateString()}</div>
      </div>
      <span className={`status ${report.status}`}>{report.status}</span>
    </article>
  );
}

function NotificationRow({ notification }: { notification: PatientDashboardNotification }) {
  return (
    <article className="table-row">
      <div>
        <strong>{notification.title}</strong>
        <div className="meta">{notification.message}</div>
      </div>
      <span className={`status ${notification.isRead ? "Completed" : "Scheduled"}`}>
        {notification.isRead ? "Read" : "New"}
      </span>
    </article>
  );
}

function Macro({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="macro">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function Empty({ text }: { text: string }) {
  return <div className="notice">{text}</div>;
}

function initials(name?: string) {
  if (!name) {
    return "PT";
  }

  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
}

function formatChange(value?: number) {
  if (value === undefined || value === null) {
    return "no trend yet";
  }

  return `${value > 0 ? "+" : ""}${value.toFixed(1)} kg change`;
}

function formatBytes(bytes: number) {
  if (bytes < 1024 * 1024) {
    return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
