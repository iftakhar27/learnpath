import 'dart:async';
import 'dart:io' show Platform;

import 'package:razorpay_flutter/razorpay_flutter.dart';

import '../../core/api/models.g.dart';
import 'razorpay_types.dart';

/// Opens Razorpay's native checkout (Android/iOS) for an order created by the API.
Future<CheckoutOutcome> openRazorpay(CheckoutDto checkout, String brandName) async {
  if (!Platform.isAndroid && !Platform.isIOS) return const CheckoutUnsupported();
  final razorpay = Razorpay();
  final done = Completer<CheckoutOutcome>();
  razorpay
    ..on(Razorpay.EVENT_PAYMENT_SUCCESS, (PaymentSuccessResponse r) {
      if (!done.isCompleted) {
        done.complete(CheckoutPaid(paymentId: r.paymentId ?? '', orderId: r.orderId ?? checkout.gatewayOrderId, signature: r.signature ?? ''));
      }
    })
    ..on(Razorpay.EVENT_PAYMENT_ERROR, (PaymentFailureResponse r) {
      if (!done.isCompleted) done.complete(CheckoutFailed(r.code == Razorpay.PAYMENT_CANCELLED));
    })
    ..on(Razorpay.EVENT_EXTERNAL_WALLET, (ExternalWalletResponse r) {
      // External wallets finish outside the app; the server webhook confirms the payment.
      if (!done.isCompleted) done.complete(const CheckoutFailed(false));
    });
  razorpay.open({
    'key': checkout.keyId,
    'amount': checkout.amountInPaise,
    'currency': checkout.currency,
    'order_id': checkout.gatewayOrderId,
    'name': brandName,
    'description': checkout.courseTitle,
    'prefill': {'email': ?checkout.prefillEmail, 'contact': ?checkout.prefillPhone},
  });
  try {
    return await done.future;
  } finally {
    razorpay.clear();
  }
}
