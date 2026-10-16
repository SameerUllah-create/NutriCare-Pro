"use client";

import {
  CalendarCheck,
  Clock3,
  DollarSign,
  Loader2,
  MessageSquare,
  RefreshCw,
  Scale,
  Star,
  Stethoscope,
  TrendingUp,
  Users
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { getNutritionistDashboard } from "@/services/nutritionistDashboard";
import type {
  NutritionistAppointment,
  NutritionistDashboard,
  NutritionistPatientProgress,
  NutritionistRevenuePoint
} from "@/types/nutritionist-dashboard";

export default function NutritionistDashboardPage() {
  const [dashboard, setDashboard] = useState<NutritionistDashboard | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    void loadDashboard();
  }, []);

  const maxRevenue = useMemo(() => {
    const points = dashboard?.revenueSummary.revenueByDay ?? [];
    return Math.max(...points.map((point) => point.amount), 1);
  }, [dashboard]);

  async function loadDashboard() {
    setLoading(true);
    setError("");

    try {
      setDashboard(await getNutritionistDashboard());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load nutritionist dashboard");
    } finally {
      setLoading(false);
    }
  }


  const overview = dashboard?.overview;
  const revenue = dashboard?.revenueSummary;

  return (
    <main className="page">
      <header className="topbar">
        <div className="brand">
          <Stethoscope size={22} />
          NutriCare Pro Nutritionist
        </div>
        <button className="icon-button" onClick={loadDashboard} title="Refresh dashboard" type="button">
          {loading ? <Loader2 size={17} /> : <RefreshCw size={17} />}
        </button>
      </header>

      <div className="patient-shell">
        <aside className="grid">

          <section className="panel profile-panel">
            <div className="avatar">{initials(overview?.nutritionistName)}</div>
            <div>
              <h2>{overview?.nutritionistName || "Nutritionist Dashboard"}</h2>
              <div className="meta">
                {overview?.activePatientCount ?? 0} active patients | {overview?.ratingAverage?.toFixed(1) ?? "0.0"} rating
              </div>
            </div>
          </section>
        </aside>

        <section className="grid">
          {error ? <div className="error">{error}</div> : null}

          <section className="stats-grid">
            <Stat icon={<CalendarCheck size={18} />} label="Today" value={overview?.todaysAppointmentCount ?? 0} detail="appointments" />
            <Stat icon={<Clock3 size={18} />} label="Pending" value={overview?.pendingConsultationCount ?? 0} detail="consultations" />
            <Stat icon={<DollarSign size={18} />} label="Month Revenue" value={formatMoney(overview?.monthRevenue ?? 0)} detail="completed payments" />
            <Stat icon={<MessageSquare size={18} />} label="Unread" value={overview?.unreadMessageCount ?? 0} detail="messages" />
          </section>

          <section className="nutritionist-dashboard-grid">
            <Card title="Today's Appointments" icon={<CalendarCheck size={17} />}>
              <div className="list">
                {dashboard?.todaysAppointments.length === 0 ? <Empty text="No appointments scheduled today." /> : null}
                {dashboard?.todaysAppointments.map((appointment) => (
                  <AppointmentRow appointment={appointment} key={appointment.id} />
                ))}
              </div>
            </Card>

            <Card title="Pending Consultations" icon={<Clock3 size={17} />}>
              <div className="list">
                {dashboard?.pendingConsultations.length === 0 ? <Empty text="No pending consultations." /> : null}
                {dashboard?.pendingConsultations.map((appointment) => (
                  <AppointmentRow appointment={appointment} key={appointment.id} />
                ))}
              </div>
            </Card>

            <Card title="Patient Progress Overview" icon={<TrendingUp size={17} />}>
              <div className="list">
                {dashboard?.patientProgressOverview.length === 0 ? <Empty text="No patient progress records yet." /> : null}
                {dashboard?.patientProgressOverview.map((patient) => (
                  <ProgressRow patient={patient} key={patient.patientId} />
                ))}
              </div>
            </Card>

            <Card title="Revenue Summary" icon={<DollarSign size={17} />}>
              <div className="macro-grid">
                <Macro label="Today" value={formatMoney(revenue?.todayRevenue ?? 0)} />
                <Macro label="Week" value={formatMoney(revenue?.weekRevenue ?? 0)} />
                <Macro label="Month" value={formatMoney(revenue?.monthRevenue ?? 0)} />
                <Macro label="Total" value={formatMoney(revenue?.totalRevenue ?? 0)} />
              </div>
              <RevenueChart points={revenue?.revenueByDay ?? []} max={maxRevenue} />
              <div className="water-pill">
                <DollarSign size={15} />
                {formatMoney(revenue?.refundedAmount ?? 0)} refunded
              </div>
            </Card>

            <Card title="Recent Messages" icon={<MessageSquare size={17} />}>
              <div className="list">
                <div className="notice">{dashboard?.recentMessages.totalUnread ?? 0} unread messages</div>
                {dashboard?.recentMessages.messages.length === 0 ? <Empty text="No recent messages." /> : null}
                {dashboard?.recentMessages.messages.map((message) => (
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

            <Card title="Practice Snapshot" icon={<Users size={17} />}>
              <div className="snapshot-grid">
                <Snapshot icon={<Users size={18} />} label="Active Patients" value={overview?.activePatientCount ?? 0} />
                <Snapshot icon={<Star size={18} />} label="Average Rating" value={overview?.ratingAverage?.toFixed(1) ?? "0.0"} />
                <Snapshot icon={<Scale size={18} />} label="Tracked Patients" value={dashboard?.patientProgressOverview.length ?? 0} />
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

function AppointmentRow({ appointment }: { appointment: NutritionistAppointment }) {
  return (
    <article className="table-row">
      <div>
        <strong>{appointment.patientName || `Patient #${appointment.patientId}`}</strong>
        <div className="meta">
          {new Date(appointment.appointmentDateTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })} | {appointment.duration} min | {appointment.appointmentType}
        </div>
      </div>
      <span className={`status ${appointment.status}`}>{appointment.status}</span>
    </article>
  );
}

function ProgressRow({ patient }: { patient: NutritionistPatientProgress }) {
  const change = patient.weightChange ?? 0;

  return (
    <article className="table-row">
      <div>
        <strong>{patient.patientName || `Patient #${patient.patientId}`}</strong>
        <div className="meta">
          {patient.currentWeight ?? "-"} kg current | BMI {patient.latestBMI?.toFixed(1) ?? "-"} | {patient.progressRecordCount} records
        </div>
      </div>
      <span className={`status ${change <= 0 ? "Completed" : "Scheduled"}`}>
        {change > 0 ? "+" : ""}{change.toFixed(1)} kg
      </span>
    </article>
  );
}

function RevenueChart({ points, max }: { points: NutritionistRevenuePoint[]; max: number }) {
  return (
    <div className="mini-bars">
      {points.length === 0 ? <Empty text="No revenue this week." /> : null}
      {points.map((point) => (
        <div className="mini-bar-column" key={`${point.date}-${point.amount}`}>
          <div className="mini-bar-track">
            <div className="mini-bar-fill" style={{ height: `${Math.max((point.amount / max) * 100, 4)}%` }} />
          </div>
          <span>{new Date(point.date).toLocaleDateString(undefined, { weekday: "short" })}</span>
        </div>
      ))}
    </div>
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

function Snapshot({ icon, label, value }: { icon: ReactNode; label: string; value: number | string }) {
  return (
    <div className="snapshot">
      <div className="stat-icon">{icon}</div>
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
      </div>
    </div>
  );
}

function Empty({ text }: { text: string }) {
  return <div className="notice">{text}</div>;
}

function initials(name?: string) {
  if (!name) {
    return "NT";
  }

  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
}

function formatMoney(value: number) {
  return new Intl.NumberFormat("en-US", { currency: "PKR", style: "currency", maximumFractionDigits: 0 }).format(value);
}

