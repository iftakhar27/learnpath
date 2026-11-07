import 'dart:async';

import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/api/api_client.dart';
import '../../core/config.dart';
import '../../core/providers.dart';
import '../../l10n/app_localizations.dart';
import '../../widgets/common.dart';

class SplashScreen extends ConsumerWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unreachable = ref.watch(sessionProvider) is SessionUnreachable;
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const _Logo(size: 72),
            const SizedBox(height: 24),
            if (unreachable) ...[
              Text(context.l10n.serverUnreachable, style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: () => ref.read(sessionProvider.notifier).restore(), child: Text(context.l10n.retry)),
            ] else
              const CircularProgressIndicator(),
          ],
        ),
      ),
    );
  }
}

class _Logo extends StatelessWidget {
  const _Logo({this.size = 56});
  final double size;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Semantics(
      label: AppConfig.brandName,
      child: Container(
        width: size,
        height: size,
        decoration: BoxDecoration(color: scheme.primary, borderRadius: BorderRadius.circular(size / 4)),
        child: Icon(Icons.school_rounded, color: scheme.onPrimary, size: size * 0.55),
      ),
    );
  }
}

class LanguageToggle extends ConsumerWidget {
  const LanguageToggle({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final code = ref.watch(localeProvider).languageCode;
    return SegmentedButton<String>(
      segments: [
        ButtonSegment(value: 'en', label: Text(context.l10n.english)),
        ButtonSegment(value: 'hi', label: Text(context.l10n.hindi)),
      ],
      selected: {code},
      showSelectedIcon: false,
      onSelectionChanged: (s) => ref.read(localeProvider.notifier).set(s.first),
    );
  }
}

class WelcomeScreen extends ConsumerWidget {
  const WelcomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final session = ref.watch(sessionProvider);
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Align(alignment: Alignment.centerRight, child: LanguageToggle()),
              const Spacer(),
              const Center(child: _Logo(size: 80)),
              const SizedBox(height: 24),
              Text(
                AppConfig.brandName,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: 8),
              Text(l.appTagline, textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyLarge),
              if (session is SignedOut && session.expired) ...[
                const SizedBox(height: 16),
                Text(
                  session.replaced ? l.sessionReplaced : l.sessionExpired,
                  textAlign: TextAlign.center,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ],
              const Spacer(),
              FilledButton.icon(onPressed: () => context.push('/otp'), icon: const Icon(Icons.phone_android_rounded), label: Text(l.continueWithOtp)),
              const SizedBox(height: 12),
              OutlinedButton.icon(onPressed: () => context.push('/login'), icon: const Icon(Icons.mail_outline_rounded), label: Text(l.continueWithEmail)),
              const SizedBox(height: 8),
              TextButton(onPressed: () => context.push('/register'), child: Text(l.createAccount)),
            ],
          ),
        ),
      ),
    );
  }
}

final _emailPattern = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _obscure = true;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    await signInConfirmingTakeOver(context, (takeOver) => ref.read(sessionProvider.notifier).login(_email.text.trim(), _password.text, takeOver: takeOver));
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return Scaffold(
      appBar: AppBar(title: Text(l.signIn)),
      body: SafeArea(
        child: Form(
          key: _form,
          child: ListView(
            padding: const EdgeInsets.all(24),
            children: [
              TextFormField(
                controller: _email,
                keyboardType: TextInputType.emailAddress,
                autofillHints: const [AutofillHints.email],
                decoration: InputDecoration(labelText: l.email),
                validator: (v) => v == null || v.trim().isEmpty ? l.fieldRequired : (_emailPattern.hasMatch(v.trim()) ? null : l.invalidEmail),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _password,
                obscureText: _obscure,
                autofillHints: const [AutofillHints.password],
                decoration: InputDecoration(
                  labelText: l.password,
                  suffixIcon: IconButton(
                    tooltip: l.password,
                    icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                    onPressed: () => setState(() => _obscure = !_obscure),
                  ),
                ),
                validator: (v) => v == null || v.isEmpty ? l.fieldRequired : null,
                onFieldSubmitted: (_) => _submit(),
              ),
              const SizedBox(height: 24),
              AsyncButton(onPressed: _submit, expand: true, child: Text(l.signIn)),
              const SizedBox(height: 8),
              TextButton(onPressed: () => context.pushReplacement('/register'), child: Text(l.newHereCreateAccount)),
            ],
          ),
        ),
      ),
    );
  }
}

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _terms = false;
  bool _termsError = false;

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final valid = _form.currentState!.validate();
    setState(() => _termsError = !_terms);
    if (!valid || !_terms) return;
    await ref.read(sessionProvider.notifier).register(fullName: _name.text.trim(), email: _email.text.trim(), password: _password.text);
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return Scaffold(
      appBar: AppBar(title: Text(l.createAccount)),
      body: SafeArea(
        child: Form(
          key: _form,
          child: ListView(
            padding: const EdgeInsets.all(24),
            children: [
              TextFormField(
                controller: _name,
                textCapitalization: TextCapitalization.words,
                autofillHints: const [AutofillHints.name],
                decoration: InputDecoration(labelText: l.fullName),
                validator: (v) => v == null || v.trim().isEmpty ? l.fieldRequired : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _email,
                keyboardType: TextInputType.emailAddress,
                autofillHints: const [AutofillHints.email],
                decoration: InputDecoration(labelText: l.email),
                validator: (v) => v == null || v.trim().isEmpty ? l.fieldRequired : (_emailPattern.hasMatch(v.trim()) ? null : l.invalidEmail),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _password,
                obscureText: true,
                autofillHints: const [AutofillHints.newPassword],
                decoration: InputDecoration(labelText: l.password, helperText: l.passwordTooShort),
                validator: (v) => v == null || v.length < 8 ? l.passwordTooShort : null,
              ),
              const SizedBox(height: 12),
              TermsCheckbox(value: _terms, showError: _termsError, onChanged: (v) => setState(() => _terms = v)),
              const SizedBox(height: 16),
              AsyncButton(onPressed: _submit, expand: true, child: Text(l.createAccount)),
              TextButton(onPressed: () => context.pushReplacement('/login'), child: Text(l.haveAccountSignIn)),
            ],
          ),
        ),
      ),
    );
  }
}

class TermsCheckbox extends StatelessWidget {
  const TermsCheckbox({super.key, required this.value, required this.onChanged, this.showError = false});
  final bool value;
  final bool showError;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        CheckboxListTile(
          value: value,
          onChanged: (v) => onChanged(v ?? false),
          contentPadding: EdgeInsets.zero,
          controlAffinity: ListTileControlAffinity.leading,
          title: Text(l.acceptTerms),
          subtitle: showError ? Text(l.mustAcceptTerms, style: TextStyle(color: Theme.of(context).colorScheme.error)) : null,
        ),
        TextButton(
          onPressed: () => launchUrl(Uri.parse(AppConfig.termsUrl), mode: LaunchMode.externalApplication),
          child: Text(l.viewTerms),
        ),
      ],
    );
  }
}

/// Mobile number + OTP. New numbers get an account automatically; the name is used only then.
class OtpScreen extends ConsumerStatefulWidget {
  const OtpScreen({super.key});

  @override
  ConsumerState<OtpScreen> createState() => _OtpScreenState();
}

class _OtpScreenState extends ConsumerState<OtpScreen> {
  final _phoneForm = GlobalKey<FormState>();
  final _codeForm = GlobalKey<FormState>();
  final _phone = TextEditingController();
  final _code = TextEditingController();
  final _name = TextEditingController();
  bool _sent = false;
  bool _terms = false;
  bool _termsError = false;
  int _cooldown = 0;
  Timer? _timer;

  @override
  void dispose() {
    _timer?.cancel();
    _phone.dispose();
    _code.dispose();
    _name.dispose();
    super.dispose();
  }

  String get _digits => _phone.text.replaceAll(RegExp(r'\D'), '');

  void _startCooldown() {
    _cooldown = 30;
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 1), (t) {
      if (!mounted) return t.cancel();
      setState(() => _cooldown--);
      if (_cooldown <= 0) t.cancel();
    });
  }

  Future<void> _send() async {
    if (!_phoneForm.currentState!.validate()) return;
    await ref.read(apiProvider).requestOtp(_digits);
    setState(() => _sent = true);
    _startCooldown();
  }

  Future<void> _verify() async {
    final valid = _codeForm.currentState!.validate();
    setState(() => _termsError = !_terms);
    if (!valid || !_terms) return;
    await signInConfirmingTakeOver(
      context,
      (takeOver) => ref
          .read(sessionProvider.notifier)
          .verifyOtp(phone: _digits, code: _code.text.trim(), fullName: _name.text.trim().isEmpty ? null : _name.text.trim(), takeOver: takeOver),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return Scaffold(
      appBar: AppBar(title: Text(l.continueWithOtp)),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            Form(
              key: _phoneForm,
              child: TextFormField(
                controller: _phone,
                enabled: !_sent,
                keyboardType: TextInputType.phone,
                autofillHints: const [AutofillHints.telephoneNumberNational],
                inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(10)],
                decoration: InputDecoration(labelText: l.mobileNumber, prefixText: '+91 '),
                validator: (v) => RegExp(r'^[6-9]\d{9}$').hasMatch(v ?? '') ? null : l.invalidPhone,
              ),
            ),
            const SizedBox(height: 16),
            if (!_sent)
              AsyncButton(onPressed: _send, expand: true, child: Text(l.sendOtp))
            else
              Form(
                key: _codeForm,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(l.otpSentTo('+91 $_digits')),
                    TextButton(onPressed: () => setState(() => _sent = false), child: Text(l.changeNumber)),
                    TextFormField(
                      controller: _code,
                      autofocus: true,
                      keyboardType: TextInputType.number,
                      autofillHints: const [AutofillHints.oneTimeCode],
                      inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(6)],
                      decoration: InputDecoration(labelText: l.enterOtp),
                      validator: (v) => (v ?? '').length == 6 ? null : l.enterOtp,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _name,
                      textCapitalization: TextCapitalization.words,
                      decoration: InputDecoration(labelText: l.yourNameNewAccounts),
                    ),
                    const SizedBox(height: 8),
                    TermsCheckbox(value: _terms, showError: _termsError, onChanged: (v) => setState(() => _terms = v)),
                    const SizedBox(height: 8),
                    AsyncButton(onPressed: _verify, expand: true, child: Text(l.verify)),
                    TextButton(onPressed: _cooldown > 0 ? null : () => _send(), child: Text(_cooldown > 0 ? l.resendIn(_cooldown) : l.resendOtp)),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// "5 minutes ago" in the app's language.
String timeAgo(AppLocalizations l, Object? iso) {
  final at = iso is String ? DateTime.tryParse(iso) : null;
  if (at == null) return l.timeJustNow;
  final minutes = DateTime.now().difference(at).inMinutes;
  if (minutes < 1) return l.timeJustNow;
  if (minutes < 60) return l.timeMinutesAgo(minutes);
  final hours = (minutes / 60).round();
  return hours < 24 ? l.timeHoursAgo(hours) : l.timeDaysAgo((hours / 24).round());
}

/// Signs in; if the account is already signed in on another phone, asks first. Continuing signs that phone out.
Future<void> signInConfirmingTakeOver(BuildContext context, Future<void> Function(bool takeOver) signIn) async {
  try {
    await signIn(false);
  } on ApiException catch (e) {
    if (e.code != 'session_active' || !context.mounted) rethrow;
    final l = context.l10n;
    final device = (e.details['deviceName'] as String?) ?? '?';
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: Text(l.sessionActiveTitle),
        content: Text(l.sessionActiveBody(device, timeAgo(l, e.details['lastActiveAt']))),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: Text(l.cancel)),
          FilledButton(onPressed: () => Navigator.pop(c, true), child: Text(l.continueHere)),
        ],
      ),
    );
    if (ok == true) await signIn(true);
  }
}
