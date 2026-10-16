"use client";

import { CalendarPlus, CheckCircle2, ExternalLink, History, KeyRound, Loader2, Video } from "lucide-react";
import { useSession } from "@/components/SessionProvider";
import { FormEvent, useEffect, useState } from "react";
import {
  completeVideoConsultation,
  getVideoConsultationHistory,
  joinVideoConsultation,
  scheduleVideoConsultation
} from "@/services/videoConsultations";
import type { JoinVideoConsultation, VideoConsultation } from "@/types/video-consultation";

export default function VideoConsultationsPage() {
  const { user } = useSession();
  const [appointmentId, setAppointmentId] = useState("");
  const [provider, setProvider] = useState<"GoogleMeet" | "Zoom">("GoogleMeet");
  const [notes, setNotes] = useState("");
  const [history, setHistory] = useState<VideoConsultation[]>([]);
  const [selectedJoin, setSelectedJoin] = useState<JoinVideoConsultation | null>(null);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    void loadHistory();
  }, []);


  async function loadHistory() {
    try {
      setError("");
      const data = await getVideoConsultationHistory();
      setHistory(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load consultations");
    }
  }

  async function schedule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLoading(true);
    setError("");
    setMessage("");

    try {
      const result = await scheduleVideoConsultation({
        appointmentId: Number(appointmentId),
        provider,
        notes
      });
      setMessage(`Scheduled ${result.provider} meeting for appointment #${result.appointmentId}`);
      setAppointmentId("");
      setNotes("");
      await loadHistory();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not schedule consultation");
    } finally {
      setLoading(false);
    }
  }

  async function join(id: number) {
    setLoading(true);
    setError("");
    try {
      const result = await joinVideoConsultation(id);
      setSelectedJoin(result);
      window.open(result.isHost && result.hostUrl ? result.hostUrl : result.meetingUrl, "_blank", "noopener,noreferrer");
      await loadHistory();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not join meeting");
    } finally {
      setLoading(false);
    }
  }

  async function complete(id: number) {
    setLoading(true);
    setError("");
    try {
      await completeVideoConsultation(id, "Session completed from portal");
      setMessage("Consultation marked completed");
      await loadHistory();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not complete meeting");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="page">
      <header className="topbar">
        <div className="brand">
          <Video size={22} />
          NutriCare Pro Video Consultations
        </div>
        <div className="meta">{history.length} sessions</div>
      </header>

      <div className="shell">
        <aside className="grid">
          {user?.role === "Nutritionist" && <section className="panel">
            <h2>Schedule Meeting</h2>
            <form className="form" onSubmit={schedule}>
              <div className="field">
                <label htmlFor="appointmentId">Appointment ID</label>
                <input
                  id="appointmentId"
                  inputMode="numeric"
                  min="1"
                  required
                  type="number"
                  value={appointmentId}
                  onChange={(event) => setAppointmentId(event.target.value)}
                />
              </div>

              <div className="field">
                <label htmlFor="provider">Provider</label>
                <select
                  id="provider"
                  value={provider}
                  onChange={(event) => setProvider(event.target.value as "GoogleMeet" | "Zoom")}
                >
                  <option value="GoogleMeet">Google Meet</option>
                  <option value="Zoom">Zoom</option>
                </select>
              </div>

              <div className="field">
                <label htmlFor="notes">Notes</label>
                <textarea id="notes" rows={3} value={notes} onChange={(event) => setNotes(event.target.value)} />
              </div>

              <button className="button" disabled={loading} type="submit">
                {loading ? <Loader2 size={16} /> : <CalendarPlus size={16} />}
                Schedule
              </button>
            </form>
          </section>}
        </aside>

        <section className="grid">
          {message ? <div className="notice">{message}</div> : null}
          {error ? <div className="error">{error}</div> : null}

          {selectedJoin ? (
            <section className="panel">
              <h2>Join Link</h2>
              <div className="row">
                <div>
                  <strong>{selectedJoin.provider} appointment #{selectedJoin.appointmentId}</strong>
                  <div className="meta">{selectedJoin.isHost ? "Host link available" : "Participant link"}</div>
                </div>
                <a
                  className="button"
                  href={selectedJoin.isHost && selectedJoin.hostUrl ? selectedJoin.hostUrl : selectedJoin.meetingUrl}
                  rel="noreferrer"
                  target="_blank"
                >
                  <ExternalLink size={16} />
                  Open
                </a>
              </div>
            </section>
          ) : null}

          <section className="panel">
            <div className="actions" style={{ justifyContent: "space-between", marginBottom: 12 }}>
              <h2 style={{ margin: 0 }}>Session History</h2>
              <button className="button secondary" onClick={loadHistory} type="button">
                <History size={16} />
                Refresh
              </button>
            </div>

            <div className="list">
              {history.length === 0 ? <div className="notice">No video consultations found.</div> : null}
              {history.map((item) => (
                <article className="row" key={item.id}>
                  <div>
                    <strong>
                      #{item.id} appointment #{item.appointmentId}
                    </strong>
                    <div className="meta">
                      {item.provider} | {new Date(item.scheduledStartTime).toLocaleString()} -{" "}
                      {new Date(item.scheduledEndTime).toLocaleTimeString()}
                    </div>
                  </div>
                  <div className="actions">
                    <span className={`status ${item.status}`}>{item.status}</span>
                    <button className="button secondary" onClick={() => join(item.id)} type="button">
                      <KeyRound size={16} />
                      Join
                    </button>
                    {user?.role === "Nutritionist" && item.status !== "Completed" ? (
                      <button className="button secondary" onClick={() => complete(item.id)} type="button">
                        <CheckCircle2 size={16} />
                        Complete
                      </button>
                    ) : null}
                  </div>
                </article>
              ))}
            </div>
          </section>
        </section>
      </div>
    </main>
  );
}
