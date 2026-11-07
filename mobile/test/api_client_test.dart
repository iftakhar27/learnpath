import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/token_store.dart';

/// A JWT whose only meaningful claim is `exp` (the client never verifies signatures).
String jwt(DateTime expires) {
  String enc(Map<String, dynamic> m) => base64Url.encode(utf8.encode(jsonEncode(m))).replaceAll('=', '');
  return '${enc({'alg': 'HS256'})}.${enc({'exp': expires.millisecondsSinceEpoch ~/ 1000})}.sig';
}

/// Fake server: `/auth/refresh` rotates tokens; other paths require the current access token.
class FakeServer implements HttpClientAdapter {
  FakeServer({required this.validAccess, this.refreshSucceeds = true, this.refreshFailureCode = 'invalid_refresh_token'});
  String validAccess;
  bool refreshSucceeds;
  String refreshFailureCode;
  int refreshCalls = 0;
  final seenAuth = <String?>[];

  ResponseBody _json(int status, Object body) => ResponseBody.fromString(
    jsonEncode(body),
    status,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    await Future<void>.delayed(const Duration(milliseconds: 20)); // let concurrent requests overlap
    if (options.path.endsWith('/auth/refresh')) {
      refreshCalls++;
      if (!refreshSucceeds) return _json(401, {'code': refreshFailureCode, 'title': 'Session expired.'});
      validAccess = jwt(DateTime.now().add(const Duration(minutes: 15)));
      return _json(200, {'accessToken': validAccess, 'refreshToken': 'refresh-$refreshCalls'});
    }
    final auth = options.headers['Authorization'] as String?;
    seenAuth.add(auth);
    if (auth != 'Bearer $validAccess') return _json(401, {'code': 'unauthenticated', 'title': 'Login required.'});
    return _json(200, {'ok': true, 'lang': options.headers['Accept-Language']});
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  late MemoryTokenStore store;
  setUp(() => store = MemoryTokenStore());

  ApiClient client(FakeServer server, {void Function(String?)? onExpired}) =>
      ApiClient(baseUrl: 'https://api.test', store: store, language: () => 'hi', onSessionExpired: onExpired, adapter: server);

  test('attaches the bearer token and Accept-Language', () async {
    final access = jwt(DateTime.now().add(const Duration(minutes: 10)));
    store.tokens = StoredTokens(accessToken: access, refreshToken: 'r0');
    final server = FakeServer(validAccess: access);
    final res = await client(server).dio.get<Map<String, dynamic>>('/student/profile');
    expect(res.data!['lang'], 'hi');
    expect(server.seenAuth.single, 'Bearer $access');
    expect(server.refreshCalls, 0);
  });

  test('concurrent requests with an expiring token share exactly one refresh', () async {
    store.tokens = StoredTokens(accessToken: jwt(DateTime.now().add(const Duration(seconds: 5))), refreshToken: 'r0');
    final server = FakeServer(validAccess: 'nothing-yet');
    final api = client(server);

    final results = await Future.wait([for (var i = 0; i < 6; i++) api.dio.get<Map<String, dynamic>>('/student/dashboard')]);

    expect(results.every((r) => r.statusCode == 200), isTrue);
    expect(server.refreshCalls, 1, reason: 'a rotated refresh token reused twice would revoke the session');
    expect(store.tokens!.refreshToken, 'refresh-1');
  });

  test('a 401 on a fresh-looking token triggers one refresh and a retry', () async {
    store.tokens = StoredTokens(accessToken: jwt(DateTime.now().add(const Duration(minutes: 10))), refreshToken: 'r0');
    final server = FakeServer(validAccess: 'revoked-server-side');
    final res = await client(server).dio.get<Map<String, dynamic>>('/tests');
    expect(res.statusCode, 200);
    expect(server.refreshCalls, 1);
    expect(server.seenAuth.length, 2);
  });

  test('a rejected refresh clears tokens and reports the session as expired', () async {
    store.tokens = StoredTokens(accessToken: jwt(DateTime.now().subtract(const Duration(minutes: 1))), refreshToken: 'r0');
    final server = FakeServer(validAccess: 'x', refreshSucceeds: false);
    var expired = 0;
    final api = client(server, onExpired: (_) => expired++);

    await expectLater(api.dio.get<dynamic>('/student/dashboard'), throwsA(isA<DioException>()));
    expect(store.tokens, isNull);
    expect(expired, 1);
  });

  test('a session replaced by another phone is reported with its reason', () async {
    store.tokens = StoredTokens(accessToken: jwt(DateTime.now().subtract(const Duration(minutes: 1))), refreshToken: 'r0');
    final server = FakeServer(validAccess: 'x', refreshSucceeds: false, refreshFailureCode: 'session_replaced');
    String? reason;
    final api = client(server, onExpired: (r) => reason = r);

    await expectLater(api.dio.get<dynamic>('/student/dashboard'), throwsA(isA<DioException>()));
    expect(store.tokens, isNull);
    expect(reason, 'session_replaced');
  });

  test('problem details become ApiException with code and message', () {
    final e = ApiException.from(
      DioException(
        requestOptions: RequestOptions(path: '/x'),
        response: Response(
          requestOptions: RequestOptions(path: '/x'),
          statusCode: 402,
          data: {'code': 'entitlement_required', 'title': 'Buy it'},
        ),
      ),
    );
    expect((e.status, e.code, e.message, e.needsPurchase), (402, 'entitlement_required', 'Buy it', true));

    final network = ApiException.from(
      DioException(
        requestOptions: RequestOptions(path: '/x'),
        type: DioExceptionType.connectionError,
      ),
    );
    expect(network.isNetwork, isTrue);
  });

  test('ASP.NET validation errors surface the first field message', () {
    final e = ApiException.from(
      DioException(
        requestOptions: RequestOptions(path: '/x'),
        response: Response(
          requestOptions: RequestOptions(path: '/x'),
          statusCode: 400,
          data: {
            'title': 'One or more validation errors occurred.',
            'errors': {
              'Password': ['The field Password must be a string with a minimum length of 8.'],
            },
          },
        ),
      ),
    );
    expect(e.code, 'validation_failed');
    expect(e.message, contains('minimum length of 8'));
  });
}
