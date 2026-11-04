import 'package:material_ui/material_ui.dart';

/// Design tokens shared with the web portal (docs/06-screen-map.md).
abstract final class Brand {
  static const primary = Color(0xFF4F46E5);
  static const primaryDark = Color(0xFF818CF8);
  static const accent = Color(0xFFF59E0B);
  static const success = Color(0xFF16A34A);
  static const successDark = Color(0xFF4ADE80);
  static const danger = Color(0xFFDC2626);
  static const dangerDark = Color(0xFFF87171);
  static const warning = Color(0xFFB45309);
  static const background = Color(0xFFF7F7FB);
  static const backgroundDark = Color(0xFF0F1018);
  static const surfaceDark = Color(0xFF1A1B26);
}

extension SemanticColors on ColorScheme {
  bool get _light => brightness == Brightness.light;
  Color get success => _light ? Brand.success : Brand.successDark;
  Color get successContainer => _light ? const Color(0xFFE8F6EE) : const Color(0xFF16301F);
  Color get danger => _light ? Brand.danger : Brand.dangerDark;
  Color get dangerContainer => _light ? const Color(0xFFFDECEC) : const Color(0xFF3A1C1C);
  Color get warning => _light ? Brand.warning : const Color(0xFFFBBF24);
  Color get warningContainer => _light ? const Color(0xFFFDF3E2) : const Color(0xFF372A12);
  Color get accent => Brand.accent;
}

ThemeData buildTheme(Brightness brightness) {
  final light = brightness == Brightness.light;
  final scheme = ColorScheme.fromSeed(seedColor: Brand.primary, brightness: brightness).copyWith(
    primary: light ? Brand.primary : Brand.primaryDark,
    error: light ? Brand.danger : Brand.dangerDark,
    surface: light ? Colors.white : Brand.surfaceDark,
  );
  final shape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(12));
  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: light ? Brand.background : Brand.backgroundDark,
    cardTheme: CardThemeData(
      elevation: 0,
      shape: shape.copyWith(side: BorderSide(color: scheme.outlineVariant)),
      margin: EdgeInsets.zero,
    ),
    appBarTheme: AppBarTheme(centerTitle: false, backgroundColor: light ? Brand.background : Brand.backgroundDark, scrolledUnderElevation: 0),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: scheme.surface,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        minimumSize: const Size(64, 48),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        minimumSize: const Size(64, 48),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    ),
    chipTheme: ChipThemeData(shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999))),
    snackBarTheme: const SnackBarThemeData(behavior: SnackBarBehavior.floating),
  );
}
