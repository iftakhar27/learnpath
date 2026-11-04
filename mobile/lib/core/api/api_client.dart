import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';

import 'token_store.dart';

/// Problem-details error from the API, or a network failure (`status == 0`).
class ApiException implements Exception {
  const ApiException(this.status, this.code, this.message);
  final int status;
  final String code;
  final String message;

  bool get isNetwork => status == 0;
  bool get needsPurchase => status == 402;

  @override
  String toString() => 'ApiException($status, $code, $message)';

  static ApiException from(Object error) {
    if (error is ApiException) return error;
    if (error is DioException) {
      final res = error.response;
      if (res == null) return const ApiException(0, 'network_error', 'Network error');
      final data = res.data;
      if (data is Map) {
        final errors = data['errors'];
        final firstValidation = errors is Map && errors.isNotEmpty ? (errors.values.first as List?)?.first?.toString() : null;
        return ApiException(
          res.statusCode ?? 0,
          (data['code'] as String?) ?? (errors != null ? 'validation_failed' : 'http_${res.statusCode}'),
          firstValidation ?? (data['title'] as String?) ?? 'Request failed (${res.statusCode})',
        );
      }
      return ApiException(res.statusCode ?? 0, 'http_${res.statusCode}', 'Request failed (${res.statusCode})');
    }
    return ApiException(-1, 'unexpected', error.toString());
  }
}

/// Seconds since epoch at which a JWT expires (0 if unreadable). Not a validation; the API verifies tokens.
int jwtExpiry(String token) {
  try {
    final part = token.split('.')[1];
    final payload = jsonDecode(utf8.decode(base64Url.decode(base64Url.normalize(part)))) as Map<String, dynamic>;
    return (payload['exp'] as num?)?.toInt() ?? 0;
  } catch (_) {
    return 0;
  }
}

/// Dio configured for the LearnPath API.
///
/// Access tokens are attached to every request and refreshed shortly before they expire, or once after a 401.
/// The API revokes every session if a rotated refresh token is presented twice, so concurrent requests share a
/// single in-flight refresh instead of each refreshing on their own.
class ApiClient {
  ApiClient({required String baseUrl, required this.store, required this.language, this.onSessionExpired, HttpClientAdapter? adapter})
    : dio = Dio(
        BaseOptions(
          baseUrl: '$baseUrl/api/v1',
          connectTimeout: const Duration(seconds: 15),
          // Long enough for a sleeping server (e.g. a free hosting plan) to wake up on the first request.
          receiveTimeout: const Duration(seconds: 60),
          contentType: Headers.jsonContentType,
        ),
      ) {
    if (adapter != null) dio.httpClientAdapter = adapter;
    dio.interceptors.add(InterceptorsWrapper(onRequest: _onRequest, onError: _onError));
  }

  final Dio dio;
  final TokenStore store;

  /// Current UI language code ("en" or "hi"), sent as Accept-Language so the API returns localized content.
  final String Function() language;

  /// Called when the refresh token is rejected; the app should return to sign-in.
  final void Function()? onSessionExpired;

  Future<String?>? _refreshing;

  static const _noAuth = '_noAuth';
  static const _retried = '_retried';

  /// Options for endpoints that must not carry or refresh a token (sign-in, register, refresh itself).
  static Options get anonymous => Options(extra: {_noAuth: true});

  Future<void> _onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    options.headers['Accept-Language'] = language();
    if (options.extra[_noAuth] == true) return handler.next(options);
    var tokens = await store.read();
    if (tokens != null && jwtExpiry(tokens.accessToken) - 30 < DateTime.now().millisecondsSinceEpoch ~/ 1000) {
      final fresh = await refresh();
      if (fresh != null) tokens = StoredTokens(accessToken: fresh, refreshToken: tokens.refreshToken);
    }
    if (tokens != null) options.headers['Authorization'] = 'Bearer ${tokens.accessToken}';
    handler.next(options);
  }

  Future<void> _onError(DioException error, ErrorInterceptorHandler handler) async {
    final options = error.requestOptions;
    if (error.response?.statusCode == 401 && options.extra[_noAuth] != true && options.extra[_retried] != true) {
      final token = await refresh();
      if (token != null) {
        options.extra[_retried] = true;
        options.headers['Authorization'] = 'Bearer $token';
        try {
          return handler.resolve(await dio.fetch<dynamic>(options));
        } on DioException catch (e) {
          return handler.next(e);
        }
      }
    }
    handler.next(error);
  }

  /// Rotates the refresh token. Concurrent callers share one request. Returns the new access token, or null when
  /// the session has ended (tokens are cleared and [onSessionExpired] fires).
  Future<String?> refresh() => _refreshing ??= _doRefresh().whenComplete(() => _refreshing = null);

  Future<String?> _doRefresh() async {
    final current = await store.read();
    if (current == null) return null;
    try {
      final res = await dio.post<Map<String, dynamic>>('/auth/refresh', data: {'refreshToken': current.refreshToken}, options: anonymous);
      final data = res.data!;
      await store.save(StoredTokens(accessToken: data['accessToken'] as String, refreshToken: data['refreshToken'] as String));
      return data['accessToken'] as String;
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) {
        await store.clear();
        onSessionExpired?.call();
      }
      return null; // network failure: keep tokens and let the caller surface the error
    }
  }
}
