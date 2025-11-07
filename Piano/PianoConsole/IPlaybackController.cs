namespace Piano.Player
{
    /// <summary>
    /// 播放控制接口
    /// </summary>
    public interface IPlaybackController
    {
        /// <summary>
        /// 暂停播放
        /// </summary>
        void Pause();

        /// <summary>
        /// 继续播放
        /// </summary>
        void Resume();

        /// <summary>
        /// 停止播放
        /// </summary>
        void Stop();

        /// <summary>
        /// 设置播放速度
        /// </summary>
        /// <param name="bpm">每分钟节拍数</param>
        void SetSpeed(int bpm);

        /// <summary>
        /// 设置音量
        /// </summary>
        /// <param name="volume">音量（0.0-1.0）</param>
        void SetVolume(double volume);
    }
}