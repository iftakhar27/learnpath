import '../../core/api/models.g.dart';

/// Outcome of the gateway's checkout sheet. Only a server-verified signature unlocks the course.
sealed class CheckoutOutcome {
  const CheckoutOutcome();
}

class CheckoutPaid extends CheckoutOutcome {
  const CheckoutPaid({required this.paymentId, required this.orderId, required this.signature});
  final String paymentId;
  final String orderId;
  final String signature;
}

class CheckoutFailed extends CheckoutOutcome {
  const CheckoutFailed(this.cancelled);
  final bool cancelled;
}

class CheckoutUnsupported extends CheckoutOutcome {
  const CheckoutUnsupported();
}

typedef OpenCheckout = Future<CheckoutOutcome> Function(CheckoutDto checkout, String brandName);
