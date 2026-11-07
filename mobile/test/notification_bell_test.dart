import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/home/notification_bell.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart';

/// Serves the unread count; tests change [unread] to simulate new notifications arriving.
class CountApi implements HttpClientAdapter {
  int unread = 2;
  int calls = 0;

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    calls++;
    return ResponseBody.fromString(
      jsonEncode({'count': unread}),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  testWidgets('the bell picks up new notifications while open, and pauses in the background', (tester) async {
    final api = CountApi();
    final store = MemoryTokenStore()
      ..tokens = const StoredTokens(accessToken: 'a.eyJleHAiOjk5OTk5OTk5OTl9.s', refreshToken: 'r');
    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
        overrides: [
          tokenStoreProvider.overrideWithValue(store),
          apiClientProvider.overrideWith(
            (ref) => ApiClient(baseUrl: 'https://api.test', store: store, language: () => 'en', adapter: api),
          ),
        ],
        child: MaterialApp(
          supportedLocales: AppLocalizations.supportedLocales,
          localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
          home: Scaffold(appBar: AppBar(actions: const [NotificationBell()])),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('2'), findsOneWidget);

    // A teacher replies: the badge updates on the next check without opening anything.
    api.unread = 3;
    await tester.pump(UnreadCountController.interval);
    await tester.pumpAndSettle();
    expect(find.text('3'), findsOneWidget);

    // In the background nothing is polled; coming back checks straight away.
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.hidden);
    final callsWhenHidden = api.calls;
    api.unread = 5;
    await tester.pump(UnreadCountController.interval * 3);
    expect(api.calls, callsWhenHidden);
    expect(find.text('3'), findsOneWidget);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
    await tester.pumpAndSettle();
    expect(find.text('5'), findsOneWidget);

    api.unread = 0;
    await tester.pump(UnreadCountController.interval);
    await tester.pumpAndSettle();
    expect(
      find.byType(Badge).evaluate().single.widget,
      isA<Badge>().having((b) => b.isLabelVisible, 'label visible', false),
    );
  });
}
