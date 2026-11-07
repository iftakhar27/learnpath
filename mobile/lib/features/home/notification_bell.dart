import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/providers.dart';
import '../../widgets/common.dart';

/// Unread notifications for the bell. Checked every 30 seconds while the app is on screen and as soon as it comes
/// back to the foreground; paused in the background. (Alerts while the app is closed need push notifications.)
class UnreadCountController extends Notifier<int> {
  static const interval = Duration(seconds: 30);
  Timer? _timer;
  AppLifecycleListener? _lifecycle;

  @override
  int build() {
    _lifecycle = AppLifecycleListener(onResume: _start, onHide: _stop);
    ref.onDispose(() {
      _stop();
      _lifecycle?.dispose();
    });
    _start();
    return 0;
  }

  void _start() {
    _stop();
    unawaited(refresh());
    _timer = Timer.periodic(interval, (_) => refresh());
  }

  void _stop() {
    _timer?.cancel();
    _timer = null;
  }

  Future<void> refresh() async {
    try {
      final count = await ref.read(apiProvider).unreadCount();
      if (ref.mounted) state = count;
    } catch (_) {
      // Best effort: the next check retries.
    }
  }
}

final unreadCountProvider = NotifierProvider.autoDispose<UnreadCountController, int>(UnreadCountController.new);

class NotificationBell extends ConsumerWidget {
  const NotificationBell({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread = ref.watch(unreadCountProvider);
    return IconButton(
      tooltip: context.l10n.notificationsTitle,
      onPressed: () async {
        await context.push('/notifications');
        unawaited(ref.read(unreadCountProvider.notifier).refresh());
      },
      icon: Badge(
        isLabelVisible: unread > 0,
        label: Text(unread > 99 ? '99+' : '$unread'),
        child: const Icon(Icons.notifications_none_rounded),
      ),
    );
  }
}
