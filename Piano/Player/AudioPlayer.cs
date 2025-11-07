using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Piano.Player
{
    /// <summary>
    /// 音频播放器类
    /// </summary>
    public class AudioPlayer : IDisposable
    {
        private WaveOutEvent? _waveOut;
        private bool _disposed = false;

        // 音符频率表（基于A4=440Hz的标准调音）
        private static readonly Dictionary<string, double[]> NoteFrequencies = new Dictionary<string, double[]>
        {
            {"C", new double[] { 16.35, 32.70, 65.41, 130.81, 261.63, 523.25, 1046.50, 2093.00, 4186.01 }},
            {"C#", new double[] { 17.32, 34.65, 69.30, 138.59, 277.18, 554.37, 1108.73, 2217.46, 4434.92 }},
            {"Db", new double[] { 17.32, 34.65, 69.30, 138.59, 277.18, 554.37, 1108.73, 2217.46, 4434.92 }},
            {"D", new double[] { 18.35, 36.71, 73.42, 146.83, 293.66, 587.33, 1174.66, 2349.32, 4698.64 }},
            {"D#", new double[] { 19.45, 38.89, 77.78, 155.56, 311.13, 622.25, 1244.51, 2489.02, 4978.03 }},
            {"Eb", new double[] { 19.45, 38.89, 77.78, 155.56, 311.13, 622.25, 1244.51, 2489.02, 4978.03 }},
            {"E", new double[] { 20.60, 41.20, 82.41, 164.81, 329.63, 659.26, 1318.51, 2637.02, 5274.04 }},
            {"F", new double[] { 21.83, 43.65, 87.31, 174.61, 349.23, 698.46, 1396.91, 2793.83, 5587.65 }},
            {"F#", new double[] { 23.12, 46.25, 92.50, 185.00, 369.99, 739.99, 1479.98, 2959.96, 5919.91 }},
            {"Gb", new double[] { 23.12, 46.25, 92.50, 185.00, 369.99, 739.99, 1479.98, 2959.96, 5919.91 }},
            {"G", new double[] { 24.50, 49.00, 98.00, 196.00, 392.00, 783.99, 1567.98, 3135.96, 6271.93 }},
            {"G#", new double[] { 25.96, 51.91, 103.83, 207.65, 415.30, 830.61, 1661.22, 3322.44, 6644.88 }},
            {"Ab", new double[] { 25.96, 51.91, 103.83, 207.65, 415.30, 830.61, 1661.22, 3322.44, 6644.88 }},
            {"A", new double[] { 27.50, 55.00, 110.00, 220.00, 440.00, 880.00, 1760.00, 3520.00, 7040.00 }},
            {"A#", new double[] { 29.14, 58.27, 116.54, 233.08, 466.16, 932.33, 1864.66, 3729.31, 7458.62 }},
            {"Bb", new double[] { 29.14, 58.27, 116.54, 233.08, 466.16, 932.33, 1864.66, 3729.31, 7458.62 }},
            {"B", new double[] { 30.87, 61.74, 123.47, 246.94, 493.88, 987.77, 1975.53, 3951.07, 7902.13 }}
        };

        /// <summary>
        /// 获取指定音符和八度的频率
        /// </summary>
        /// <param name="noteName">音符名称</param>
        /// <param name="octave">八度</param>
        /// <returns>频率（Hz）</returns>
        public static double GetNoteFrequency(string noteName, int octave)
        {
            if (NoteFrequencies.ContainsKey(noteName) && octave >= 0 && octave < NoteFrequencies[noteName].Length)
            {
                return NoteFrequencies[noteName][octave];
            }
            throw new ArgumentException($"Invalid note name or octave: {noteName}{octave}");
        }

        /// <summary>
        /// 播放音符
        /// </summary>
        /// <param name="frequency">频率</param>
        /// <param name="duration">持续时间（毫秒）</param>
        /// <param name="volume">音量（0.0-1.0）</param>
        /// <param name="hasVibrato">是否有颤音效果</param>
        public void PlayNote(double frequency, int duration, double volume = 1.0, bool hasVibrato = false)
        {
            if (_disposed) return;

            // 确保音频设备已初始化
            if (_waveOut == null)
            {
                _waveOut = new WaveOutEvent();
            }
            else
            {
                // 在播放新音符前等待之前的播放完成
                while (_waveOut.PlaybackState == PlaybackState.Playing)
                {
                    System.Threading.Thread.Sleep(10);
                }
                _waveOut.Stop();
            }

            // 为低频音符适度增强音量以提高可听性，但避免过度增强导致炸麦
            double adjustedVolume = volume;
            if (frequency < 200.0) // 增强200Hz以下的音符
            {
                // 显著增加音量以提高可听性，但避免过度增强导致失真
                adjustedVolume = Math.Min(volume * 2.0, 1.0); // 最多增加100%的音量，但不超过1.0
            }

            // 创建带淡入淡出效果和颤音效果的音符
            var noteWithEffects = new FadeInOutNoteProvider(frequency, duration, adjustedVolume, hasVibrato);

            _waveOut.Init(noteWithEffects);
            _waveOut.Play();

            // 等待播放完成，但稍微缩短时间以实现音符间的平滑过渡
            int overlapTime = 20; // 增加重叠时间为20ms以实现更柔和的过渡
            System.Threading.Thread.Sleep(Math.Max(duration - overlapTime, 1)); // 确保至少播放1ms
            _waveOut.Stop();
        }
        
        /// <summary>
        /// 播放滑音效果
        /// </summary>
        /// <param name="startFrequency">起始频率</param>
        /// <param name="endFrequency">结束频率</param>
        /// <param name="duration">持续时间（毫秒）</param>
        /// <param name="volume">音量（0.0-1.0）</param>
        public void PlayGlissando(double startFrequency, double endFrequency, int duration, double volume = 1.0)
        {
            if (_disposed) return;

            // 确保音频设备已初始化
            if (_waveOut == null)
            {
                _waveOut = new WaveOutEvent();
            }
            else
            {
                // 在播放新音频前等待之前的播放完成
                while (_waveOut.PlaybackState == PlaybackState.Playing)
                {
                    System.Threading.Thread.Sleep(10);
                }
                _waveOut.Stop();
            }

            // 创建滑音效果提供者
            var glissandoProvider = new GlissandoProvider(startFrequency, endFrequency, duration, volume);
            
            _waveOut.Init(glissandoProvider);
            _waveOut.Play();

            // 等待播放完成
            System.Threading.Thread.Sleep(duration + 20); // 添加一些额外时间确保播放完成
            _waveOut.Stop();
        }
        
        /// <summary>
        /// 播放混合音频信号
        /// </summary>
        /// <param name="mixer">混合器</param>
        /// <param name="duration">持续时间（毫秒）</param>
        public void PlayMixer(ISampleProvider mixer, int duration)
        {
            if (_disposed) return;

            // 确保音频设备已初始化
            if (_waveOut == null)
            {
                _waveOut = new WaveOutEvent();
            }
            else
            {
                // 在播放新音频前等待之前的播放完成
                while (_waveOut.PlaybackState == PlaybackState.Playing)
                {
                    System.Threading.Thread.Sleep(10);
                }
                _waveOut.Stop();
            }

            // 为混合器应用淡入淡出效果
            var faded = new FadeInOutMixerProvider(mixer, duration);
            
            _waveOut.Init(faded);
            _waveOut.Play();

            // 等待播放完成
            System.Threading.Thread.Sleep(duration + 20); // 添加一些额外时间确保播放完成
            _waveOut.Stop();
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
                    // 适度增加音量以提高可听性，但避免过度增强导致失真
                    adjustedVolume = Math.Min(volume * 1.5, 1.0); // 最多增加50%的音量，但不超过1.0
                }
                
                // 始终使用正弦波，避免方波造成的"滋"声，但提高可听性通过音量调整
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
                // 如果已经处理完所有样本，返回0
                if (_currentSample >= _totalSamples)
                {
                    return 0;
                }
                
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
                    
                    // 淡入淡出效果
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
        /// 带淡入淡出效果的混合器提供者
        /// </summary>
        private class FadeInOutMixerProvider : ISampleProvider
        {
            private readonly ISampleProvider _source;
            private readonly int _fadeSamples;
            private readonly int _totalSamples;
            private int _currentSample = 0;
            
            public WaveFormat WaveFormat => _source.WaveFormat;
            
            public FadeInOutMixerProvider(ISampleProvider source, int durationMs)
            {
                _source = source;
                _totalSamples = (int)(durationMs * 44.1);
                // 增加淡入淡出时间以实现更柔和的音频过渡
                _fadeSamples = Math.Min((int)(30 * 44.1), _totalSamples / 5); // 30ms的淡入淡出或总时间的1/5
            }
            
            public int Read(float[] buffer, int offset, int count)
            {
                int samplesRead = _source.Read(buffer, offset, count);
                
                // 应用淡入淡出效果
                for (int i = 0; i < samplesRead; i++)
                {
                    if (_currentSample < _fadeSamples)
                    {
                        // 淡入
                        float fadeFactor = (float)_currentSample / _fadeSamples;
                        buffer[offset + i] *= fadeFactor;
                    }
                    else if (_currentSample >= _totalSamples - _fadeSamples)
                    {
                        // 淡出
                        int samplesRemaining = _totalSamples - _currentSample;
                        float fadeFactor = (float)samplesRemaining / _fadeSamples;
                        // 确保淡出因子不会小于0
                        fadeFactor = Math.Max(0.0f, fadeFactor);
                        buffer[offset + i] *= fadeFactor;
                    }
                    _currentSample++;
                }
                
                return samplesRead;
            }
        }

        /// <summary>
        /// 滑音效果提供者
        /// </summary>
        private class GlissandoProvider : ISampleProvider
        {
            private readonly double _startFrequency;
            private readonly double _endFrequency;
            private readonly int _totalSamples;
            private readonly double _volume;
            private readonly int _fadeSamples;
            private int _currentSample = 0;
            private readonly SignalGenerator _signalGenerator;
            
            public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
            
            public GlissandoProvider(double startFrequency, double endFrequency, int durationMs, double volume)
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
                _fadeSamples = (int)(10 * 44.1); // 10ms的淡入淡出
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

        /// <summary>
        /// 播放休止符
        /// </summary>
        /// <param name="duration">持续时间（毫秒）</param>
        public void PlayRest(int duration)
        {
            // 休止符就是静音，只需要等待指定时间
            System.Threading.Thread.Sleep(duration);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _waveOut?.Stop();
                    _waveOut?.Dispose();
                    _waveOut = null;
                }
                _disposed = true;
            }
        }
    }
}