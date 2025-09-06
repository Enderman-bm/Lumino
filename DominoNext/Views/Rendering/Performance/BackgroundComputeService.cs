/*using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Views.Rendering.Performance
{
    /// <summary>
    /// 后台计算服务 - 将重计算任务移到后台线程
    /// </summary>
    public class BackgroundComputeService
    {
        private static readonly Lazy<BackgroundComputeService> _instance = new(() => new BackgroundComputeService());
        public static BackgroundComputeService Instance => _instance.Value;

        private readonly SemaphoreSlim _computeSemaphore = new(Environment.ProcessorCount, Environment.ProcessorCount);
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTasks = new();

        private BackgroundComputeService() { }

        /// <summary>
        /// 异步计算可见音符
        /// </summary>
        public async Task<Dictionary<NoteViewModel, Rect>> ComputeVisibleNotesAsync(
            IEnumerable<NoteViewModel> notes,
            PianoRollViewModel viewModel,
            Rect viewport,
            string taskId = "visible_notes")
        {
            // 取消之前的任务
            if (_runningTasks.TryRemove(taskId, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _runningTasks[taskId] = cts;

            try
            {
                await _computeSemaphore.WaitAsync(cts.Token);

                return await Task.Run(() =>
                {
                    var result = new Dictionary<NoteViewModel, Rect>();
                    var expandedViewport = viewport.Inflate(100);

                    foreach (var note in notes)
                    {
                        if (cts.Token.IsCancellationRequested)
                            break;

                        var noteRect = CalculateNoteRect(note, viewModel);
                        if (noteRect.Intersects(expandedViewport))
                        {
                            result[note] = noteRect;
                        }
                    }

                    return result;
                }, cts.Token);
            }
            finally
            {
                _computeSemaphore.Release();
                _runningTasks.TryRemove(taskId, out _);
                cts.Dispose();
            }
        }

        /// <summary>
        /// 异步预计算音符几何信息
        /// </summary>
        public async Task PrecomputeNoteGeometryAsync(
            IEnumerable<NoteViewModel> notes,
            PianoRollViewModel viewModel,
            string taskId = "precompute_geometry")
        {
            // 取消之前的任务
            if (_runningTasks.TryRemove(taskId, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _runningTasks[taskId] = cts;

            try
            {
                await _computeSemaphore.WaitAsync(cts.Token);

                await Task.Run(() =>
                {
                    var timeToPixelScale = viewModel.TimeToPixelScale;
                    var keyHeight = viewModel.KeyHeight;

                    foreach (var note in notes)
                    {
                        if (cts.Token.IsCancellationRequested)
                            break;

                        // 触发缓存计算
                        _ = note.GetX(timeToPixelScale);
                        _ = note.GetY(keyHeight);
                        _ = note.GetWidth(timeToPixelScale);
                        _ = note.GetHeight(keyHeight);
                    }
                }, cts.Token);
            }
            finally
            {
                _computeSemaphore.Release();
                _runningTasks.TryRemove(taskId, out _);
                cts.Dispose();
            }
        }

        /// <summary>
        /// 异步计算脏区域
        /// </summary>
        public async Task<List<Rect>> ComputeDirtyRegionsAsync(
            IEnumerable<NoteViewModel> changedNotes,
            PianoRollViewModel viewModel,
            string taskId = "dirty_regions")
        {
            // 取消之前的任务
            if (_runningTasks.TryRemove(taskId, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _runningTasks[taskId] = cts;

            try
            {
                await _computeSemaphore.WaitAsync(cts.Token);

                return await Task.Run(() =>
                {
                    var dirtyRegions = new List<Rect>();

                    foreach (var note in changedNotes)
                    {
                        if (cts.Token.IsCancellationRequested)
                            break;

                        var noteRect = CalculateNoteRect(note, viewModel);
                        // 扩展矩形以包含边框和可能的阴影
                        var expandedRect = noteRect.Inflate(5);
                        dirtyRegions.Add(expandedRect);
                    }

                    return dirtyRegions;
                }, cts.Token);
            }
            finally
            {
                _computeSemaphore.Release();
                _runningTasks.TryRemove(taskId, out _);
                cts.Dispose();
            }
        }

        /// <summary>
        /// 取消指定任务
        /// </summary>
        public void CancelTask(string taskId)
        {
            if (_runningTasks.TryRemove(taskId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        /// <summary>
        /// 取消所有任务
        /// </summary>
        public void CancelAllTasks()
        {
            foreach (var kvp in _runningTasks)
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
            _runningTasks.Clear();
        }

        /// <summary>
        /// 计算音符矩形
        /// </summary>
        private static Rect CalculateNoteRect(NoteViewModel note, PianoRollViewModel viewModel)
        {
            var absoluteX = note.GetX(viewModel.TimeToPixelScale);
            var absoluteY = note.GetY(viewModel.KeyHeight);
            var width = Math.Max(4, note.GetWidth(viewModel.TimeToPixelScale));
            var height = Math.Max(2, note.GetHeight(viewModel.KeyHeight) - 1);

            var x = absoluteX - viewModel.CurrentScrollOffset;
            var y = absoluteY - viewModel.VerticalScrollOffset;

            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            CancelAllTasks();
            _computeSemaphore?.Dispose();
        }
    }
}*/