import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/api/models.g.dart';
import '../../core/config.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';

/// Opens a URL outside the app (tests replace it).
final openUrlProvider = Provider<Future<bool> Function(Uri)>(
  (ref) =>
      (uri) => launchUrl(uri, mode: LaunchMode.externalApplication),
);

/// Where "Buy on website" goes: the course page with checkout open, signed in with a one-time [code] when there is one.
Uri websiteBuyUri(String courseId, {String? code}) {
  final next = '/learn/$courseId?buy=1&from=app';
  return code == null
      ? Uri.parse('${AppConfig.webBaseUrl}$next')
      : Uri.parse('${AppConfig.webBaseUrl}/handoff').replace(queryParameters: {'code': code, 'next': next});
}

/// Courses are bought on the website: this opens it signed in, with checkout started. When the student comes back to
/// the app, [onReturn] runs so the course shows as unlocked. Access is granted by the server after the payment is verified.
class BuyOnWebsiteButton extends ConsumerStatefulWidget {
  const BuyOnWebsiteButton({super.key, required this.course, required this.onReturn});
  final CourseSummaryDto course;
  final VoidCallback onReturn;

  @override
  ConsumerState<BuyOnWebsiteButton> createState() => _BuyOnWebsiteButtonState();
}

class _BuyOnWebsiteButtonState extends ConsumerState<BuyOnWebsiteButton> {
  AppLifecycleListener? _lifecycle;

  @override
  void initState() {
    super.initState();
    // While this button is shown, coming back from the browser refreshes the course.
    _lifecycle = AppLifecycleListener(onResume: () => widget.onReturn());
  }

  @override
  void dispose() {
    _lifecycle?.dispose();
    super.dispose();
  }

  Future<void> _open() async {
    String? code;
    try {
      code = (await ref.read(apiProvider).createHandoff()).code;
    } catch (_) {
      // Still open the website; the student signs in there.
    }
    final opened = await ref.read(openUrlProvider)(websiteBuyUri(widget.course.id, code: code));
    if (!opened && mounted) showSnack(context, context.l10n.errorGeneric, error: true);
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        AsyncButton(
          expand: true,
          onPressed: _open,
          child: Text(l.buyOnWebsite(formatPaise(widget.course.priceInPaise))),
        ),
        const SizedBox(height: 6),
        Text(l.buyOnWebsiteHint, style: Theme.of(context).textTheme.bodySmall),
      ],
    );
  }
}
