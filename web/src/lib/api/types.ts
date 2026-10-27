import type { components } from "./schema";

type S = components["schemas"];

export type PublishStatus = S["PublishStatus"];
export type QuestionType = S["QuestionType"];
export type Difficulty = S["Difficulty"];
export type TestType = S["TestType"];
export type ResourceType = S["ResourceType"];
export type UserStatus = S["UserStatus"];
export type OrderStatus = S["OrderStatus"];

export type AdminExam = S["AdminExamDto"];
export type AdminCourse = S["AdminCourseDto"];
export type AdminCourseTree = S["AdminCourseTreeDto"];
export type AdminSubject = S["AdminSubjectDto"];
export type AdminChapter = S["AdminChapterDto"];
export type AdminTopic = S["AdminTopicDto"];
export type AdminResource = S["AdminResourceDto"];
export type AdminQuestionListItem = S["AdminQuestionListItem"];
export type AdminQuestion = S["AdminQuestionDto"];
export type AdminTestListItem = S["AdminTestListItem"];
export type AdminTest = S["AdminTestDto"];
export type AdminTestQuestion = S["AdminTestQuestionDto"];
export type AdminUserListItem = S["AdminUserListItem"];
export type AdminUserDetail = S["AdminUserDetailDto"];
export type AdminOrder = S["AdminOrderDto"];
export type AdminTicket = S["AdminTicketDto"];
export type AdminQuestionReport = S["AdminQuestionReportDto"];
export type AdminImportJob = S["AdminImportJobDto"];
export type AdminAuditLog = S["AdminAuditLogDto"];
export type AdminDashboard = S["AdminDashboardDto"];
export type ImportResult = S["ImportResultDto"];

export type CourseUpsert = S["CourseUpsert"];
export type NodeUpsert = S["NodeUpsert"];
export type ResourceUpsert = S["ResourceUpsert"];
export type QuestionUpsert = S["QuestionUpsert"];
export type OptionUpsert = S["OptionUpsert"];
export type TestUpsert = S["TestUpsert"];
export type TestQuestionUpsert = S["TestQuestionUpsert"];
export type SectionUpsert = S["SectionUpsert"];
export type ExamUpsert = S["ExamUpsert"];

export const PUBLISH_STATUSES: PublishStatus[] = ["Draft", "Published", "Archived"];
export const QUESTION_TYPES: QuestionType[] = ["SingleCorrect", "MultipleCorrect", "Numerical", "Integer", "TrueFalse"];
export const DIFFICULTIES: Difficulty[] = ["Easy", "Medium", "Hard"];
export const TEST_TYPES: TestType[] = ["Topic", "Chapter", "Subject", "FullSyllabus", "Mock", "PreviousYear", "Diagnostic", "Custom"];
export const ORDER_STATUSES: OrderStatus[] = ["Created", "Paid", "Failed", "Refunded"];
export const USER_STATUSES: UserStatus[] = ["Active", "Suspended", "DeletionRequested", "Deleted"];
export const ROLES = ["Student", "Teacher", "ContentAdmin", "Support", "Finance", "Analyst", "SuperAdmin"] as const;
