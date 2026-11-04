import 'package:flutter/rendering.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import 'app/router.dart';
import 'app/theme.dart';
import 'core/config.dart';
import 'core/providers.dart';
import 'l10n/app_localizations.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  if (AppConfig.enableSemantics) SemanticsBinding.instance.ensureSemantics();
  // API errors are shown in the UI; providers should not silently retry 4xx responses.
  runApp(ProviderScope(retry: (count, error) => null, child: const LearnPathApp()));
}

class LearnPathApp extends ConsumerWidget {
  const LearnPathApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return MaterialApp.router(
      title: AppConfig.brandName,
      debugShowCheckedModeBanner: false,
      theme: buildTheme(Brightness.light),
      darkTheme: buildTheme(Brightness.dark),
      locale: ref.watch(localeProvider),
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
      routerConfig: ref.watch(routerProvider),
    );
  }
}
