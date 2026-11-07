import 'dart:async';

import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';
import 'package:video_player/video_player.dart';

import '../../app/theme.dart';
import '../../core/api/learnpath_api.dart';
import '../../core/api/models.g.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../doubts/doubt_screens.dart';

final playbackProvider = FutureProvider.autoDispose.family<PlaybackDto, String>((ref, id) => ref.watch(apiProvider).playback(id));

class VideoScreen extends ConsumerWidget {
  const VideoScreen({super.key, required this.resourceId});
  final String resourceId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final playback = ref.watch(playbackProvider(resourceId));
    return Scaffold(
      appBar: AppBar(
        title: Text(context.l10n.video),
        actions: [if (playback.value case final p?) AskDoubtAction(resourceId: p.resourceId, about: p.title)],
      ),
      body: AsyncView(
        value: playback,
        onRetry: () => ref.invalidate(playbackProvider(resourceId)),
        data: (p) => p.streamUrl == null
            ? EmptyState(icon: Icons.videocam_off_outlined, message: context.l10n.videoUnavailable)
            : _Player(playback: p, api: ref.read(apiProvider)),
      ),
    );
  }
}

class _Player extends StatefulWidget {
  const _Player({required this.playback, required this.api});
  final PlaybackDto playback;
  final LearnPathApi api;

  @override
  State<_Player> createState() => _PlayerState();
}

class _PlayerState extends State<_Player> {
  late final VideoPlayerController _video = VideoPlayerController.networkUrl(Uri.parse(widget.playback.streamUrl!));
  Timer? _saver;
  bool _failed = false;
  bool _completed = false;
  TopicQuizSummaryDto? _quizPrompt; // set when this lesson completes the topic
  bool _fullscreen = false;
  int _lastSaved = -1;

  @override
  void initState() {
    super.initState();
    _start();
  }

  Future<void> _start() async {
    try {
      await _video.initialize();
      if (widget.playback.resumeAtSeconds > 5) {
        await _video.seekTo(Duration(seconds: widget.playback.resumeAtSeconds));
        if (mounted) showSnack(context, context.l10n.resumedAt(formatClock(widget.playback.resumeAtSeconds)));
      }
      await _video.play();
      _video.addListener(_onTick);
      // Progress is saved every 10 seconds while playing, on pause, and when leaving.
      _saver = Timer.periodic(const Duration(seconds: 10), (_) => _video.value.isPlaying ? _save() : null);
      if (mounted) setState(() {});
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    }
  }

  bool _wasPlaying = false;
  void _onTick() {
    final playing = _video.value.isPlaying;
    if (_wasPlaying && !playing) _save();
    _wasPlaying = playing;
    if (mounted) setState(() {});
  }

  Future<void> _save() async {
    final seconds = _video.value.position.inSeconds;
    if (seconds == _lastSaved) return;
    _lastSaved = seconds;
    try {
      final p = await widget.api.saveProgress(widget.playback.resourceId, seconds);
      if (p.completed && !_completed && mounted) setState(() => _completed = true);
      if (p.topicCompleted && p.quiz != null && p.quiz!.attempts == 0 && _quizPrompt == null && mounted) {
        setState(() => _quizPrompt = p.quiz);
      }
    } catch (_) {
      // Progress saving is best-effort; the next save retries.
    }
  }

  Future<void> _toggleFullscreen() async {
    setState(() => _fullscreen = !_fullscreen);
    if (_fullscreen) {
      await SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
      await SystemChrome.setPreferredOrientations([DeviceOrientation.landscapeLeft, DeviceOrientation.landscapeRight]);
    } else {
      await SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
      await SystemChrome.setPreferredOrientations(DeviceOrientation.values);
    }
  }

  @override
  void dispose() {
    _saver?.cancel();
    if (_video.value.isInitialized) {
      unawaited(widget.api.saveProgress(widget.playback.resourceId, _video.value.position.inSeconds).then((_) {}, onError: (_) {}));
    }
    _video.removeListener(_onTick);
    _video.dispose();
    if (_fullscreen) {
      SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
      SystemChrome.setPreferredOrientations(DeviceOrientation.values);
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    if (_failed) return EmptyState(icon: Icons.videocam_off_outlined, message: l.videoUnavailable);
    if (!_video.value.isInitialized) return const Center(child: CircularProgressIndicator());

    final v = _video.value;
    final player = AspectRatio(
      aspectRatio: v.aspectRatio,
      child: Stack(
        alignment: Alignment.bottomCenter,
        children: [
          VideoPlayer(_video),
          Container(
            color: Colors.black45,
            padding: const EdgeInsets.symmetric(horizontal: 4),
            child: Row(
              children: [
                IconButton(
                  tooltip: v.isPlaying ? l.pause : l.play,
                  color: Colors.white,
                  icon: Icon(v.isPlaying ? Icons.pause_rounded : Icons.play_arrow_rounded),
                  onPressed: () => v.isPlaying ? _video.pause() : _video.play(),
                ),
                Text(formatClock(v.position.inSeconds), style: const TextStyle(color: Colors.white)),
                Expanded(
                  child: Slider(
                    value: v.position.inMilliseconds.clamp(0, v.duration.inMilliseconds).toDouble(),
                    max: v.duration.inMilliseconds.toDouble().clamp(1, double.infinity),
                    onChanged: (ms) => _video.seekTo(Duration(milliseconds: ms.round())),
                    onChangeEnd: (_) => _save(),
                  ),
                ),
                Text(formatClock(v.duration.inSeconds), style: const TextStyle(color: Colors.white)),
                PopupMenuButton<double>(
                  tooltip: l.playbackSpeed,
                  icon: Text('${v.playbackSpeed}x', style: const TextStyle(color: Colors.white)),
                  onSelected: _video.setPlaybackSpeed,
                  itemBuilder: (_) => [
                    for (final s in [0.75, 1.0, 1.25, 1.5, 2.0]) PopupMenuItem(value: s, child: Text('${s}x')),
                  ],
                ),
                IconButton(
                  color: Colors.white,
                  tooltip: _fullscreen ? l.exitFullScreen : l.fullScreen,
                  icon: Icon(_fullscreen ? Icons.fullscreen_exit_rounded : Icons.fullscreen_rounded),
                  onPressed: _toggleFullscreen,
                ),
              ],
            ),
          ),
        ],
      ),
    );

    if (_fullscreen) {
      return ColoredBox(
        color: Colors.black,
        child: Center(child: player),
      );
    }
    return ListView(
      children: [
        player,
        if (_completed)
          Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                Icon(Icons.check_circle_rounded, color: Theme.of(context).colorScheme.success),
                const SizedBox(width: 8),
                Text(l.completed),
              ],
            ),
          ),
        if (_quizPrompt case final quiz?)
          Card(
            margin: const EdgeInsets.symmetric(horizontal: 16),
            color: Theme.of(context).colorScheme.primaryContainer,
            child: ListTile(
              leading: const Icon(Icons.fact_check_outlined),
              title: Text(l.topicDonePrompt(quiz.questionCount)),
              trailing: FilledButton(
                onPressed: () => context.push('/topic/${widget.playback.topicId}/quiz'),
                child: Text(l.startQuiz),
              ),
            ),
          ),
      ],
    );
  }
}
