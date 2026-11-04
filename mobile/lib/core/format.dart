import 'package:intl/intl.dart';

final _inr = NumberFormat.currency(locale: 'en_IN', symbol: '₹', decimalDigits: 0);
final _number = NumberFormat.decimalPattern('en_IN');

/// Prices are stored in paise (₹1 = 100 paise) and include GST.
String formatPaise(int paise) => _inr.format(paise / 100);

String formatNumber(num n) => n == n.roundToDouble() ? _number.format(n) : n.toStringAsFixed(n.abs() < 10 ? 2 : 1);

/// "12:05" or "1:02:05".
String formatClock(int totalSeconds) {
  final s = totalSeconds < 0 ? 0 : totalSeconds;
  final h = s ~/ 3600, m = (s % 3600) ~/ 60, sec = s % 60;
  String two(int v) => v.toString().padLeft(2, '0');
  return h > 0 ? '$h:${two(m)}:${two(sec)}' : '$m:${two(sec)}';
}

/// Dates are shown in India Standard Time regardless of the device setting.
String formatDate(DateTime utc, String locale) =>
    DateFormat.yMMMd(locale == 'hi' ? 'hi' : 'en_IN').format(utc.toUtc().add(const Duration(hours: 5, minutes: 30)));

String formatDateTime(DateTime utc, String locale) =>
    DateFormat.yMMMd(locale == 'hi' ? 'hi' : 'en_IN').add_jm().format(utc.toUtc().add(const Duration(hours: 5, minutes: 30)));
