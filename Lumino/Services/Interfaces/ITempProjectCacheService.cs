using Lumino.Models.Music;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// 临时工程缓存服务接口
    /// 用于在MIDI导入过程中将音符数据缓存到硬盘而非内存
    /// </summary>
    public interface ITempProjectCacheService : IDisposable
    {
        /// <summary>
        /// 获取临时缓存目录路径（程序根目录下的temp文件夹）
        /// </summary>
        string TempCacheDirectory { get; }

        /// <summary>
        /// 当前缓存文件路径（如果有活动的缓存会话）
        /// </summary>
        string? CurrentCacheFilePath { get; }

        /// <summary>
        /// 开始新的缓存会话
        /// 创建临时文件并返回会话ID
        /// </summary>
        /// <returns>缓存会话ID</returns>
        string BeginCacheSession();

        /// <summary>
        /// 向缓存写入音符（流式写入）
        /// </summary>
        /// <param name="note">要写入的音符</param>
        Task WriteNoteAsync(Note note);

        /// <summary>
        /// 向缓存批量写入音符
        /// </summary>
        /// <param name="notes">要写入的音符集合</param>
        Task WriteNotesAsync(IEnumerable<Note> notes);

        /// <summary>
        /// 完成缓存写入并准备读取
        /// </summary>
        Task FinishWritingAsync();

        /// <summary>
        /// 从缓存按批次读取音符（流式读取）
        /// </summary>
        /// <param name="batchSize">每批次读取的音符数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>音符批次的异步枚举</returns>
        IAsyncEnumerable<IList<Note>> ReadNotesInBatchesAsync(int batchSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取缓存中的音符总数
        /// </summary>
        /// <returns>音符总数</returns>
        long GetCachedNoteCount();

        /// <summary>
        /// 结束并清理当前缓存会话
        /// </summary>
        void EndCacheSession();

        /// <summary>
        /// 清理所有临时缓存文件
        /// </summary>
        void CleanupAllCacheFiles();
    }
}
