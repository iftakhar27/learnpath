import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import '../app/theme.dart';
import '../core/api/api_client.dart';
import '../l10n/app_localizations.dart';

extension L10nX on BuildContext {
  AppLocalizations get l10n => AppLocalizations.of(this);
  String get lang => Localizations.localeOf(this).languageCode;
}

/// User-facing message for any error: localized for network/unknown errors, the server's message otherwise.
String errorText(BuildContext context, Object error) {
  final l = context.l10n;
  if (error is ApiException) {
    if (error.isNetwork) return l.errorNetwork;
    if (error.status >= 500 || error.status <= 0) return l.errorGeneric;
    if (error.needsPurchase) return l.purchaseRequired;
    return error.message;
  }
  return l.errorGeneric;
}

void showSnack(BuildContext context, String message, {bool error = false}) {
  final scheme = Theme.of(context).colorScheme;
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message), backgroundColor: error ? scheme.danger : null));
}

/// Loading / error / data rendering for a provider value, with retry.
class AsyncView<T> extends StatelessWidget {
  const AsyncView({super.key, required this.value, required this.data, this.onRetry, this.loading});
  final AsyncValue<T> value;
  final Widget Function(T data) data;
  final VoidCallback? onRetry;
  final Widget? loading;

  @override
  Widget build(BuildContext context) => switch (value) {
    AsyncData(:final value) => data(value),
    AsyncError(:final error) => ErrorView(error: error, onRetry: onRetry),
    _ =>
      loading ??
          const Center(
            child: Padding(padding: EdgeInsets.all(32), child: CircularProgressIndicator()),
          ),
  };
}

class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.error, this.onRetry});
  final Object error;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final e = error;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              e is ApiException && e.isNetwork ? Icons.wifi_off_rounded : Icons.error_outline_rounded,
              size: 40,
              color: Theme.of(context).colorScheme.danger,
            ),
            const SizedBox(height: 12),
            Text(errorText(context, error), textAlign: TextAlign.center),
            if (onRetry != null) ...[const SizedBox(height: 12), OutlinedButton(onPressed: onRetry, child: Text(context.l10n.retry))],
          ],
        ),
      ),
    );
  }
}

class EmptyState extends StatelessWidget {
  const EmptyState({super.key, required this.icon, required this.message, this.action});
  final IconData icon;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 44, color: Theme.of(context).colorScheme.outline),
          const SizedBox(height: 12),
          Text(message, textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyLarge),
          if (action != null) ...[const SizedBox(height: 16), action!],
        ],
      ),
    ),
  );
}

class SectionHeader extends StatelessWidget {
  const SectionHeader(this.title, {super.key, this.action});
  final String title;
  final Widget? action;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(16, 20, 8, 8),
    child: Row(
      children: [
        Expanded(
          child: Text(title, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w600)),
        ),
        ?action,
      ],
    ),
  );
}

class Pill extends StatelessWidget {
  const Pill(this.label, {super.key, this.color, this.background});
  final String label;
  final Color? color;
  final Color? background;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(color: background ?? scheme.surfaceContainerHighest, borderRadius: BorderRadius.circular(999)),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(color: color ?? scheme.onSurfaceVariant, fontWeight: FontWeight.w600),
      ),
    );
  }
}

/// A button that shows a spinner while its async action runs and reports failures in a snackbar.
class AsyncButton extends StatefulWidget {
  const AsyncButton({super.key, required this.onPressed, required this.child, this.outlined = false, this.expand = false});
  final Future<void> Function()? onPressed;
  final Widget child;
  final bool outlined;
  final bool expand;

  @override
  State<AsyncButton> createState() => _AsyncButtonState();
}

class _AsyncButtonState extends State<AsyncButton> {
  bool _busy = false;

  Future<void> _run() async {
    setState(() => _busy = true);
    try {
      await widget.onPressed!();
    } catch (e) {
      if (mounted) showSnack(context, errorText(context, e), error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final onPressed = widget.onPressed == null || _busy ? null : _run;
    final child = _busy ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2)) : widget.child;
    final button = widget.outlined ? OutlinedButton(onPressed: onPressed, child: child) : FilledButton(onPressed: onPressed, child: child);
    return widget.expand ? SizedBox(width: double.infinity, child: button) : button;
  }
}

/// Wraps provider-backed lists so pull-to-refresh invalidates the provider.
class RefreshableList extends ConsumerWidget {
  const RefreshableList({super.key, required this.onRefresh, required this.children, this.padding});
  final Future<void> Function() onRefresh;
  final List<Widget> children;
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context, WidgetRef ref) => RefreshIndicator(
    onRefresh: onRefresh,
    child: ListView(physics: const AlwaysScrollableScrollPhysics(), padding: padding ?? const EdgeInsets.only(bottom: 24), children: children),
  );
}
