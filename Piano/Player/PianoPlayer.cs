using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Piano.Parser;
using Piano.PianoConsole;
using Piano.Player;

namespace Piano.Player
{
    /// <summary>
    /// 钢琴音播放器类
    /// </summary>
public class PianoPlayer : IDisposable, IPlaybackController
    {
        #region 成员变量和构造函数
        
        private bool _isPlaying = false;
        private bool _disposed = false;
        private CancellationTokenSource? _cancellationTokenSource;
        private AudioPlayer _audioPlayer;
        // private WaveOutEvent? _waveOut;

        public PianoPlayer()
        {
            _audioPlayer = new AudioPlayer();
        }
        
        #endregion

        #region 公共播放方法
        
        /// <summary>
        /// 播放音符
        /// </summary>
        /// <param name="noteName">音符名称</param>
        /// <param name="octave">八度</param>
        /// <param name="duration">持续时间（毫秒）</param>
        /// <param name="volume">音量（0.0-1.0）</param>
        /// <param name="hasVibrato">是否有颤音效果</param>
        public void PlayNote(string noteName, int octave, int duration, double volume = 1.0, bool hasVibrato = false)
        {
            // 应用当前音量设置
            double adjustedVolume = volume * _currentVolume;
            
            // 获取音符频率
            double frequency = AudioPlayer.GetNoteFrequency(noteName, octave);
            
            // 播放音符（带颤音效果）
            PlayNoteWithVibrato(frequency, duration, adjustedVolume, hasVibrato);
            
            // 输出信息到控制台
            string vibratoText = hasVibrato ? " (颤音)" : "";
            DebugOutputManager.DebugWriteLine($"演奏音符: {noteName}{octave} ({frequency:F2}Hz), 持续时间: {duration}ms, 音量: {adjustedVolume:F2}{vibratoText}");
        }

        /// <summary>
        /// 播放滑音
        /// </summary>
        /// <param name="startNoteName">起始音符名称</param>
        /// <param name="startOctave">起始音符八度</param>
        /// <param name="endNoteName">结束音符名称</param>
        /// <param name="endOctave">结束音符八度</param>
        /// <param name="duration">持续时间（毫秒）</param>
        /// <param name="volume">音量（0.0-1.0）</param>
        public void PlayGlissando(string startNoteName, int startOctave, string endNoteName, int endOctave, int duration, double volume = 1.0)
        {
            // 应用当前音量设置
            double adjustedVolume = volume * _currentVolume;
            
            // 获取起始和结束音符频率
            double startFrequency = AudioPlayer.GetNoteFrequency(startNoteName, startOctave);
            double endFrequency = AudioPlayer.GetNoteFrequency(endNoteName, endOctave);
            
            // 播放滑音
            PlayGlissandoEffect(startFrequency, endFrequency, duration, adjustedVolume);
            
            // 输出信息到控制台
            DebugOutputManager.DebugWriteLine($"演奏滑音: {startNoteName}{startOctave} -> {endNoteName}{endOctave}, 持续时间: {duration}ms, 音量: {adjustedVolume}");
        }

        public void PlayRest(int duration)
        {
            // 调整休止符持续时间以匹配速度设置
            int adjustedDuration = (int)(duration * _currentSpeedFactor);
            _audioPlayer.PlayRest(adjustedDuration);
            DebugOutputManager.DebugWriteLine($"演奏休止符: 持续时间 {adjustedDuration}ms (原始: {duration}ms)");
        }

        /// <summary>
        /// 播放和弦
        /// </summary>
        /// <param name="chord">和弦节点</param>
        /// <param name="duration">持续时间（毫秒）</param>
        /// <param name="volume">音量（0.0-1.0）</param>
        public void PlayChord(ChordNode chord, int duration, double volume = 1.0)
        {
            // 应用当前音量设置
            double adjustedVolume = volume * _currentVolume;
            
            // 输出信息到控制台
            StringBuilder chordInfo = new StringBuilder();
            chordInfo.Append("{");
            for (int i = 0; i < chord.Notes.Count; i++)
            {
                if (i > 0) chordInfo.Append(",");
                chordInfo.Append($"{chord.Notes[i].NoteName}{chord.Notes[i].Octave}");
            }
            chordInfo.Append($"}}({chord.Duration})");
            
            DebugOutputManager.DebugWriteLine($"演奏和弦: {chordInfo}, 持续时间: {duration}ms, 音量: {adjustedVolume:F2}");
            
            // 为和弦创建混合音频信号
            var mixer = new NAudio.Wave.SampleProviders.MixingSampleProvider(
                NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
            
            // 允许不同格式的输入（解决波形格式不匹配问题）
            mixer.ReadFully = true;
            
            // 添加和弦中的所有音符到混合器
            foreach (var note in chord.Notes)
            {
                // 获取音符频率
                double frequency = AudioPlayer.GetNoteFrequency(note.NoteName, note.Octave);
                
                // 创建带淡入淡出效果的音符信号
                var fadedNote = new FadeInOutNoteProvider(frequency, duration, adjustedVolume / chord.Notes.Count, false);
                mixer.AddMixerInput(fadedNote);
            }
            
            // 播放混合后的和弦
            _audioPlayer.PlayMixer(mixer, duration);
        }

        /// <summary>
        /// 播放音乐作品
        /// </summary>
        /// <param name="score">音乐作品抽象语法树</param>
        public void PlayScore(ScoreNode score)
        {
            _isPlaying = true;
            _isPaused = false;
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                // 设置默认BPM
                int bpm = score.Tempo?.BPM ?? 120;
                
                // 播放声部
                if (score.Voices.Count > 0)
                {
                    // 计算最大音符数（跨所有小节和声部）
                    int maxNotes = 0;
                    foreach (var voice in score.Voices)
                    {
                        int voiceNoteCount = 0;
                        foreach (var measure in voice.Measures)
                        {
                            voiceNoteCount += measure.Elements.Count;
                        }
                        if (voiceNoteCount > maxNotes)
                            maxNotes = voiceNoteCount;
                    }
                    
                    // 按音符位置顺序播放所有声部，同时播放多个声部的对应音符
                    for (int noteIndex = 0; noteIndex < maxNotes && _isPlaying && !_isPaused; noteIndex++)
                    {
                        // 在每个音符位置检查暂停状态
                        if (_isPaused)
                        {
                            // 保存暂停状态
                            _pausedScore = score;
                            _pausedBpm = bpm;
                            _pausedVoiceIndex = 0; // 从第一个声部开始
                            _pausedNoteIndex = noteIndex;
                            _pausedSectionIndex = 0;
                            _pausedMeasureIndex = 0;
                            _pausedElementIndex = 0;
                            
                            // 退出播放循环
                            break;
                        }
                        
                        // 收集当前音符位置的所有声部元素
                        var allElements = new List<(AstNode element, int voiceIndex)>();
                        
                        foreach (var voice in score.Voices)
                        {
                            if (!_isPlaying || _isPaused) break;
                            
                            // 计算当前声部在当前音符位置的元素
                            int currentNoteIndex = noteIndex;
                            AstNode? element = null;
                            
                            foreach (var measure in voice.Measures)
                            {
                                if (currentNoteIndex < measure.Elements.Count)
                                {
                                    element = measure.Elements[currentNoteIndex];
                                    break;
                                }
                                else
                                {
                                    currentNoteIndex -= measure.Elements.Count;
                                }
                            }
                            
                            if (element != null)
                            {
                                allElements.Add((element, voice.VoiceNumber));
                            }
                        }
                        
                        // 同时播放当前音符位置的所有声部元素
                        if (allElements.Count > 0 && !_isPaused)
                        {
                            PlayElementsSimultaneously(allElements, bpm);
                        }
                    }
                }
                
                // 如果在声部播放时暂停，则不再播放段落
                if (!_isPaused)
                {
                    // 播放段落
                    for (int s = 0; s < score.Sections.Count && _isPlaying && !_isPaused; s++)
                    {
                        var section = score.Sections[s];
                        
                        if (section is SectionNode sectionNode)
                        {
                            for (int m = 0; m < sectionNode.Measures.Count && _isPlaying && !_isPaused; m++)
                            {
                                // 保存暂停状态
                                if (_isPaused)
                                {
                                    _pausedScore = score;
                                    _pausedBpm = bpm;
                                    _pausedVoiceIndex = 0;
                                    _pausedNoteIndex = 0;
                                    _pausedSectionIndex = s;
                                    _pausedMeasureIndex = m;
                                    _pausedElementIndex = 0;
                                    
                                    break;
                                }
                                
                                PlayMeasure(sectionNode.Measures[m], bpm);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 播放被取消
                DebugOutputManager.DebugWriteLine("播放已取消。");
            }
            finally
            {
                if (!_isPaused)
                {
                    _isPlaying = false;
                    _cancellationTokenSource?.Dispose();
                }
            }
        }
        
        #endregion

        #region 私有播放方法
        
        /// <summary>
        /// 播放带颤音效果的音符
        /// </summary>
        /// <param name="frequency">频率</param>
        /// <param name="duration">持续时间（毫秒）</param>
        /// <param name="volume">音量（0.0-1.0）</param>
        /// <param name="hasVibrato">是否有颤音效果</param>
        private void PlayNoteWithVibrato(double frequency, int duration, double volume, bool hasVibrato)
        {
            if (_disposed) return;

            // 创建带淡入淡出效果和颤音效果的音符
            var noteWithEffects = new FadeInOutNoteProvider(frequency, duration, volume, hasVibrato);
            
            // 使用AudioPlayer来播放音符，确保资源正确管理
            _audioPlayer.PlayNote(frequency, duration, volume, hasVibrato);
        }
        
        /// <summary>
        /// 播放滑音效果
        /// </summary>
        /// <param name="startFrequency">起始频率</param>
        /// <param name="endFrequency">结束频率</param>
        /// <param name="duration">持续时间（毫秒）</param>
        /// <param name="volume">音量（0.0-1.0）</param>
        private void PlayGlissandoEffect(double startFrequency, double endFrequency, int duration, double volume)
        {
            if (_disposed) return;

            // 使用AudioPlayer来播放滑音效果，确保资源正确管理
            _audioPlayer.PlayGlissando(startFrequency, endFrequency, duration, volume);
        }
        
        /// <summary>
        /// 播放倚音和主音符
        /// </summary>
        /// <param name="graceNote">倚音</param>
        /// <param name="mainNoteName">主音符名称</param>
        /// <param name="mainOctave">主音符八度</param>
        /// <param name="duration">主音符持续时间（毫秒）</param>
        /// <param name="volume">音量（0.0-1.0）</param>
        /// <param name="hasVibrato">主音符是否有颤音效果</param>
        private void PlayGraceNoteAndMainNote(NoteNode graceNote, string mainNoteName, int mainOctave, int duration, double volume, bool hasVibrato)
        {
            // 计算倚音的持续时间（主音符的1/8）
            int graceDuration = duration / 8;
            if (graceDuration < 20) graceDuration = 20; // 最小20ms
            
            // 播放倚音
            double graceFrequency = AudioPlayer.GetNoteFrequency(graceNote.NoteName, graceNote.Octave);
            PlayNoteWithVibrato(graceFrequency, graceDuration, volume * 0.7, false); // 倚音音量稍低，无颤音
            
            // 播放主音符（持续时间减去倚音时间）
            int mainDuration = duration - graceDuration;
            if (mainDuration > 0)
            {
                double mainFrequency = AudioPlayer.GetNoteFrequency(mainNoteName, mainOctave);
                PlayNoteWithVibrato(mainFrequency, mainDuration, volume, hasVibrato);
            }
        }

        #endregion



        #region 私有辅助类

        

        /// <summary>
        /// 带倚音效果的音符提供者
        /// </summary>
        private class GraceNoteProvider : ISampleProvider
        {
            private readonly int _fadeSamples;
            private readonly int _totalSamples;
            private readonly NoteNode _note;
            private readonly double _volume;
            private readonly int _totalDuration;
            private readonly int _voiceIndex;
            private ISampleProvider? _graceNoteSignal;
            private ISampleProvider? _mainNoteSignal;
            private int _graceNoteSamples = 0;
            private int _mainNoteSamples = 0;
            private int _currentSample = 0;
            private int _currentGraceSample = 0;
            private int _currentMainSample = 0;
            
            public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
            
            public GraceNoteProvider(NoteNode note, double volume, int totalDuration, int voiceIndex)
            {
                _note = note;
                _volume = volume;
                _totalDuration = totalDuration;
                _voiceIndex = voiceIndex;
                
                // 计算倚音和主音符的持续时间
                _graceNoteSamples = (int)(totalDuration * 44.1 / 8); // 倚音占总时长的1/8
                if (_graceNoteSamples < 882) _graceNoteSamples = 882; // 最少20ms (20 * 44.1)
                _mainNoteSamples = (int)(totalDuration * 44.1) - _graceNoteSamples;
                
                // 创建倚音信号
                if (note.GraceNote != null)
                {
                    double graceFrequency = AudioPlayer.GetNoteFrequency(note.GraceNote.NoteName, note.GraceNote.Octave);
                    var graceSignal = new SignalGenerator(44100, 1)
                    {
                        Gain = volume * 0.7, // 倚音音量稍低
                        Frequency = graceFrequency,
                        Type = SignalGeneratorType.Sin
                    };
                    
                    _graceNoteSignal = new OffsetSampleProvider(graceSignal)
                    {
                        TakeSamples = _graceNoteSamples
                    };
                }
                
                // 创建主音符信号
                double mainFrequency = AudioPlayer.GetNoteFrequency(note.NoteName, note.Octave);
                var mainSignal = new SignalGenerator(44100, 1)
                {
                    Gain = volume,
                    Frequency = mainFrequency,
                    Type = SignalGeneratorType.Sin
                };
                
                _mainNoteSignal = new OffsetSampleProvider(mainSignal)
                {
                    TakeSamples = _mainNoteSamples
                };
                
                _totalSamples = _graceNoteSamples + _mainNoteSamples;
                _fadeSamples = (int)(10 * 44.1); // 10ms的淡入淡出
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int samplesRead = 0;
                
                // 如果还有倚音需要播放
                if (_currentGraceSample < _graceNoteSamples && _graceNoteSignal != null)
                {
                    int graceSamplesToRead = Math.Min(count, _graceNoteSamples - _currentGraceSample);
                    int graceRead = _graceNoteSignal.Read(buffer, offset, graceSamplesToRead);
                    
                    // 应用淡入淡出效果到倚音
                    for (int i = 0; i < graceRead; i++)
                    {
                        if (_currentGraceSample < _fadeSamples)
                        {
                            // 淡入 - 使用平滑的余弦函数
                            float fadeInT = (float)_currentGraceSample / _fadeSamples;
                            float fadeFactor = (float)(1.0 - Math.Cos(Math.PI * fadeInT)) / 2.0f;
                            buffer[offset + i] *= fadeFactor;
                        }
                        else if (_currentGraceSample >= _graceNoteSamples - _fadeSamples)
                        {
                            // 淡出 - 使用平滑的余弦函数
                            int samplesRemaining = _graceNoteSamples - _currentGraceSample;
                            float fadeOutT = (float)samplesRemaining / _fadeSamples;
                            float fadeFactor = (float)(1.0 - Math.Cos(Math.PI * fadeOutT)) / 2.0f;
                            // 确保淡出因子不会小于0
                            fadeFactor = Math.Max(0.0f, fadeFactor);
                            buffer[offset + i] *= fadeFactor;
                        }
                        _currentGraceSample++;
                    }
                    
                    samplesRead += graceRead;
                    count -= graceRead;
                    offset += graceRead;
                }
                
                // 如果还有主音符需要播放
                if (count > 0 && _currentMainSample < _mainNoteSamples && _mainNoteSignal != null)
                {
                    int mainSamplesToRead = Math.Min(count, _mainNoteSamples - _currentMainSample);
                    int mainRead = _mainNoteSignal.Read(buffer, offset, mainSamplesToRead);
                    
                    // 应用淡入淡出效果和颤音效果到主音符
                    for (int i = 0; i < mainRead; i++)
                    {
                        // 淡入淡出效果
                        if (_currentMainSample < _fadeSamples)
                        {
                            // 淡入 - 使用平滑的余弦函数
                            float fadeInT = (float)_currentMainSample / _fadeSamples;
                            float fadeFactor = (float)(1.0 - Math.Cos(Math.PI * fadeInT)) / 2.0f;
                            buffer[offset + i] *= fadeFactor;
                        }
                        else if (_currentMainSample >= _mainNoteSamples - _fadeSamples)
                        {
                            // 淡出 - 使用平滑的余弦函数
                            int samplesRemaining = _mainNoteSamples - _currentMainSample;
                            float fadeOutT = (float)samplesRemaining / _fadeSamples;
                            float fadeFactor = (float)(1.0 - Math.Cos(Math.PI * fadeOutT)) / 2.0f;
                            // 确保淡出因子不会小于0
                            fadeFactor = Math.Max(0.0f, fadeFactor);
                            buffer[offset + i] *= fadeFactor;
                        }
                        
                        // 颤音效果
                        if (_note.HasVibrato)
                        {
                            // 计算颤音调制
                            double vibratoRate = 6.0; // 颤音频率 (Hz)
                            double vibratoModulation = Math.Sin(2 * Math.PI * vibratoRate * (_currentMainSample + _graceNoteSamples) / 44100.0);
                            
                            // 重新生成样本值（简化处理，实际应该更复杂）
                            // 这里我们只是简单地调整振幅来模拟颤音效果
                            buffer[offset + i] *= (float)(1.0 + 0.1 * vibratoModulation);
                        }
                        
                        _currentMainSample++;
                    }
                    
                    samplesRead += mainRead;
                }
                
                _currentSample += samplesRead;
                return samplesRead;
            }
        }
        
        /// <summary>
        /// 带淡入淡出效果和颤音效果的音符提供者
        /// </summary>
        private class FadeInOutNoteProvider : ISampleProvider
        {
            private readonly int _fadeSamples;
            private readonly int _totalSamples;
            private readonly double _frequency;
            private readonly bool _hasVibrato;
            private readonly double _volume;
            private readonly double _vibratoRate = 6.0; // 颤音频率 (Hz)
            private readonly double _vibratoDepth = 0.75; // 颤音深度 (半音)
            private int _currentSample = 0;
            private readonly SignalGenerator _signalGenerator;
            
            public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
            
            public FadeInOutNoteProvider(double frequency, int durationMs, double volume, bool hasVibrato = false)
            {
                _frequency = frequency;
                _volume = volume;
                _hasVibrato = hasVibrato;
                
                // 为低频音符适度增强音量以提高可听性，但避免过度增强导致炸麦
                double adjustedVolume = volume;
                if (frequency < 200.0) // 增强200Hz以下的音符
                {
                    // 降低音量增强幅度，避免炸麦
                    adjustedVolume = Math.Min(volume * 1.2, 1.0); // 最多增加20%的音量，但不超过1.0
                }
                
                // 保持使用正弦波，避免方波造成的"滋"声
                SignalGeneratorType waveType = SignalGeneratorType.Sin; // 始终使用正弦波
                
                // 创建波形生成器
                _signalGenerator = new SignalGenerator(44100, 1)
                {
                    Gain = adjustedVolume,
                    Frequency = frequency,
                    Type = waveType
                };
                
                _totalSamples = (int)(durationMs * 44.1);
                // 增加淡入淡出时间以实现更柔和的音符过渡
                if (frequency < 100.0)
                {
                    _fadeSamples = (int)(80 * 44.1); // 80ms的淡入淡出，用于极低频音符
                }
                else if (frequency < 300.0)
                {
                    _fadeSamples = (int)(60 * 44.1); // 60ms的淡入淡出，用于低频音符
                }
                else
                {
                    _fadeSamples = (int)(40 * 44.1); // 40ms的淡入淡出，用于中高频音符
                }
            }
            
            public int Read(float[] buffer, int offset, int count)
            {
                int samplesToProcess = Math.Min(count, _totalSamples - _currentSample);
                
                // 生成音频样本并应用效果
                for (int i = 0; i < samplesToProcess; i++)
                {
                    // 计算当前样本位置
                    int sampleIndex = _currentSample + i;
                    
                    // 基础频率
                    double currentFrequency = _frequency;
                    
                    // 颤音效果
                    if (_hasVibrato)
                    {
                        // 计算颤音调制
                        double vibratoModulation = Math.Sin(2 * Math.PI * _vibratoRate * sampleIndex / 44100.0);
                        // 使用频率调制而不是振幅调制来实现更真实的颤音效果
                        currentFrequency = _frequency * Math.Pow(2, _vibratoDepth * vibratoModulation / 12.0);
                    }
                    
                    // 更新信号生成器频率
                    _signalGenerator.Frequency = currentFrequency;
                    
                    // 生成样本
                    float[] tempBuffer = new float[1];
                    _signalGenerator.Read(tempBuffer, 0, 1);
                    buffer[offset + i] = tempBuffer[0];
                    
                    // 应用淡入淡出效果
                    if (sampleIndex < _fadeSamples)
                    {
                        // 淡入 - 使用平滑的余弦函数
                        float fadeInT = (float)sampleIndex / _fadeSamples;
                        float fadeFactor = (float)(1.0 - Math.Cos(Math.PI * fadeInT)) / 2.0f;
                        buffer[offset + i] *= fadeFactor;
                    }
                    else if (sampleIndex >= _totalSamples - _fadeSamples)
                    {
                        // 淡出 - 使用平滑的余弦函数
                        int samplesRemaining = _totalSamples - sampleIndex;
                        float fadeOutT = (float)samplesRemaining / _fadeSamples;
                        float fadeFactor = (float)(1.0 - Math.Cos(Math.PI * fadeOutT)) / 2.0f;
                        // 确保淡出因子不会小于0
                        fadeFactor = Math.Max(0.0f, fadeFactor);
                        buffer[offset + i] *= fadeFactor;
                    }
                }
                
                _currentSample += samplesToProcess;
                return samplesToProcess;
            }
        }
        
        /// <summary>
        /// 滑音效果提供者
        /// </summary>
        private class GlissandoNoteProvider : ISampleProvider
        {
            private readonly double _startFrequency;
            private readonly double _endFrequency;
            private readonly int _totalSamples;
            private readonly double _volume;
            private readonly int _fadeSamples;
            private int _currentSample = 0;
            private readonly SignalGenerator _signalGenerator;
            
            public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
            
            public GlissandoNoteProvider(double startFrequency, double endFrequency, int durationMs, double volume)
            {
                _startFrequency = startFrequency;
                _endFrequency = endFrequency;
                _volume = volume;
                
                // 创建正弦波生成器
                _signalGenerator = new SignalGenerator(44100, 1)
                {
                    Gain = volume,
                    Frequency = startFrequency,
                    Type = SignalGeneratorType.Sin
                };
                
                _totalSamples = (int)(durationMs * 44.1);
                // 对于极低频音符，增加淡入淡出时间以减少pop声
                // 检查起始频率是否低于50Hz
                if (_startFrequency < 50.0)
                {
                    _fadeSamples = (int)(60 * 44.1); // 60ms的淡入淡出
                }
                else
                {
                    _fadeSamples = (int)(30 * 44.1); // 30ms的淡入淡出
                }
            }
            
            public int Read(float[] buffer, int offset, int count)
            {
                // 如果已经处理完所有样本，返回0
                if (_currentSample >= _totalSamples)
                {
                    return 0;
                }
                
                int samplesToProcess = Math.Min(count, _totalSamples - _currentSample);
                
                // 生成滑音样本
                for (int i = 0; i < samplesToProcess; i++)
                {
                    // 计算当前样本位置
                    int sampleIndex = _currentSample + i;
                    
                    // 计算当前频率（线性插值）
                    double t = (double)sampleIndex / _totalSamples;
                    double currentFrequency = _startFrequency + t * (_endFrequency - _startFrequency);
                    
                    // 更新信号生成器频率
                    _signalGenerator.Frequency = currentFrequency;
                    
                    // 生成样本
                    float[] tempBuffer = new float[1];
                    _signalGenerator.Read(tempBuffer, 0, 1);
                    buffer[offset + i] = tempBuffer[0];
                    
                    // 应用淡入淡出效果
                    if (sampleIndex < _fadeSamples)
                    {
                        // 淡入 - 使用平滑的余弦函数
                        float fadeInT = (float)sampleIndex / _fadeSamples;
                        float fadeFactor = (float)(1.0 - Math.Cos(Math.PI * fadeInT)) / 2.0f;
                        buffer[offset + i] *= fadeFactor;
                    }
                    else if (sampleIndex >= _totalSamples - _fadeSamples)
                    {
                        // 淡出 - 使用平滑的余弦函数
                        int samplesRemaining = _totalSamples - sampleIndex;
                        float fadeOutT = (float)samplesRemaining / _fadeSamples;
                        float fadeFactor = (float)(1.0 - Math.Cos(Math.PI * fadeOutT)) / 2.0f;
                        // 确保淡出因子不会小于0
                        fadeFactor = Math.Max(0.0f, fadeFactor);
                        buffer[offset + i] *= fadeFactor;
                    }
                }
                
                _currentSample += samplesToProcess;
                return samplesToProcess;
            }
        }
        
        #endregion

        #region 私有播放方法
        
        /// <summary>
        /// 同时播放多个声部的元素
        /// </summary>
        /// <param name="elements">元素列表</param>
        /// <param name="bpm">每分钟节拍数</param>
        private void PlayElementsSimultaneously(List<(AstNode element, int voiceIndex)> elements, int bpm)
        {
            // 输出播放信息
            DebugOutputManager.DebugWriteLine($"播放音符位置，BPM: {bpm}，声部数量: {elements.Count}");

            // 计算元素的总时长
            int elementDuration = 0;
            
            foreach (var (element, voiceIndex) in elements)
            {
                int duration = 0;
                
                if (element is NoteNode note)
                {
                    duration = CalculateDuration(note.Duration, note.IsDotted, bpm);
                    string vibratoText = note.HasVibrato ? " (颤音)" : "";
                    string graceNoteText = note.GraceNote != null ? $" (倚音: {note.GraceNote.NoteName}{note.GraceNote.Octave})" : "";
                    DebugOutputManager.DebugWriteLine($"  声部 {voiceIndex}: 音符 {note.NoteName}{note.Octave}({note.Duration}){vibratoText}{graceNoteText}");
                }
                else if (element is ChordNode chord)
                {
                    duration = CalculateDuration(chord.Duration, chord.IsDotted, bpm);
                    // 和弦信息输出需要特殊处理，因为涉及多个Write操作
                    StringBuilder chordInfo = new StringBuilder();
                    chordInfo.Append($"  声部 {voiceIndex}: 和弦 {{");
                    
                    for (int i = 0; i < chord.Notes.Count; i++)
                    {
                        if (i > 0) chordInfo.Append(",");
                        chordInfo.Append($"{chord.Notes[i].NoteName}{chord.Notes[i].Octave}");
                    }
                    
                    chordInfo.Append($"}}({chord.Duration})");
                    DebugOutputManager.DebugWriteLine(chordInfo.ToString());
                }
                else if (element is RestNode rest)
                {
                    duration = CalculateDuration(rest.Duration, rest.IsDotted, bpm);
                    DebugOutputManager.DebugWriteLine($"  声部 {voiceIndex}: 休止符 R({rest.Duration})");
                }

                if (duration > elementDuration)
                    elementDuration = duration;
            }
            
            // 应用速度因子调整持续时间
            int adjustedDuration = (int)(elementDuration * _currentSpeedFactor);
            
            // 为每个元素创建混合器输入
            var mixer = new NAudio.Wave.SampleProviders.MixingSampleProvider(
                NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
            
            // 允许不同格式的输入（解决波形格式不匹配问题）
            mixer.ReadFully = true;
            
            // 为每个元素创建音频信号
            foreach (var (element, voiceIndex) in elements) 
            {
                AddElementToMixer(mixer, element, bpm, voiceIndex);
            }

            // 播放混合后的音频
            if (mixer.MixerInputs.Count() > 0)
            {
                DebugOutputManager.DebugWriteLine($"调用 _audioPlayer.PlayMixer，持续时间: {adjustedDuration}ms");
                _audioPlayer.PlayMixer(mixer, adjustedDuration);
            }
        }
        
        /// <summary>
        /// 将元素添加到混合器
        /// </summary>
        /// <param name="mixer">混合器</param>
        /// <param name="element">元素</param>
        /// <param name="bpm">每分钟节拍数</param>
        /// <param name="voiceIndex">声部索引</param>
        private void AddElementToMixer(NAudio.Wave.SampleProviders.MixingSampleProvider mixer, AstNode element, int bpm, int voiceIndex)
        {
            if (element is NoteNode note)
            {
                // 计算音符持续时间（毫秒）
                int duration = CalculateDuration(note.Duration, note.IsDotted, bpm);
                double volume = GetVolumeFromDynamics(note.Dynamics);
                
                // 应用全局音量设置
                double finalVolume = volume * _currentVolume;
                
                // 为不同的声部调整音量或音高
                double adjustedVolume = finalVolume * (1.0 - voiceIndex * 0.2); // 每个声部音量递减20%
                
                // 处理倚音
                if (note.GraceNote != null)
                {
                    // 对于有倚音的音符，我们需要创建一个特殊的提供者来处理倚音和主音符
                    var graceNoteProvider = new GraceNoteProvider(note, adjustedVolume, duration, voiceIndex);
                    mixer.AddMixerInput(graceNoteProvider);
                }
                // 处理滑音
                else if (note.GlissandoStart != null)
                {
                    // 对于有滑音的音符，我们需要创建一个特殊的提供者来处理滑音效果
                    double startFrequency = AudioPlayer.GetNoteFrequency(note.GlissandoStart.NoteName, note.GlissandoStart.Octave);
                    double endFrequency = AudioPlayer.GetNoteFrequency(note.NoteName, note.Octave);
                    var glissandoProvider = new GlissandoNoteProvider(startFrequency, endFrequency, duration, adjustedVolume);
                    mixer.AddMixerInput(glissandoProvider);
                }
                else
                {
                    // 获取音符频率
                    double frequency = AudioPlayer.GetNoteFrequency(note.NoteName, note.Octave);
                    
                    // 为不同的声部调整音高
                    double adjustedFrequency = frequency * Math.Pow(0.95, voiceIndex); // 每个声部音高降低5%
                    
                    // 应用淡入淡出效果和颤音效果
                    var faded = new FadeInOutNoteProvider(adjustedFrequency, duration, adjustedVolume, note.HasVibrato);
                    
                    mixer.AddMixerInput(faded);
                }
            }
            else if (element is ChordNode chord)
            {
                // 计算和弦持续时间（毫秒）
                int duration = CalculateDuration(chord.Duration, chord.IsDotted, bpm);
                double volume = GetVolumeFromDynamics(chord.Dynamics);
                
                // 应用全局音量设置
                double finalVolume = volume * _currentVolume;
                
                // 为不同的声部调整音量
                double adjustedVolume = finalVolume * (1.0 - voiceIndex * 0.2); // 每个声部音量递减20%
                
                // 创建和弦混合器 - 使用与主混合器相同的格式
                var chordMixer = new NAudio.Wave.SampleProviders.MixingSampleProvider(
                    NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
                
                // 允许不同格式的输入
                chordMixer.ReadFully = true;
                
                // 添加和弦中的所有音符到混合器
                foreach (var chordNote in chord.Notes)
                {
                    // 获取音符频率
                    double frequency = AudioPlayer.GetNoteFrequency(chordNote.NoteName, chordNote.Octave);
                    
                    // 为不同的声部调整音高
                    double adjustedFrequency = frequency * Math.Pow(0.95, voiceIndex); // 每个声部音高降低5%
                    
                    // 应用淡入淡出效果以避免pop声
                    var faded = new FadeInOutNoteProvider(adjustedFrequency, duration, adjustedVolume / chord.Notes.Count, false);
                    
                    chordMixer.AddMixerInput(faded);
                }
                
                mixer.AddMixerInput(chordMixer);
            }
            // 休止符不需要添加到混合器中
        }
        
        /// <summary>
        /// 播放小节（从指定元素开始）
        /// </summary>
        /// <param name="measure">小节</param>
        /// <param name="bpm">每分钟节拍数</param>
        /// <param name="startIndex">起始元素索引</param>
        private void PlayMeasureFromElement(MeasureNode measure, int bpm, int startIndex)
        {
            for (int i = startIndex; i < measure.Elements.Count; i++)
            {
                if (!_isPlaying || _isPaused) break;
                
                var element = measure.Elements[i];
                
                if (element is NoteNode note)
                {
                    int duration = CalculateDuration(note.Duration, note.IsDotted, bpm);
                    double volume = GetVolumeFromDynamics(note.Dynamics);
                    
                    // 处理倚音
                    if (note.GraceNote != null)
                    {
                        PlayGraceNoteAndMainNote(note.GraceNote, note.NoteName, note.Octave, duration, volume, note.HasVibrato);
                    }
                    // 处理滑音
                    else if (note.GlissandoStart != null)
                    {
                        PlayGlissando(note.GlissandoStart.NoteName, note.GlissandoStart.Octave, 
                            note.NoteName, note.Octave, duration, volume);
                    }
                    else
                    {
                        PlayNote(note.NoteName, note.Octave, duration, volume, note.HasVibrato);
                    }
                }
                else if (element is ChordNode chord)
                {
                    int duration = CalculateDuration(chord.Duration, chord.IsDotted, bpm);
                    double volume = GetVolumeFromDynamics(chord.Dynamics);
                    PlayChord(chord, duration, volume);
                }
                else if (element is RestNode rest)
                {
                    int duration = CalculateDuration(rest.Duration, rest.IsDotted, bpm);
                    PlayRest(duration);
                }
                
                // 减少延迟以实现更平滑的音符过渡
                if (_isPlaying && !_isPaused && !(element is RestNode))
                {
                    // 应用速度因子调整延迟
                    int adjustedDelay = (int)(2 * _currentSpeedFactor);
                    Thread.Sleep(Math.Max(1, adjustedDelay)); // 很短的延迟以确保播放顺序但保持平滑过渡
                }
            }
        }
        
        /// <summary>
        /// 播放小节
        /// </summary>
        /// <param name="measure">小节</param>
        /// <param name="bpm">每分钟节拍数</param>
        private void PlayMeasure(MeasureNode measure, int bpm)
        {
            foreach (var element in measure.Elements)
            {
                if (!_isPlaying || _isPaused) break;
                
                if (element is NoteNode note)
                {
                    int duration = CalculateDuration(note.Duration, note.IsDotted, bpm);
                    double volume = GetVolumeFromDynamics(note.Dynamics);
                    
                    // 处理倚音
                    if (note.GraceNote != null)
                    {
                        PlayGraceNoteAndMainNote(note.GraceNote, note.NoteName, note.Octave, duration, volume, note.HasVibrato);
                    }
                    // 处理滑音
                    else if (note.GlissandoStart != null)
                    {
                        PlayGlissando(note.GlissandoStart.NoteName, note.GlissandoStart.Octave, 
                            note.NoteName, note.Octave, duration, volume);
                    }
                    else
                    {
                        PlayNote(note.NoteName, note.Octave, duration, volume, note.HasVibrato);
                    }
                }
                else if (element is ChordNode chord)
                {
                    int duration = CalculateDuration(chord.Duration, chord.IsDotted, bpm);
                    double volume = GetVolumeFromDynamics(chord.Dynamics);
                    PlayChord(chord, duration, volume);
                }
                else if (element is RestNode rest)
                {
                    int duration = CalculateDuration(rest.Duration, rest.IsDotted, bpm);
                    PlayRest(duration);
                }
                
                // 减少延迟以实现更平滑的音符过渡
                if (_isPlaying && !_isPaused && !(element is RestNode))
                {
                    // 应用速度因子调整延迟
                    int adjustedDelay = (int)(2 * _currentSpeedFactor);
                    Thread.Sleep(Math.Max(1, adjustedDelay)); // 很短的延迟以确保播放顺序但保持平滑过渡
                }
            }
        }
        
        #endregion

        #region 音频处理方法
        
        /// <summary>
        /// 估算小节的时长（毫秒）
        /// </summary>
        /// <param name="measure">小节节点</param>
        /// <param name="bpm">每分钟节拍数</param>
        /// <returns>小节时长（毫秒）</returns>
        private int EstimateMeasureDuration(MeasureNode measure, int bpm)
        {
            int totalDuration = 0;
            
            foreach (var element in measure.Elements)
            {
                if (element is NoteNode note)
                {
                    // 计算音符持续时间（毫秒）
                    int duration = CalculateDuration(note.Duration, note.IsDotted, bpm);
                    totalDuration += duration;
                }
                else if (element is ChordNode chord)
                {
                    // 计算和弦持续时间（毫秒）
                    int duration = CalculateDuration(chord.Duration, chord.IsDotted, bpm);
                    totalDuration += duration;
                }
                else if (element is RestNode rest)
                {
                    // 计算休止符持续时间（毫秒）
                    int duration = CalculateDuration(rest.Duration, rest.IsDotted, bpm);
                    totalDuration += duration;
                }
            }
            
            return totalDuration;
        }

        /// <summary>
        /// 计算音符或休止符的持续时间
        /// </summary>
        /// <param name="duration">时值（如"1/4"）</param>
        /// <param name="isDotted">是否附点</param>
        /// <param name="bpm">每分钟节拍数</param>
        /// <returns>持续时间（毫秒）</returns>
        private int CalculateDuration(string duration, bool isDotted, int bpm)
        {
            // 以四分音符为一拍计算
            double beats = 0;
            
            switch (duration)
            {
                case "1":
                    beats = 4.0;
                    break;
                case "1/2":
                    beats = 2.0;
                    break;
                case "1/4":
                    beats = 1.0;
                    break;
                case "1/8":
                    beats = 0.5;
                    break;
                case "1/16":
                    beats = 0.25;
                    break;
                default:
                    beats = 1.0; // 默认为四分音符
                    break;
            }
            
            // 如果是附点音符，增加一半时值
            if (isDotted)
            {
                beats *= 1.5;
            }
            
            // 计算毫秒数
            // 1分钟 = 60,000毫秒
            // 1拍 = 60,000 / BPM 毫秒
            int baseDuration = (int)(beats * (60000.0 / bpm));
            
            // 应用当前速度因子
            return (int)(baseDuration * _currentSpeedFactor);
        }

        /// <summary>
        /// 根据强弱记号获取音量
        /// </summary>
        /// <param name="dynamics">强弱记号</param>
        /// <returns>音量（0.0-1.0）</returns>
        private double GetVolumeFromDynamics(string dynamics)
        {
            switch (dynamics?.ToLower())
            {
                case "p":
                    return 0.3;
                case "pp":
                    return 0.1;
                case "mp":
                    return 0.45;
                case "mf":
                    return 0.6;
                case "f":
                    return 0.8;
                case "ff":
                    return 0.95;
                case "fff":
                    return 1.0;
                default:
                    return 0.7; // 默认音量
            }
        }
        
        #endregion

        #region 播放控制方法
        
        // 播放状态相关字段
        private bool _isPaused = false;
        private ScoreNode? _pausedScore = null;
        private int _pausedBpm = 120;
        private int _pausedVoiceIndex = 0;
        private int _pausedNoteIndex = 0;
        private int _pausedSectionIndex = 0;
        private int _pausedMeasureIndex = 0;
        private int _pausedElementIndex = 0;
        private double _currentVolume = 1.0;
        private double _currentSpeedFactor = 1.0;
        
        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            if (_isPlaying && !_isPaused)
            {
                _isPaused = true;
                _isPlaying = false;
                _cancellationTokenSource?.Cancel();
                DebugOutputManager.DebugWriteLine("播放已暂停。");
            }
        }

        /// <summary>
        /// 继续播放
        /// </summary>
        public void Resume()
        {
            if (_isPaused && _pausedScore != null)
            {
                DebugOutputManager.DebugWriteLine("继续播放...");
                _isPaused = false;
                _isPlaying = true;
                // 重新初始化音频播放器以确保设备可用
                _audioPlayer.Dispose(); // 先释放旧的资源
                _audioPlayer = new AudioPlayer();
                PlayScoreFromPausedState(_pausedScore, _pausedBpm, _pausedVoiceIndex, 
                    _pausedNoteIndex, _pausedSectionIndex, _pausedMeasureIndex, _pausedElementIndex);
            }
            else
            {
                DebugOutputManager.DebugWriteLine("没有暂停的播放任务。");
            }
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
            _isPaused = false;
            _cancellationTokenSource?.Cancel();
            
            // 清除暂停状态
            _pausedScore = null;
            _pausedBpm = 120;
            _pausedVoiceIndex = 0;
            _pausedNoteIndex = 0;
            _pausedSectionIndex = 0;
            _pausedMeasureIndex = 0;
            _pausedElementIndex = 0;
        }

        /// <summary>
        /// 从暂停状态继续播放音乐作品
        /// </summary>
        /// <param name="score">音乐作品抽象语法树</param>
        /// <param name="bpm">当前BPM</param>
        /// <param name="voiceIndex">声部索引</param>
        /// <param name="noteIndex">音符索引</param>
        /// <param name="sectionIndex">段落索引</param>
        /// <param name="measureIndex">小节索引</param>
        /// <param name="elementIndex">元素索引</param>
        private void PlayScoreFromPausedState(ScoreNode score, int bpm, int voiceIndex, 
            int noteIndex, int sectionIndex, int measureIndex, int elementIndex)
        {
            DebugOutputManager.DebugWriteLine($"从暂停状态恢复播放: BPM={bpm}, VoiceIndex={voiceIndex}, NoteIndex={noteIndex}");
            _isPlaying = true;
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                // 从保存的位置继续播放声部
                if (score.Voices.Count > 0)
                {
                    // 计算最大音符数（跨所有小节和声部）
                    int maxNotes = 0;
                    foreach (var voice in score.Voices)
                    {
                        int voiceNoteCount = 0;
                        foreach (var measure in voice.Measures)
                        {
                            voiceNoteCount += measure.Elements.Count;
                        }
                        if (voiceNoteCount > maxNotes)
                            maxNotes = voiceNoteCount;
                    }
                    
                    DebugOutputManager.DebugWriteLine($"声部数量: {score.Voices.Count}, 最大音符数: {maxNotes}");
                    
                    // 从保存的音符位置继续播放所有声部
                    for (int currentNoteIndex = noteIndex; currentNoteIndex < maxNotes && _isPlaying && !_isPaused; currentNoteIndex++)
                    {
                        DebugOutputManager.DebugWriteLine($"播放音符索引: {currentNoteIndex}");
                        
                        // 收集当前音符位置的所有声部元素
                        var allElements = new List<(AstNode element, int voiceIndex)>();
                        
                        // 对于第一个音符位置，从保存的声部索引开始；对于后续位置，从第一个声部开始
                        int voiceStartIndex = (currentNoteIndex == noteIndex) ? voiceIndex : 0;
                        
                        for (int v = voiceStartIndex; v < score.Voices.Count; v++)
                        {
                            var voice = score.Voices[v];
                            
                            if (!_isPlaying || _isPaused) break;
                            
                            // 计算当前声部在当前音符位置的元素
                            int elementPos = currentNoteIndex;
                            AstNode? element = null;
                            
                            foreach (var measure in voice.Measures)
                            {
                                if (elementPos < measure.Elements.Count)
                                {
                                    element = measure.Elements[elementPos];
                                    break;
                                }
                                else
                                {
                                    elementPos -= measure.Elements.Count;
                                }
                            }
                            
                            if (element != null)
                            {
                                allElements.Add((element, voice.VoiceNumber));
                            }
                        }
                        
                        // 同时播放当前音符位置的所有声部元素
                        if (allElements.Count > 0 && !_isPaused)
                        {
                            DebugOutputManager.DebugWriteLine($"播放 {allElements.Count} 个声部元素");
                            PlayElementsSimultaneously(allElements, (int)(bpm * _currentSpeedFactor));
                        }
                        
                        // 如果已暂停，退出循环
                        if (_isPaused) break;
                    }
                }
                
                // 如果在声部播放时暂停，则不再播放段落
                if (!_isPaused)
                {
                    DebugOutputManager.DebugWriteLine("继续播放段落");
                    // 播放段落 - 从暂停位置继续
                    for (int s = sectionIndex; s < score.Sections.Count; s++)
                    {
                        if (!_isPlaying || _isPaused) break;
                        
                        if (score.Sections[s] is SectionNode sectionNode)
                        {
                            // 对于第一个段落，从保存的小节索引开始；对于后续段落，从第一个小节开始
                            int measureStartIndex = (s == sectionIndex) ? measureIndex : 0;
                            
                            for (int m = measureStartIndex; m < sectionNode.Measures.Count; m++)
                            {
                                if (!_isPlaying || _isPaused) break;
                                
                                // 从暂停的元素位置开始（如果在当前小节）
                                if (s == sectionIndex && m == measureIndex)
                                {
                                    DebugOutputManager.DebugWriteLine($"从段落 {s}, 小节 {m} 继续播放");
                                    PlayMeasureFromElement(sectionNode.Measures[m], (int)(bpm * _currentSpeedFactor), elementIndex);
                                }
                                else
                                {
                                    DebugOutputManager.DebugWriteLine($"播放段落 {s}, 小节 {m}");
                                    PlayMeasure(sectionNode.Measures[m], (int)(bpm * _currentSpeedFactor));
                                }
                                
                                // 如果已暂停，退出循环
                                if (_isPaused) break;
                            }
                        }
                        
                        // 如果已暂停，退出循环
                        if (_isPaused) break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 播放被取消
                DebugOutputManager.DebugWriteLine("播放已取消。");
            }
            finally
            {
                if (!_isPaused)
                {
                    _isPlaying = false;
                    _cancellationTokenSource?.Dispose();
                }
            }
        }

        /// <summary>
        /// 设置播放速度
        /// </summary>
        /// <param name="bpm">每分钟节拍数</param>
        public void SetSpeed(int bpm)
        {
            if (bpm > 0)
            {
                _currentSpeedFactor = (double)bpm / 120.0; // 基于默认BPM 120的倍数
                DebugOutputManager.DebugWriteLine($"播放速度已设置为 {bpm} BPM (倍数: {_currentSpeedFactor:F2})。");
            }
            else
            {
                DebugOutputManager.DebugWriteLine("错误: BPM 必须大于0。");
            }
        }

        /// <summary>
        /// 设置播放速度倍数
        /// </summary>
        /// <param name="factor">速度倍数 (0.5 = 一半速度, 2.0 = 两倍速度)</param>
        public void SetSpeedFactor(double factor)
        {
            if (factor > 0)
            {
                _currentSpeedFactor = factor;
                int effectiveBpm = (int)(120 * factor);
                DebugOutputManager.DebugWriteLine($"播放速度已设置为 {effectiveBpm} BPM (倍数: {factor:F2})。");
            }
            else
            {
                DebugOutputManager.DebugWriteLine("错误: 速度倍数必须大于0。");
            }
        }

        /// <summary>
        /// 设置音量
        /// </summary>
        /// <param name="volume">音量（0.0-1.0）</param>
        public void SetVolume(double volume)
        {
            if (volume >= 0.0 && volume <= 1.0)
            {
                _currentVolume = volume;
                DebugOutputManager.DebugWriteLine($"音量已设置为 {volume:F2}。");
            }
            else
            {
                DebugOutputManager.DebugWriteLine("错误: 音量必须在0.0到1.0之间。");
            }
        }
        #endregion

        #region 资源管理方法
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _isPlaying = false;
            _isPaused = false;
            _cancellationTokenSource?.Cancel();
            _audioPlayer?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
        
        #endregion
    }
}