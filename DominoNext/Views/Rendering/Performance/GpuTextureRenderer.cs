/*using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Performance;

namespace DominoNext.Views.Rendering.Performance
{
    /// <summary>
    /// GPU加速纹理渲染器 - 利用硬件加速提升大量音符渲染性能
    /// </summary>
    public class GpuTextureRenderer : IDisposable
    {
        private readonly RenderObjectPool _objectPool = RenderObjectPool.Instance;
        private readonly Dictionary<string, RenderTargetBitmap> _textureCache = new();
        private readonly Dictionary<(Color, Size), RenderTargetBitmap> _noteTextureCache = new();
        
        // 纹理大小常量
        private const int TextureWidth = 2048;
        private const int TextureHeight = 2048;
        private const int NoteTextureSize = 64;
        
        // 性能统计
        private int _cacheHitCount = 0;
        private int _cacheMissCount = 0;

        /// <summary>
        /// 预渲染音符纹理到GPU
        /// </summary>
        public async Task<RenderTargetBitmap> GetOrCreateNoteTextureAsync(Color color, Size size, double opacity = 1.0)
        {
            var key = (color, size);
            
            if (_noteTextureCache.TryGetValue(key, out var existingTexture))
            {
                _cacheHitCount++;
                return existingTexture;
            }

            _cacheMissCount++;
            
            return await Task.Run(() =>
            {
                // 创建渲染目标位图
                var texture = new RenderTargetBitmap(
                    new PixelSize((int)size.Width, (int)size.Height),
                    new Vector(96, 96));

                using (var context = texture.CreateDrawingContext())
                {
                    var brush = _objectPool.GetSolidBrush(color, opacity);
                    var pen = _objectPool.GetPen(Color.FromRgb(0, 0, 0), 1.0);
                    var rect = new Rect(0, 0, size.Width, size.Height);
                    
                    context.DrawRectangle(brush, pen, rect);
                }

                _noteTextureCache[key] = texture;
                return texture;
            });
        }

        /// <summary>
        /// 批量渲染音符使用预渲染纹理
        /// </summary>
        public void RenderNotesWithTextures(DrawingContext context, 
                                          IEnumerable<(NoteViewModel note, Rect rect, Color color)> noteData)
        {
            var textureGroups = GroupNotesByTexture(noteData);
            
            foreach (var group in textureGroups)
            {
                var texture = group.Key;
                var positions = group.Value;
                
                // 批量绘制相同纹理的音符
                foreach (var position in positions)
                {
                    context.DrawImage(texture, position);
                }
            }
        }

        /// <summary>
        /// 预渲染大型场景到纹理
        /// </summary>
        public async Task<RenderTargetBitmap> PreRenderSceneAsync(
            IEnumerable<(NoteViewModel note, Rect rect)> visibleNotes,
            PianoRollViewModel viewModel,
            Size sceneSize)
        {
            return await Task.Run(() =>
            {
                var sceneTexture = new RenderTargetBitmap(
                    new PixelSize((int)sceneSize.Width, (int)sceneSize.Height),
                    new Vector(96, 96));

                using (var context = sceneTexture.CreateDrawingContext())
                {
                    // 清除背景
                    context.DrawRectangle(Brushes.Transparent, null, 
                        new Rect(0, 0, sceneSize.Width, sceneSize.Height));

                    // 批量渲染音符
                    var batchRenderer = new BatchNoteRenderer();
                    batchRenderer.BatchRenderNotes(context, visibleNotes, viewModel);
                }

                return sceneTexture;
            });
        }

        /// <summary>
        /// 渲染预渲染的场景纹理
        /// </summary>
        public void RenderPreRenderedScene(DrawingContext context, RenderTargetBitmap sceneTexture, 
                                         Point offset = default)
        {
            if (sceneTexture == null) return;
            
            var destRect = new Rect(offset.X, offset.Y, sceneTexture.Size.Width, sceneTexture.Size.Height);
            context.DrawImage(sceneTexture, destRect);
        }

        /// <summary>
        /// 使用层级细节（LOD）渲染
        /// 根据缩放级别选择不同质量的渲染
        /// </summary>
        public void RenderWithLOD(DrawingContext context, 
                                 IEnumerable<(NoteViewModel note, Rect rect)> noteData,
                                 PianoRollViewModel viewModel)
        {
            var zoom = viewModel.Zoom;
            
            if (zoom < 0.5)
            {
                // 低缩放级别：使用简化渲染
                RenderSimplified(context, noteData);
            }
            else if (zoom < 1.0)
            {
                // 中等缩放级别：使用中等质量渲染
                RenderMediumQuality(context, noteData, viewModel);
            }
            else
            {
                // 高缩放级别：使用完整质量渲染
                var batchRenderer = new BatchNoteRenderer();
                batchRenderer.BatchRenderNotes(context, noteData, viewModel);
            }
        }

        /// <summary>
        /// 简化渲染（低缩放级别）
        /// </summary>
        private void RenderSimplified(DrawingContext context, IEnumerable<(NoteViewModel note, Rect rect)> noteData)
        {
            var brush = _objectPool.GetSolidBrush(Color.Parse("#4CAF50"), 0.8);
            
            foreach (var (note, rect) in noteData)
            {
                // 只绘制填充，不绘制边框
                context.DrawRectangle(brush, null, rect);
            }
        }

        /// <summary>
        /// 中等质量渲染
        /// </summary>
        private void RenderMediumQuality(DrawingContext context, 
                                       IEnumerable<(NoteViewModel note, Rect rect)> noteData,
                                       PianoRollViewModel viewModel)
        {
            var normalBrush = _objectPool.GetSolidBrush(Color.Parse("#4CAF50"), 0.8);
            var selectedBrush = _objectPool.GetSolidBrush(Color.Parse("#FF9800"), 0.8);
            var pen = _objectPool.GetPen(Color.Parse("#2E7D32"), 1);
            
            foreach (var (note, rect) in noteData)
            {
                var brush = note.IsSelected ? selectedBrush : normalBrush;
                context.DrawRectangle(brush, pen, rect);
            }
        }

        /// <summary>
        /// 按纹理分组音符
        /// </summary>
        private Dictionary<RenderTargetBitmap, List<Rect>> GroupNotesByTexture(
            IEnumerable<(NoteViewModel note, Rect rect, Color color)> noteData)
        {
            var groups = new Dictionary<RenderTargetBitmap, List<Rect>>();
            
            foreach (var (note, rect, color) in noteData)
            {
                var textureKey = (color, new Size(NoteTextureSize, NoteTextureSize));
                
                if (_noteTextureCache.TryGetValue(textureKey, out var texture))
                {
                    if (!groups.TryGetValue(texture, out var list))
                    {
                        list = new List<Rect>();
                        groups[texture] = list;
                    }
                    
                    list.Add(rect);
                }
            }
            
            return groups;
        }

        /// <summary>
        /// 清理过期的纹理缓存
        /// </summary>
        public void CleanupTextureCache(int maxCacheSize = 100)
        {
            if (_noteTextureCache.Count <= maxCacheSize) return;
            
            // 清理一半的缓存（简单的LRU策略）
            var toRemove = _noteTextureCache.Count / 2;
            var keysToRemove = new List<(Color, Size)>();
            
            foreach (var kvp in _noteTextureCache)
            {
                if (keysToRemove.Count >= toRemove) break;
                keysToRemove.Add(kvp.Key);
            }
            
            foreach (var key in keysToRemove)
            {
                if (_noteTextureCache.TryGetValue(key, out var texture))
                {
                    texture?.Dispose();
                    _noteTextureCache.Remove(key);
                }
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int hits, int misses, double hitRate) GetCacheStats()
        {
            var total = _cacheHitCount + _cacheMissCount;
            var hitRate = total > 0 ? (double)_cacheHitCount / total : 0.0;
            return (_cacheHitCount, _cacheMissCount, hitRate);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            foreach (var texture in _textureCache.Values)
            {
                texture?.Dispose();
            }
            _textureCache.Clear();
            
            foreach (var texture in _noteTextureCache.Values)
            {
                texture?.Dispose();
            }
            _noteTextureCache.Clear();
        }
    }
}*/