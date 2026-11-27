import axios from "axios";

/**
 * The message to show the user for a failed API call. The API answers errors as ProblemDetails with the reason in
 * `detail` (docs/API_ENDPOINTS.md, Common responses); model validation adds `errors` per field. A 403 from an
 * ownership or role check has no body on purpose, so it gets a generic text here.
 */
export function apiErrorMessage(error: unknown): string {
  if (!axios.isAxiosError(error)) return "Something went wrong. Please try again.";
  const response = error.response;
  if (!response) return "The server could not be reached. Check your connection and try again.";

  const data = response.data as
    { detail?: string | null; title?: string | null; errors?: Record<string, string[]> } | string | undefined;
  if (data && typeof data === "object") {
    if (data.errors) {
      const messages = Object.values(data.errors).flat().filter(Boolean);
      if (messages.length) return messages.join(" ");
    }
    if (data.detail) return data.detail;
  }
  if (typeof data === "string" && data.trim()) return data;
  if (response.status === 401) return "Your session has ended. Please sign in again.";
  if (response.status === 403) return "You are not allowed to do this.";
  if (data && typeof data === "object" && data.title) return data.title;
  return `The request failed (${response.status}). Please try again.`;
}
