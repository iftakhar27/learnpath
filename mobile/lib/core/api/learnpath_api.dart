import 'dart:math';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import 'api_client.dart';
import 'models.g.dart';

/// Typed calls to the LearnPath API. Every method throws [ApiException] on failure.
class LearnPathApi {
  LearnPathApi(this.client);
  final ApiClient client;
  Dio get _dio => client.dio;

  Future<T> _call<T>(Future<Response<dynamic>> Function() request, T Function(dynamic data) parse) async {
    try {
      final res = await request();
      return parse(res.data);
    } catch (e) {
      throw ApiException.from(e);
    }
  }

  Future<void> _send(Future<Response<dynamic>> Function() request) => _call(request, (_) {});

  Map<String, dynamic> _map(dynamic d) => d as Map<String, dynamic>;
  List<T> _list<T>(dynamic d, T Function(Map<String, dynamic>) f) => (d as List<dynamic>).map((x) => f(x as Map<String, dynamic>)).toList();

  // ---- Auth ----
  // Every sign-in says which device it comes from: the account may be signed in on one phone and one browser; another
  // phone has to confirm taking over (takeOver), and this phone signing in again is recognised by its own id.

  static const _deviceIdKey = 'device_id';

  Future<({String slot, String name, String id})> device() async {
    var id = await client.store.readPreference(_deviceIdKey);
    if (id == null) {
      final r = Random.secure();
      id = List.generate(16, (_) => r.nextInt(256).toRadixString(16).padLeft(2, '0')).join();
      await client.store.writePreference(_deviceIdKey, id);
    }
    final name = kIsWeb
        ? 'Web app'
        : switch (defaultTargetPlatform) {
            TargetPlatform.iOS => 'iPhone app',
            TargetPlatform.android => 'Android app',
            _ => 'Phone app',
          };
    return (slot: kIsWeb ? 'browser' : 'phone', name: name, id: id);
  }

  Future<AuthTokens> login(String email, String password, {bool takeOver = false}) async {
    final d = await device();
    return _call(
      () => _dio.post(
        '/auth/login',
        data: LoginRequest(email: email, password: password, deviceSlot: d.slot, deviceName: d.name, deviceId: d.id, takeOver: takeOver).toJson(),
        options: ApiClient.anonymous,
      ),
      (d) => AuthTokens.fromJson(_map(d)),
    );
  }

  Future<AuthTokens> register({required String fullName, required String email, required String password, required bool acceptTerms}) async {
    final d = await device();
    return _call(
      () => _dio.post(
        '/auth/register',
        data: RegisterRequest(
          email: email,
          password: password,
          fullName: fullName,
          acceptTerms: acceptTerms,
          deviceSlot: d.slot,
          deviceName: d.name,
          deviceId: d.id,
        ).toJson(),
        options: ApiClient.anonymous,
      ),
      (d) => AuthTokens.fromJson(_map(d)),
    );
  }

  Future<void> requestOtp(String phone) => _send(
    () => _dio.post(
      '/auth/otp/request',
      data: OtpRequest(phone: phone).toJson(),
      options: ApiClient.anonymous,
    ),
  );

  Future<AuthTokens> verifyOtp({required String phone, required String code, String? fullName, required bool acceptTerms, bool takeOver = false}) async {
    final d = await device();
    return _call(
      () => _dio.post(
        '/auth/otp/verify',
        data: OtpVerifyRequest(
          phone: phone,
          code: code,
          fullName: fullName,
          acceptTerms: acceptTerms,
          deviceSlot: d.slot,
          deviceName: d.name,
          deviceId: d.id,
          takeOver: takeOver,
        ).toJson(),
        options: ApiClient.anonymous,
      ),
      (d) => AuthTokens.fromJson(_map(d)),
    );
  }

  Future<void> logout(String refreshToken) => _send(
    () => _dio.post(
      '/auth/logout',
      data: RefreshRequest(refreshToken: refreshToken).toJson(),
      options: ApiClient.anonymous,
    ),
  );

  // ---- Profile ----
  Future<ProfileDto> profile() => _call(() => _dio.get('/student/profile'), (d) => ProfileDto.fromJson(_map(d)));
  Future<ProfileDto> updateProfile(UpdateProfileRequest body) =>
      _call(() => _dio.put('/student/profile', data: body.toJson()), (d) => ProfileDto.fromJson(_map(d)));
  Future<void> requestAccountDeletion() => _send(() => _dio.post('/student/profile/delete-request'));

  // ---- Catalog & learning ----
  Future<List<ExamDto>> exams() => _call(() => _dio.get('/exams'), (d) => _list(d, ExamDto.fromJson));

  Future<PagedResultOfCourseSummaryDto> courses({String? q, String? examId, bool? free, int page = 1}) => _call(
    () => _dio.get('/courses', queryParameters: {'q': ?q, 'examId': ?examId, 'free': ?free, 'page': page, 'pageSize': 50}),
    (d) => PagedResultOfCourseSummaryDto.fromJson(_map(d)),
  );

  Future<CourseDetailDto> syllabus(String courseId) => _call(() => _dio.get('/courses/$courseId/syllabus'), (d) => CourseDetailDto.fromJson(_map(d)));
  Future<void> enroll(String courseId) => _send(() => _dio.post('/courses/$courseId/enroll'));
  Future<TopicDetailDto> topic(String topicId) => _call(() => _dio.get('/topics/$topicId'), (d) => TopicDetailDto.fromJson(_map(d)));
  Future<PlaybackDto> playback(String resourceId) => _call(() => _dio.get('/resources/$resourceId/playback'), (d) => PlaybackDto.fromJson(_map(d)));

  Future<ProgressDto> saveProgress(String resourceId, int positionSeconds, {bool markCompleted = false}) => _call(
    () => _dio.post(
      '/learning/progress',
      data: ProgressRequest(resourceId: resourceId, positionSeconds: positionSeconds, markCompleted: markCompleted).toJson(),
    ),
    (d) => ProgressDto.fromJson(_map(d)),
  );

  Future<List<BookmarkDto>> bookmarks() => _call(() => _dio.get('/learning/bookmarks'), (d) => _list(d, BookmarkDto.fromJson));
  Future<void> addBookmark(BookmarkType type, String entityId) => _send(
    () => _dio.post(
      '/learning/bookmarks',
      data: BookmarkRequest(entityType: type, entityId: entityId).toJson(),
    ),
  );
  Future<void> removeBookmark(String bookmarkId) => _send(() => _dio.delete('/learning/bookmarks/$bookmarkId'));

  // ---- Dashboard, analytics, plan ----
  Future<DashboardDto> dashboard() => _call(() => _dio.get('/student/dashboard'), (d) => DashboardDto.fromJson(_map(d)));
  Future<StudentAnalyticsDto> analytics() => _call(() => _dio.get('/student/analytics'), (d) => StudentAnalyticsDto.fromJson(_map(d)));
  Future<StudyPlanDto> studyPlan() => _call(() => _dio.get('/student/study-plan'), (d) => StudyPlanDto.fromJson(_map(d)));
  Future<StudyPlanDto> regeneratePlan() => _call(() => _dio.post('/student/study-plan/regenerate'), (d) => StudyPlanDto.fromJson(_map(d)));
  Future<List<PlanDayDto>> planHistory() =>
      _call(() => _dio.get('/student/study-plan/history', queryParameters: {'days': 7}), (d) => _list(d, PlanDayDto.fromJson));
  Future<PlanTaskDto> completeTask(String id) => _call(() => _dio.post('/student/study-plan/tasks/$id/complete'), (d) => PlanTaskDto.fromJson(_map(d)));
  Future<PlanTaskDto> skipTask(String id) => _call(() => _dio.post('/student/study-plan/tasks/$id/skip'), (d) => PlanTaskDto.fromJson(_map(d)));

  // ---- Practice ----
  Future<PracticeSessionDto> startPractice(CreatePracticeRequest body) =>
      _call(() => _dio.post('/practice/sessions', data: body.toJson()), (d) => PracticeSessionDto.fromJson(_map(d)));
  Future<PracticeFeedbackDto> answerPractice(String sessionId, PracticeAnswerRequest body) =>
      _call(() => _dio.post('/practice/sessions/$sessionId/answers', data: body.toJson()), (d) => PracticeFeedbackDto.fromJson(_map(d)));
  Future<PracticeSummaryDto> completePractice(String sessionId) =>
      _call(() => _dio.post('/practice/sessions/$sessionId/complete'), (d) => PracticeSummaryDto.fromJson(_map(d)));
  Future<void> reportQuestion(String questionId, String reason, String? details) => _send(
    () => _dio.post(
      '/questions/$questionId/report',
      data: ReportQuestionRequest(reason: reason, details: details).toJson(),
    ),
  );

  // ---- Topic quiz ----
  Future<QuizDto> startTopicQuiz(String topicId) => _call(() => _dio.post('/topics/$topicId/quiz'), (d) => QuizDto.fromJson(_map(d)));
  Future<QuizResultDto> submitQuiz(String sessionId, QuizSubmitRequest body) =>
      _call(() => _dio.post('/practice/quizzes/$sessionId/submit', data: body.toJson()), (d) => QuizResultDto.fromJson(_map(d)));

  // ---- Doubts ----
  Future<PagedResultOfDoubtSummaryDto> doubts() =>
      _call(() => _dio.get('/doubts', queryParameters: {'pageSize': 50}), (d) => PagedResultOfDoubtSummaryDto.fromJson(_map(d)));
  Future<DoubtDto> doubt(String id) => _call(() => _dio.get('/doubts/$id'), (d) => DoubtDto.fromJson(_map(d)));
  Future<DoubtDto> askDoubt(AskDoubtRequest body) => _call(() => _dio.post('/doubts', data: body.toJson()), (d) => DoubtDto.fromJson(_map(d)));
  Future<DoubtDto> followUpDoubt(String id, String text) =>
      _call(() => _dio.post('/doubts/$id/messages', data: DoubtReplyRequest(text: text).toJson()), (d) => DoubtDto.fromJson(_map(d)));
  Future<DoubtDto> resolveDoubt(String id) => _call(() => _dio.post('/doubts/$id/resolve'), (d) => DoubtDto.fromJson(_map(d)));

  // ---- Tests ----
  Future<List<TestSummaryDto>> tests({String? courseId}) =>
      _call(() => _dio.get('/tests', queryParameters: {'courseId': ?courseId}), (d) => _list(d, TestSummaryDto.fromJson));
  Future<TestDetailDto> testDetail(String testId) => _call(() => _dio.get('/tests/$testId'), (d) => TestDetailDto.fromJson(_map(d)));
  Future<AttemptDto> startAttempt(String testId) => _call(() => _dio.post('/tests/$testId/attempts'), (d) => AttemptDto.fromJson(_map(d)));
  Future<AttemptDto> attempt(String attemptId) => _call(() => _dio.get('/attempts/$attemptId'), (d) => AttemptDto.fromJson(_map(d)));
  Future<void> saveAnswer(String attemptId, SaveAnswerRequest body) => _send(() => _dio.put('/attempts/$attemptId/answers', data: body.toJson()));
  Future<ResultDto> submit(String attemptId) => _call(() => _dio.post('/attempts/$attemptId/submit'), (d) => ResultDto.fromJson(_map(d)));
  Future<ResultDto> result(String attemptId) => _call(() => _dio.get('/attempts/$attemptId/results'), (d) => ResultDto.fromJson(_map(d)));
  Future<List<AttemptHistoryDto>> attempts() => _call(() => _dio.get('/attempts'), (d) => _list(d, AttemptHistoryDto.fromJson));

  // ---- Notifications, support, search ----
  Future<PagedResultOfNotificationDto> notifications() => _call(() => _dio.get('/notifications'), (d) => PagedResultOfNotificationDto.fromJson(_map(d)));
  Future<void> markNotificationRead(String id) => _send(() => _dio.post('/notifications/$id/read'));
  Future<void> markAllNotificationsRead() => _send(() => _dio.post('/notifications/read-all'));
  Future<List<TicketDto>> tickets() => _call(() => _dio.get('/support/tickets'), (d) => _list(d, TicketDto.fromJson));
  Future<TicketDto> createTicket(TicketRequest body) => _call(() => _dio.post('/support/tickets', data: body.toJson()), (d) => TicketDto.fromJson(_map(d)));
  Future<List<SearchHitDto>> search(String q) => _call(() => _dio.get('/search', queryParameters: {'q': q}), (d) => _list(d, SearchHitDto.fromJson));

  // ---- Payments ----
  Future<CheckoutDto> createOrder(String courseId) =>
      _call(() => _dio.post('/payments/orders', data: CreateOrderRequest(courseId: courseId).toJson()), (d) => CheckoutDto.fromJson(_map(d)));
  Future<OrderDto> verifyPayment(VerifyPaymentRequest body) =>
      _call(() => _dio.post('/payments/verify', data: body.toJson()), (d) => OrderDto.fromJson(_map(d)));
  Future<List<OrderDto>> orders() => _call(() => _dio.get('/payments/orders'), (d) => _list(d, OrderDto.fromJson));
}
