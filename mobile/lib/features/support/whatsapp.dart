import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/api/models.g.dart';
import '../../core/config.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../payments/buy_on_website.dart' show openUrlProvider;

/// Server settings read once per launch: the team's WhatsApp number and channel (hidden when not set), the website.
final appConfigProvider = FutureProvider<AppConfigDto>((ref) async {
  try {
    return await ref.watch(apiProvider).appConfig();
  } catch (_) {
    return const AppConfigDto(); // offline: no WhatsApp links rather than an error
  }
});

/// The website's address for shared links (from the server, else the one the app was built with).
String websiteUrl(AppConfigDto? config) => (config?.websiteUrl ?? AppConfig.webBaseUrl).replaceAll(RegExp(r'/+$'), '');

/// A chat with the team, the message typed in for the student to send.
Uri whatsAppChatUri(String number, String message) => Uri.https('wa.me', '/$number', {'text': message});

/// WhatsApp's "send to…" picker with the message ready.
Uri whatsAppShareUri(String message) => Uri.https('wa.me', '/', {'text': message});

/// Opens a WhatsApp link (the app if installed, else WhatsApp Web) and says so if nothing could open it.
Future<void> openWhatsApp(BuildContext context, WidgetRef ref, Uri uri) async {
  final opened = await ref.read(openUrlProvider)(uri);
  if (!opened && context.mounted) showSnack(context, context.l10n.errorGeneric, error: true);
}

Future<void> shareAppOnWhatsApp(BuildContext context, WidgetRef ref) async {
  final config = await ref.read(appConfigProvider.future);
  if (!context.mounted) return;
  await openWhatsApp(
    context,
    ref,
    whatsAppShareUri(context.l10n.shareAppMessage(AppConfig.brandName, websiteUrl(config))),
  );
}

Future<void> shareCourseOnWhatsApp(BuildContext context, WidgetRef ref, CourseSummaryDto course) async {
  final config = await ref.read(appConfigProvider.future);
  if (!context.mounted) return;
  final url = '${websiteUrl(config)}/learn/${course.id}';
  await openWhatsApp(
    context,
    ref,
    whatsAppShareUri(context.l10n.shareCourseMessage(course.title, AppConfig.brandName, url)),
  );
}

/// "Chat on WhatsApp", "Join our WhatsApp channel" (each only when set on the server) and "Invite friends".
/// [beforeOpen] runs first (the side menu closes itself).
class WhatsAppTiles extends ConsumerWidget {
  const WhatsAppTiles({super.key, this.beforeOpen, this.share = true, this.padding});
  final VoidCallback? beforeOpen;
  final bool share;

  /// To line up with the rows around it (null: the list tile default).
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final config = ref.watch(appConfigProvider).value;
    void open(Uri uri) {
      beforeOpen?.call();
      openWhatsApp(context, ref, uri);
    }

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (config?.whatsAppNumber case final number?)
          ListTile(
            contentPadding: padding,
            leading: const Icon(Icons.chat_outlined),
            title: Text(l.whatsAppChat),
            trailing: const Icon(Icons.open_in_new_rounded, size: 18),
            onTap: () => open(whatsAppChatUri(number, l.whatsAppHello(AppConfig.brandName))),
          ),
        if (config?.whatsAppChannelUrl case final channel?)
          ListTile(
            contentPadding: padding,
            leading: const Icon(Icons.campaign_outlined),
            title: Text(l.whatsAppChannel),
            trailing: const Icon(Icons.open_in_new_rounded, size: 18),
            onTap: () => open(Uri.parse(channel)),
          ),
        if (share)
          ListTile(
            contentPadding: padding,
            leading: const Icon(Icons.share_outlined),
            title: Text(l.inviteFriends),
            trailing: const Icon(Icons.open_in_new_rounded, size: 18),
            onTap: () {
              beforeOpen?.call();
              shareAppOnWhatsApp(context, ref);
            },
          ),
      ],
    );
  }
}
