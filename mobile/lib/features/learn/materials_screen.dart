import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart' hide MaterialType;

import '../../core/api/models.g.dart';
import '../../core/config.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../payments/buy_on_website.dart' show openUrlProvider;
import 'subjects.dart' show subjectColor;

typedef MaterialFilter = ({String? subject, String? classLevel, MaterialType? type});

final materialsProvider = FutureProvider.autoDispose.family<List<MaterialDto>, MaterialFilter>(
  (ref, f) => ref.watch(apiProvider).materials(subject: f.subject, classLevel: f.classLevel, type: f.type),
);

String materialTypeLabel(BuildContext context, String type) {
  final l = context.l10n;
  return switch (type) {
    'Notes' => l.materialTypeNotes,
    'FormulaSheet' => l.materialTypeFormulaSheet,
    'MindMap' => l.materialTypeMindMap,
    'PreviousYearPaper' => l.materialTypePreviousYearPaper,
    'SamplePaper' => l.materialTypeSamplePaper,
    _ => l.materialTypeOther,
  };
}

String _size(int bytes) => bytes >= 1024 * 1024
    ? '${(bytes / 1024 / 1024).toStringAsFixed(1)} MB'
    : '${(bytes / 1024).round().clamp(1, 1 << 30)} KB';

/// Free downloads: notes, formula sheets, mind maps, past papers. Guests see the list and are asked to sign up to
/// download; signed-in students get a link that opens in the browser (PDF viewer or download).
class MaterialsScreen extends ConsumerStatefulWidget {
  const MaterialsScreen({super.key});

  @override
  ConsumerState<MaterialsScreen> createState() => _MaterialsScreenState();
}

class _MaterialsScreenState extends ConsumerState<MaterialsScreen> {
  String? _subject;
  String? _class;
  MaterialType? _type;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final filter = (subject: _subject, classLevel: _class, type: _type);
    final list = ref.watch(materialsProvider(filter));
    Widget chips<T>(List<(T, String)> options, T selected, void Function(T) onSelect) => SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Row(
        children: [
          for (final (value, label) in options)
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: ChoiceChip(label: Text(label), selected: selected == value, onSelected: (_) => onSelect(value)),
            ),
        ],
      ),
    );

    return Scaffold(
      appBar: AppBar(title: Text(l.freeMaterialTitle)),
      body: RefreshableList(
        onRefresh: () async => ref.invalidate(materialsProvider(filter)),
        children: [
          Padding(padding: const EdgeInsets.fromLTRB(16, 4, 16, 8), child: Text(l.freeMaterialIntro)),
          chips<String?>(
            [
              (null, l.allSubjects),
              ('Physics', l.physicsShort),
              ('Chemistry', l.chemistryShort),
              ('Biology', l.biologyShort),
              ('general', l.subjectGeneral),
            ],
            _subject,
            (v) => setState(() => _subject = v),
          ),
          chips<String?>(
            [(null, l.bothClasses), ('11', l.classLevel('11')), ('12', l.classLevel('12'))],
            _class,
            (v) => setState(() => _class = v),
          ),
          chips<MaterialType?>(
            [(null, l.allTypes), for (final t in MaterialType.values) (t, materialTypeLabel(context, t.wire))],
            _type,
            (v) => setState(() => _type = v),
          ),
          const SizedBox(height: 8),
          ...switch (list) {
            AsyncData(:final value) when value.isEmpty => [
              Padding(
                padding: const EdgeInsets.all(24),
                child: Text(l.materialsEmpty, textAlign: TextAlign.center),
              ),
            ],
            AsyncData(:final value) => [
              for (final m in value)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 10),
                  child: _MaterialCard(material: m),
                ),
            ],
            AsyncError(:final error) => [
              ErrorView(error: error, onRetry: () => ref.invalidate(materialsProvider(filter))),
            ],
            _ => [
              const Padding(
                padding: EdgeInsets.all(32),
                child: Center(child: CircularProgressIndicator()),
              ),
            ],
          },
        ],
      ),
    );
  }
}

class _MaterialCard extends ConsumerWidget {
  const _MaterialCard({required this.material});
  final MaterialDto material;

  Future<void> _open(BuildContext context, WidgetRef ref) async {
    final link = await ref.read(apiProvider).materialDownload(material.id);
    final uri = Uri.parse(link.external ? link.url : '${AppConfig.apiBaseUrl}${link.url}');
    final opened = await ref.read(openUrlProvider)(uri);
    if (!opened && context.mounted) showSnack(context, context.l10n.errorGeneric, error: true);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final m = material;
    final guest = ref.watch(sessionProvider) is Guest;
    final dark = Theme.of(context).brightness == Brightness.dark;
    final color = m.subject == null ? Theme.of(context).colorScheme.primary : subjectColor(context, m.subject!);
    final isLink = m.format == 'Link';
    final subject = switch (m.subject) {
      'Physics' => l.physicsShort,
      'Chemistry' => l.chemistryShort,
      'Biology' => l.biologyShort,
      _ => l.subjectGeneral,
    };
    // Not one merged node: the Download button must stay reachable on its own for screen readers.
    return Card(
      semanticContainer: false,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 44,
                  height: 44,
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: dark ? 0.22 : 0.12),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Icon(
                    isLink
                        ? Icons.open_in_new_rounded
                        : (m.format == 'Image' ? Icons.image_outlined : Icons.picture_as_pdf_outlined),
                    color: color,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(m.title, style: const TextStyle(fontWeight: FontWeight.w600)),
                      if (m.description case final d?) Text(d, style: Theme.of(context).textTheme.bodySmall),
                      const SizedBox(height: 4),
                      Text(
                        [
                          subject,
                          m.classLevel == null ? l.bothClasses : l.classLevel(m.classLevel!),
                          materialTypeLabel(context, m.type),
                        ].join(' · '),
                        style: Theme.of(context).textTheme.labelSmall,
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            // The button moves under the size when they don't fit side by side (long labels, Hindi, big text).
            Wrap(
              alignment: WrapAlignment.spaceBetween,
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 12,
              runSpacing: 8,
              children: [
                Text(
                  isLink ? l.materialLink : '${m.format == 'PDF' ? 'PDF' : l.materialImage} · ${_size(m.sizeBytes)}',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                if (guest)
                  OutlinedButton(onPressed: () => context.push('/register'), child: Text(l.signUpToDownload))
                else
                  Semantics(
                    label: '${isLink ? l.openLink : l.download}: ${m.title}',
                    excludeSemantics: true,
                    button: true,
                    child: AsyncButton(
                      onPressed: () => _open(context, ref),
                      child: Text(isLink ? l.openLink : l.download),
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// The way in from Learn and the guest home.
class FreeMaterialCard extends StatelessWidget {
  const FreeMaterialCard({super.key});

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return Card(
      margin: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: ListTile(
        leading: Icon(Icons.download_outlined, color: Theme.of(context).colorScheme.primary),
        title: Text(l.freeMaterialTitle, style: const TextStyle(fontWeight: FontWeight.w600)),
        subtitle: Text(l.freeMaterialIntro),
        trailing: const Icon(Icons.chevron_right_rounded),
        onTap: () => context.push('/materials'),
      ),
    );
  }
}
