import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/api/models.g.dart';
import '../../core/config.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import 'razorpay_stub.dart' if (dart.library.io) 'razorpay_mobile.dart';
import 'razorpay_types.dart';

/// Lets tests replace the native checkout sheet.
final openCheckoutProvider = Provider<OpenCheckout>((ref) => openRazorpay);

/// Creates an order, opens the payment sheet, then asks the API to verify the gateway signature.
/// Access is granted by the server only after verification (or the payment webhook), never by the app.
class BuyButton extends ConsumerWidget {
  const BuyButton({super.key, required this.course, required this.onPurchased});
  final CourseSummaryDto course;
  final VoidCallback onPurchased;

  Future<void> _buy(BuildContext context, WidgetRef ref) async {
    final l = context.l10n;
    final api = ref.read(apiProvider);
    final checkout = await api.createOrder(course.id);
    final outcome = await ref.read(openCheckoutProvider)(checkout, AppConfig.brandName);
    if (!context.mounted) return;
    switch (outcome) {
      case CheckoutUnsupported():
        showSnack(context, l.purchaseOnMobileOnly);
      case CheckoutFailed(:final cancelled):
        if (!cancelled) showSnack(context, l.paymentFailed, error: true);
      case CheckoutPaid(:final paymentId, :final orderId, :final signature):
        showSnack(context, l.paymentVerifying);
        final order = await api.verifyPayment(VerifyPaymentRequest(gatewayOrderId: orderId, gatewayPaymentId: paymentId, signature: signature));
        if (!context.mounted) return;
        if (order.status == 'Paid') {
          showSnack(context, l.paymentSuccess);
          onPurchased();
        } else {
          showSnack(context, l.paymentFailed, error: true);
        }
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) =>
      AsyncButton(expand: true, onPressed: () => _buy(context, ref), child: Text(context.l10n.buyFor(formatPaise(course.priceInPaise))));
}
