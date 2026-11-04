import '../../core/api/models.g.dart';
import 'razorpay_types.dart';

/// Web and other platforms without the Razorpay SDK.
Future<CheckoutOutcome> openRazorpay(CheckoutDto checkout, String brandName) async => const CheckoutUnsupported();
