"use client";

import {
  Activity,
  BadgeCheck,
  BookOpen,
  ClipboardList,
  DollarSign,
  FileClock,
  Loader2,
  RefreshCw,
  Shield,
  Users
} from "lucide-react";
import { FormEvent, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  createBlog,
  deleteBlog,
  getAdminBlogs,
  getAdminNutritionists,
  getAdminPatients,
  getAdminSummary,
  getAdminUsers,
  getAppointmentAnalytics,
  getAuditLogs,
  getRevenueAnalytics,
  updateBlog,
  updateUserStatus
} from "@/services/adminDashboard";
import type {
  AdminChartPoint,
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
import Link from "next/link";
import { api, json, ApiError } from "@/lib/api";

const emptyBlog: UpsertBlog = {
  title: "",
  slug: "",
  summary: "",
  content: "",
  coverImageUrl: "",
  category: "General",
  tags: "",
  status: "Draft"
};

export default function AdminDashboardPage() {
  const [tab, setTab] = useState<"overview" | "people" | "blogs" | "audit">("overview");
  const [summary, setSummary] = useState<AdminSummary | null>(null);
  const [revenue, setRevenue] = useState<RevenueAnalytics | null>(null);
  const [appointments, setAppointments] = useState<AppointmentAnalytics | null>(null);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [patients, setPatients] = useState<AdminPatient[]>([]);
  const [nutritionists, setNutritionists] = useState<AdminNutritionist[]>([]);
  const [blogs, setBlogs] = useState<Blog[]>([]);
  const [auditLogs, setAuditLogs] = useState<AuditLog[]>([]);
  const [blogForm, setBlogForm] = useState<UpsertBlog>(emptyBlog);
  const [editingBlogId, setEditingBlogId] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [patientRestricted, setPatientRestricted] = useState(false);
  const [publicationApproval, setPublicationApproval] = useState<number | null>(null);

  useEffect(() => {
    void loadDashboard();
  }, []);

  const maxRevenue = useMemo(() => maxValue(revenue?.revenueByDay), [revenue]);
  const maxAppointment = useMemo(() => maxValue(appointments?.appointmentsByDay), [appointments]);


  async function loadDashboard() {
    setLoading(true);
    setError("");
    setMessage("");

    try {
      const [
        nextSummary,
        nextRevenue,
        nextAppointments,
        nextUsers,
        nextPatients,
        nextNutritionists,
        nextBlogs,
        nextAuditLogs
      ] = await Promise.allSettled([
        getAdminSummary(),
        getRevenueAnalytics(),
        getAppointmentAnalytics(),
        getAdminUsers(),
        getAdminPatients(),
        getAdminNutritionists(),
        getAdminBlogs(),
        getAuditLogs()
      ]);

      if (nextSummary.status === "fulfilled") setSummary(nextSummary.value);
      if (nextRevenue.status === "fulfilled") setRevenue(nextRevenue.value);
      if (nextAppointments.status === "fulfilled") setAppointments(nextAppointments.value);
      if (nextUsers.status === "fulfilled") setUsers(nextUsers.value);
      if (nextPatients.status === "fulfilled") { setPatients(nextPatients.value); setPatientRestricted(false); }
      else if (nextPatients.reason instanceof ApiError && nextPatients.reason.status === 403) setPatientRestricted(true);
      if (nextNutritionists.status === "fulfilled") setNutritionists(nextNutritionists.value);
      if (nextBlogs.status === "fulfilled") setBlogs(nextBlogs.value);
      if (nextAuditLogs.status === "fulfilled") setAuditLogs(nextAuditLogs.value);
      const failures = [nextSummary,nextRevenue,nextAppointments,nextUsers,nextNutritionists,nextBlogs,nextAuditLogs].filter(result => result.status === "rejected");
      if (failures.length) setError("Some dashboard sections could not load. Other available sections are shown. Refresh to retry.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load admin dashboard");
    } finally {
      setLoading(false);
    }
  }

  async function toggleUser(user: AdminUser) {
    try {
      await updateUserStatus(user.id, !user.isActive);
      setMessage(`${user.fullName || user.email} ${user.isActive ? "disabled" : "enabled"}`);
      await loadDashboard();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not update user");
    }
  }

  async function publishBlog(blog: Blog) {
    try {
      await api(`/api/admin/blogs/${blog.id}/publish`, json("POST", { approved: true, expectedVersion: blog.rowVersion }));
      setPublicationApproval(null);
      await loadDashboard();
      setMessage("Reviewed blog published.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not publish article");
    }
  }

  async function saveBlog(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLoading(true);
    setError("");

    try {
      if (editingBlogId) {
        await updateBlog(editingBlogId, { ...blogForm, status: "Draft", expectedVersion: blogs.find(blog => blog.id === editingBlogId)?.rowVersion });
        setMessage("Blog updated");
      } else {
        await createBlog(blogForm);
        setMessage("Blog created");
      }

      setEditingBlogId(null);
      setBlogForm(emptyBlog);
      await loadDashboard();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save blog");
    } finally {
      setLoading(false);
    }
  }

  async function removeBlog(id: number) {
    try {
      await deleteBlog(id);
      setMessage("Blog deleted");
      await loadDashboard();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not delete blog");
    }
  }

  function editBlog(blog: Blog) {
    setEditingBlogId(blog.id);
    setBlogForm({
      title: blog.title,
      slug: blog.slug,
      summary: blog.summary,
      content: blog.content,
      coverImageUrl: blog.coverImageUrl ?? "",
      category: blog.category,
      tags: blog.tags,
      status: "Draft"
    });
    setTab("blogs");
  }

  return (
    <main className="page">
      <header className="topbar">
        <div className="brand">
          <Shield size={22} />
          NutriCare Pro Admin
        </div>
        <div className="actions">
          <button className="icon-button" onClick={loadDashboard} title="Refresh dashboard" type="button">
            {loading ? <Loader2 size={17} /> : <RefreshCw size={17} />}
          </button>
        </div>
      </header>

      <div className="shell">
        <aside className="grid">

          <section className="panel">
            <h2>Views</h2>
            <div className="segmented">
              <button className={tab === "overview" ? "active" : ""} onClick={() => setTab("overview")} type="button">
                <Activity size={16} />
                Overview
              </button>
              <button className={tab === "people" ? "active" : ""} onClick={() => setTab("people")} type="button">
                <Users size={16} />
                People
              </button>
              <button className={tab === "blogs" ? "active" : ""} onClick={() => setTab("blogs")} type="button">
                <BookOpen size={16} />
                Blogs
              </button>
              <button className={tab === "audit" ? "active" : ""} onClick={() => setTab("audit")} type="button">
                <FileClock size={16} />
                Audit
              </button>
            </div>
          </section>
        </aside>

        <section className="grid">
          {message ? <div className="notice">{message}</div> : null}
          {error ? <div className="error">{error}</div> : null}

          {tab === "overview" ? (
            <>
              <section className="stats-grid">
                <Stat icon={<Users size={18} />} label="Users" value={summary?.totalUsers ?? 0} detail={`${summary?.activeUsers ?? 0} active`} />
                <Stat icon={<BadgeCheck size={18} />} label="Nutritionists" value={summary?.totalNutritionists ?? 0} detail={`${summary?.verifiedNutritionists ?? 0} verified`} />
                <Stat icon={<ClipboardList size={18} />} label="Appointments" value={summary?.totalAppointments ?? 0} detail={`${summary?.upcomingAppointments ?? 0} upcoming`} />
                <Stat icon={<DollarSign size={18} />} label="Net Revenue" value={formatMoney(summary?.netRevenue ?? 0)} detail={`${formatMoney(summary?.refundedRevenue ?? 0)} refunded`} />
              </section>

              <section className="panel">
                <h2>Revenue Analytics</h2>
                <BarChart points={revenue?.revenueByDay ?? []} max={maxRevenue} valuePrefix="$" />
              </section>

              <section className="two-column">
                <div className="panel">
                  <h2>Revenue By Provider</h2>
                  <BarList points={revenue?.revenueByProvider ?? []} valuePrefix="$" />
                </div>
                <div className="panel">
                  <h2>Appointments By Status</h2>
                  <BarList points={appointments?.appointmentsByStatus ?? []} />
                </div>
              </section>

              <section className="panel">
                <h2>Appointment Volume</h2>
                <BarChart points={appointments?.appointmentsByDay ?? []} max={maxAppointment} />
              </section>
            </>
          ) : null}

          {tab === "people" ? (
            <>
              <section className="panel">
                <h2>User Management</h2>
                <div className="table">
                  {users.slice(0, 12).map((user) => (
                    <article className="table-row" key={user.id}>
                      <div>
                        <strong>{user.fullName || user.email}</strong>
                        <div className="meta">{user.email} | {user.role}</div>
                      </div>
                      <button className="button secondary" onClick={() => toggleUser(user)} type="button">
                        {user.isActive ? "Disable" : "Enable"}
                      </button>
                    </article>
                  ))}
                </div>
              </section>

              <section className="two-column">
                <div className="panel">
                  <h2>Nutritionist Management</h2>
                  <div className="table">
                    {nutritionists.slice(0, 8).map((nutritionist) => (
                      <article className="table-row" key={nutritionist.id}>
                        <div>
                          <strong>{nutritionist.fullName || nutritionist.email}</strong>
                          <div className="meta">{nutritionist.licenseNumber} | {formatMoney(nutritionist.consultationFee)}</div>
                        </div>
                        <Link className="button secondary" href="/admin/clinician">{nutritionist.isVerified ? "Verified · review credentials" : "Review credentials"}</Link>
                      </article>
                    ))}
                  </div>
                </div>

                <div className="panel">
                  <h2>Patient Management</h2>
                  {patientRestricted && <p>Clinical patient data is restricted by policy. Use User Management for operational account tasks.</p>}
                  <div className="table">
                    {patients.slice(0, 8).map((patient) => (
                      <article className="table-row" key={patient.id}>
                        <div>
                          <strong>{patient.fullName || patient.email}</strong>
                          <div className="meta">{patient.currentWeight} kg | {patient.appointmentCount} appointments</div>
                        </div>
                        <span className="status Scheduled">{patient.city || "Profile"}</span>
                      </article>
                    ))}
                  </div>
                </div>
              </section>
            </>
          ) : null}

          {tab === "blogs" ? (
            <section className="two-column wide-left">
              <div className="panel">
                <h2>Blog Management</h2>
                <div className="table">
                  {blogs.map((blog) => (
                    <article className="table-row" key={blog.id}>
                      <div>
                        <strong>{blog.title}</strong>
                        <div className="meta">{blog.category} | {blog.status} | {blog.slug}</div>
                      </div>
                      <div className="actions">
                        <button className="button secondary" onClick={() => editBlog(blog)} type="button">Edit</button>
                        <button className="button danger" onClick={() => removeBlog(blog.id)} type="button">Delete</button>
                        {blog.status === "Draft" && <div><label className="checkbox-row"><input type="checkbox" checked={publicationApproval === blog.id} onChange={event => setPublicationApproval(event.target.checked ? blog.id : null)} />I have reviewed and approved this article for publication.</label><button className="button secondary" type="button" disabled={publicationApproval !== blog.id || !blog.rowVersion} onClick={() => void publishBlog(blog)}>Publish reviewed article</button></div>}
                      </div>
                    </article>
                  ))}
                </div>
              </div>

              <div className="panel">
                <h2>{editingBlogId ? "Edit Blog" : "Create Blog"}</h2>
                <form className="form" onSubmit={saveBlog}>
                  <Field label="Title">
                    <input required value={blogForm.title} onChange={(event) => setBlogForm({ ...blogForm, title: event.target.value })} />
                  </Field>
                  <Field label="Slug">
                    <input value={blogForm.slug} onChange={(event) => setBlogForm({ ...blogForm, slug: event.target.value })} />
                  </Field>
                  <Field label="Summary">
                    <textarea required rows={3} value={blogForm.summary} onChange={(event) => setBlogForm({ ...blogForm, summary: event.target.value })} />
                  </Field>
                  <Field label="Content">
                    <textarea required rows={8} value={blogForm.content} onChange={(event) => setBlogForm({ ...blogForm, content: event.target.value })} />
                  </Field>
                  <Field label="Category">
                    <input value={blogForm.category} onChange={(event) => setBlogForm({ ...blogForm, category: event.target.value })} />
                  </Field>
                  <Field label="Tags">
                    <input value={blogForm.tags} onChange={(event) => setBlogForm({ ...blogForm, tags: event.target.value })} />
                  </Field>
                  <Field label="Status">
                    <select value={blogForm.status} onChange={(event) => setBlogForm({ ...blogForm, status: event.target.value as "Draft" | "Published" })}>
                      <option value="Draft">Draft</option>
                    </select>
                  </Field>
                  <div className="actions">
                    <button className="button" disabled={loading} type="submit">{editingBlogId ? "Update" : "Create"}</button>
                    {editingBlogId ? (
                      <button className="button secondary" onClick={() => { setEditingBlogId(null); setBlogForm(emptyBlog); }} type="button">
                        Cancel
                      </button>
                    ) : null}
                  </div>
                </form>
              </div>
            </section>
          ) : null}

          {tab === "audit" ? (
            <section className="panel">
              <h2>Audit Logs</h2>
              <div className="table">
                {auditLogs.map((log) => (
                  <article className="table-row" key={log.id}>
                    <div>
                      <strong>{log.action} {log.entityType} #{log.entityId}</strong>
                      <div className="meta">{log.description || log.errorMessage || "System event"} | {new Date(log.createdAt).toLocaleString()}</div>
                    </div>
                    <span className={`status ${log.isSuccess ? "Completed" : "Cancelled"}`}>{log.isSuccess ? "Success" : "Failed"}</span>
                  </article>
                ))}
              </div>
            </section>
          ) : null}
        </section>
      </div>
    </main>
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

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
    </label>
  );
}

function BarChart({ points, max, valuePrefix = "" }: { points: AdminChartPoint[]; max: number; valuePrefix?: string }) {
  return (
    <div className="bars">
      {points.length === 0 ? <div className="notice">No chart data yet.</div> : null}
      {points.slice(-14).map((point) => (
        <div className="bar-column" key={`${point.label}-${point.value}`}>
          <div className="bar-track">
            <div className="bar-fill" style={{ height: `${Math.max((point.value / max) * 100, 3)}%` }} />
          </div>
          <span>{point.label.slice(5)}</span>
          <strong>{valuePrefix}{Math.round(point.value)}</strong>
        </div>
      ))}
    </div>
  );
}

function BarList({ points, valuePrefix = "" }: { points: AdminChartPoint[]; valuePrefix?: string }) {
  const max = maxValue(points);
  return (
    <div className="bar-list">
      {points.length === 0 ? <div className="notice">No data yet.</div> : null}
      {points.map((point) => (
        <div className="metric-row" key={point.label}>
          <span>{point.label}</span>
          <div className="metric-track">
            <div className="metric-fill" style={{ width: `${Math.max((point.value / max) * 100, 4)}%` }} />
          </div>
          <strong>{valuePrefix}{Math.round(point.value)}</strong>
        </div>
      ))}
    </div>
  );
}

function maxValue(points: AdminChartPoint[] = []) {
  return Math.max(...points.map((point) => point.value), 1);
}

function formatMoney(value: number) {
  return new Intl.NumberFormat("en-US", { currency: "PKR", style: "currency", maximumFractionDigits: 0 }).format(value);
}

