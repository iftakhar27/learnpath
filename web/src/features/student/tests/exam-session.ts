import { ApiError } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { isAnswered, type Answer } from "../question";

type Attempt = components["schemas"]["AttemptDto"];
type SaveAnswerRequest = components["schemas"]["SaveAnswerRequest"];

export type PaletteStatus = "notVisited" | "notAnswered" | "answered" | "marked" | "answeredMarked";
export type SyncState = "idle" | "saving" | "offline";
export type QuestionState = { response: Answer | null; marked: boolean; visited: boolean; timeSpentSeconds: number };

export function paletteStatus(q: QuestionState): PaletteStatus {
  const answered = isAnswered(q.response);
  if (answered && q.marked) return "answeredMarked";
  if (q.marked) return "marked";
  if (answered) return "answered";
  return q.visited ? "notAnswered" : "notVisited";
}

/**
 * Client-side state of a test attempt (port of the mobile app's ExamSession).
 *
 * The server is authoritative for timing and scoring: the deadline is derived from the server's `remainingSeconds`,
 * and every change is saved to the server. Saves that fail because the browser is offline are retried with back-off;
 * the attempt can always be resumed, because the server keeps the saved answers.
 */
export class ExamSession {
  readonly questions: QuestionState[];
  readonly deadline: number;
  current = 0;
  sync: SyncState = "idle";
  /** The server says the attempt is closed (submitted or expired); nothing more can be saved. */
  closed = false;
  /** At least one save has reached the server in this visit. */
  savedOnce = false;

  private readonly dirty = new Set<number>();
  private enteredAt: number;
  private retryTimer: ReturnType<typeof setTimeout> | undefined;
  private debounceTimer: ReturnType<typeof setTimeout> | undefined;
  private backoffSeconds = 2;
  private flushing: Promise<void> | null = null;
  private readonly listeners = new Set<() => void>();
  private version = 0;

  constructor(
    readonly attempt: Attempt,
    private readonly save: (body: SaveAnswerRequest) => Promise<unknown>,
    private readonly now: () => number = Date.now,
  ) {
    this.questions = attempt.questions.map((q) => ({
      response: (q.response as Answer | null) ?? null,
      marked: q.markedForReview,
      visited: q.response != null || q.markedForReview,
      timeSpentSeconds: q.timeSpentSeconds,
    }));
    this.deadline = this.now() + attempt.remainingSeconds * 1000;
    this.enteredAt = this.now();
    if (this.questions.length) this.questions[0].visited = true;
  }

  // ---- store for useSyncExternalStore ----
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  getVersion = () => this.version;
  private notify() {
    this.version++;
    this.listeners.forEach((l) => l());
  }

  get question() {
    return this.attempt.questions[this.current];
  }
  get state() {
    return this.questions[this.current];
  }
  remainingSeconds() {
    return Math.max(0, Math.floor((this.deadline - this.now()) / 1000));
  }
  get hasUnsaved() {
    return this.dirty.size > 0;
  }
  get answeredCount() {
    return this.questions.filter((q) => isAnswered(q.response)).length;
  }
  get markedCount() {
    return this.questions.filter((q) => q.marked).length;
  }
  count(status: PaletteStatus) {
    return this.questions.filter((q) => paletteStatus(q) === status).length;
  }

  private accrueTime() {
    const now = this.now();
    this.state.timeSpentSeconds += Math.floor((now - this.enteredAt) / 1000);
    this.enteredAt = now;
  }

  goTo(index: number) {
    if (index < 0 || index >= this.questions.length || index === this.current) return;
    this.accrueTime();
    // Time spent is part of the saved answer, so record it for the question we're leaving.
    if (this.questions[this.current].visited) this.markDirty(this.current, true);
    this.current = index;
    this.state.visited = true;
    this.notify();
  }

  /** `typing` (numerical answers) waits briefly before saving so every keystroke isn't a request. */
  setResponse(response: Answer | null, typing = false) {
    this.state.response = response;
    this.markDirty(this.current, !typing);
    this.notify();
  }

  toggleMark() {
    this.state.marked = !this.state.marked;
    this.markDirty(this.current, true);
    this.notify();
  }

  private markDirty(index: number, immediate: boolean) {
    this.dirty.add(index);
    clearTimeout(this.debounceTimer);
    if (immediate) void this.flush();
    else this.debounceTimer = setTimeout(() => void this.flush(), 700);
  }

  /** Saves every changed question. Concurrent calls share one run; a failed run schedules a retry. */
  flush(): Promise<void> {
    this.flushing ??= this.doFlush().finally(() => {
      this.flushing = null;
    });
    return this.flushing;
  }

  private async doFlush() {
    while (this.dirty.size && !this.closed) {
      const index = this.dirty.values().next().value as number;
      this.dirty.delete(index);
      const q = this.questions[index];
      this.setSync("saving");
      try {
        await this.save({
          testQuestionId: this.attempt.questions[index].testQuestionId,
          response: isAnswered(q.response) ? q.response : null,
          markedForReview: q.marked,
          timeSpentSeconds: q.timeSpentSeconds,
        });
        this.backoffSeconds = 2;
        this.savedOnce = true;
      } catch (e) {
        if (e instanceof ApiError && e.status === 0) {
          this.dirty.add(index);
          this.setSync("offline");
          clearTimeout(this.retryTimer);
          this.retryTimer = setTimeout(() => void this.flush(), this.backoffSeconds * 1000);
          this.backoffSeconds = Math.min(30, this.backoffSeconds * 2);
          return;
        }
        // Closed or expired attempts are handled by the submit/result flow; nothing more to save.
        if (e instanceof ApiError && e.status === 409) {
          this.dirty.clear();
          this.closed = true;
          break;
        }
        throw e;
      }
    }
    this.setSync("idle");
  }

  private setSync(s: SyncState) {
    if (this.sync === s) return;
    this.sync = s;
    this.notify();
  }

  /** Records time on the current question and makes a best-effort final save before submitting. */
  async prepareSubmit() {
    this.accrueTime();
    this.dirty.add(this.current);
    clearTimeout(this.debounceTimer);
    try {
      await Promise.race([this.flush(), new Promise((r) => setTimeout(r, 8000))]);
    } catch {
      // Submit anyway: the server scores what it already has.
    }
  }
}
