import type { components } from "@/lib/api/schema";

/** Every student page URL in one place. */
export const routes = {
  home: "/home",
  learn: "/learn",
  course: (id: string) => `/learn/${id}`,
  topic: (id: string) => `/topics/${id}`,
  topicQuiz: (id: string) => `/topics/${id}/quiz`,
  watch: (resourceId: string) => `/watch/${resourceId}`,
  practice: "/practice",
  practiceTopic: (topicId: string) => `/practice/topic/${topicId}`,
  practiceMode: (mode: "incorrect" | "bookmarked" | "previous_year") => `/practice/${mode}`,
  tests: "/tests",
  test: (id: string) => `/tests/${id}`,
  exam: (attemptId: string) => `/exam/${attemptId}`,
  result: (attemptId: string) => `/results/${attemptId}`,
  review: (attemptId: string) => `/results/${attemptId}/review`,
  plan: "/plan",
  profile: "/profile",
  notifications: "/notifications",
  doubts: "/doubts",
  doubt: (id: string) => `/doubts/${id}`,
};

type PlanTask = components["schemas"]["PlanTaskDto"];

/** Where a study-plan task leads (same rules as the app). */
export function planTaskHref(task: PlanTask): string {
  switch (task.type) {
    case "Learn":
      return routes.watch(task.entityId);
    case "Practice":
      return routes.practiceTopic(task.entityId);
    case "Test":
      return routes.test(task.entityId);
    default:
      return routes.topic(task.entityId);
  }
}
