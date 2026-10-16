import type { ApiEnvelope } from "./types";

export class ApiError extends Error {
  constructor(message: string, public status: number, public errors?: ApiEnvelope<unknown>["errors"]) { super(message); }
}

export function validationErrors(payload: unknown): ApiEnvelope<unknown>["errors"] {
  if (!payload || typeof payload !== "object") return undefined;
  const envelope = payload as { errors?: unknown; data?: { errors?: unknown } };
  const errors = envelope.errors ?? envelope.data?.errors;
  if (Array.isArray(errors)) return errors.filter((item): item is string => typeof item === "string");
  if (!errors || typeof errors !== "object") return undefined;
  return Object.fromEntries(Object.entries(errors).map(([field, values]) => [field, Array.isArray(values) ? values.filter((item): item is string => typeof item === "string") : typeof values === "string" ? [values] : []]));
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  const response = await fetch(`/api/proxy${path}`, { ...init, headers, credentials: "same-origin", cache: "no-store" });
  const payload = await response.json().catch(() => null) as ApiEnvelope<T> | null;
  if (!response.ok || payload?.success === false) {
    if (response.status === 401 && typeof window !== "undefined") window.dispatchEvent(new Event("session-expired"));
    throw new ApiError(payload?.message || (response.status === 403 ? "You do not have permission for this action." : "The request could not be completed. Please try again."), response.status, validationErrors(payload));
  }
  return payload?.data as T;
}

export async function download(path: string, filename: string) {
  const response = await fetch(`/api/proxy${path}`, { credentials: "same-origin", cache: "no-store" });
  if (!response.ok) {
    if (response.status === 401) window.dispatchEvent(new Event("session-expired"));
    const payload = await response.json().catch(() => null) as ApiEnvelope<unknown> | null;
    throw new ApiError(payload?.message || "The download could not be completed.", response.status, validationErrors(payload));
  }
  const objectUrl = URL.createObjectURL(await response.blob());
  const anchor = document.createElement("a"); anchor.href = objectUrl; anchor.download = filename; anchor.click();
  setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
}

export const json = (method: string, body?: unknown): RequestInit => ({ method, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
export const errorMessage = (error: unknown) => {
  if (error instanceof ApiError && error.errors) {
    const details = Array.isArray(error.errors) ? error.errors : Object.entries(error.errors).flatMap(([field, messages]) => messages.map(message => `${field}: ${message}`));
    return [error.message, ...new Set(details)].join(" ");
  }
  return error instanceof Error ? error.message : "Something went wrong. Please try again.";
};
