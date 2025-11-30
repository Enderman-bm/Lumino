using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EnderDebugger;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// Vulkan纹理与Avalonia位图的互操作
    /// 将Vulkan离屏渲染结果转换为Avalonia可显示的WriteableBitmap
    /// </summary>
    public class VulkanTextureInterop : IDisposable
    {
        private WriteableBitmap? _bitmap;
        private byte[]? _pixelBuffer;
        private int _width;
        private int _height;
        private bool _disposed = false;

        // 像素格式常量
        private const int BYTES_PER_PIXEL = 4;

        /// <summary>
        /// 当前位图宽度
        /// </summary>
        public int Width => _width;

        /// <summary>
        /// 当前位图高度
        /// </summary>
        public int Height => _height;

        /// <summary>
        /// 获取当前位图（可能为null）
        /// </summary>
        public WriteableBitmap? Bitmap => _bitmap;

        /// <summary>
        /// 位图是否已初始化
        /// </summary>
        public bool IsInitialized => _bitmap != null;

        /// <summary>
        /// 确保位图大小匹配
        /// </summary>
        public void EnsureSize(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                EnderLogger.Instance.Warn("VulkanTextureInterop", $"无效的尺寸: {width}x{height}");
                return;
            }

            if (_bitmap != null && _width == width && _height == height)
            {
                return; // 尺寸相同，无需重建
            }

            try
            {
                _width = width;
                _height = height;

                // 创建新的位图
                var pixelSize = new PixelSize(width, height);
                var dpi = new Vector(96, 96);
                _bitmap = new WriteableBitmap(pixelSize, dpi, Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Premul);

                // 创建像素缓冲区
                _pixelBuffer = new byte[width * height * BYTES_PER_PIXEL];

                EnderLogger.Instance.Debug("VulkanTextureInterop", $"位图创建成功: {width}x{height}");
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanTextureInterop", "创建位图失败");
                _bitmap = null;
                _pixelBuffer = null;
            }
        }

        /// <summary>
        /// 从Vulkan离屏渲染器更新位图
        /// </summary>
        public unsafe void UpdateFromOffscreenRenderer(VulkanOffscreenRenderer renderer)
        {
            if (renderer == null || !renderer.IsInitialized)
            {
                return;
            }

            // 确保尺寸匹配
            EnsureSize((int)renderer.Width, (int)renderer.Height);

            if (_bitmap == null || _pixelBuffer == null)
            {
                return;
            }

            try
            {
                // 从Vulkan复制数据到CPU缓冲区
                renderer.CopyToBuffer(_pixelBuffer);

                // 更新位图
                using (var frameBuffer = _bitmap.Lock())
                {
                    var sourcePtr = Marshal.UnsafeAddrOfPinnedArrayElement(_pixelBuffer, 0);
                    var destPtr = frameBuffer.Address;
                    var rowBytes = _width * BYTES_PER_PIXEL;
                    var stride = frameBuffer.RowBytes;

                    if (stride == rowBytes)
                    {
                        // 连续内存，一次性复制
                        System.Buffer.MemoryCopy(sourcePtr.ToPointer(), destPtr.ToPointer(),
                            _height * stride, _height * rowBytes);
                    }
                    else
                    {
                        // 需要逐行复制
                        for (int y = 0; y < _height; y++)
                        {
                            var srcRow = sourcePtr + y * rowBytes;
                            var dstRow = destPtr + y * stride;
                            System.Buffer.MemoryCopy(srcRow.ToPointer(), dstRow.ToPointer(),
                                stride, rowBytes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanTextureInterop", "更新位图失败");
            }
        }

        /// <summary>
        /// 直接从内存指针更新位图
        /// </summary>
        public unsafe void UpdateFromPointer(IntPtr sourcePtr, int width, int height)
        {
            if (sourcePtr == IntPtr.Zero)
            {
                return;
            }

            // 确保尺寸匹配
            EnsureSize(width, height);

            if (_bitmap == null)
            {
                return;
            }

            try
            {
                using (var frameBuffer = _bitmap.Lock())
                {
                    var destPtr = frameBuffer.Address;
                    var rowBytes = width * BYTES_PER_PIXEL;
                    var stride = frameBuffer.RowBytes;

                    if (stride == rowBytes)
                    {
                        // 连续内存，一次性复制
                        System.Buffer.MemoryCopy(sourcePtr.ToPointer(), destPtr.ToPointer(),
                            height * stride, height * rowBytes);
                    }
                    else
                    {
                        // 需要逐行复制
                        for (int y = 0; y < height; y++)
                        {
                            var srcRow = sourcePtr + y * rowBytes;
                            var dstRow = destPtr + y * stride;
                            System.Buffer.MemoryCopy(srcRow.ToPointer(), dstRow.ToPointer(),
                                stride, rowBytes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanTextureInterop", "从指针更新位图失败");
            }
        }

        /// <summary>
        /// 清空位图
        /// </summary>
        public unsafe void Clear(byte r = 0, byte g = 0, byte b = 0, byte a = 0)
        {
            if (_bitmap == null || _pixelBuffer == null)
            {
                return;
            }

            try
            {
                // 填充像素缓冲区
                for (int i = 0; i < _pixelBuffer.Length; i += BYTES_PER_PIXEL)
                {
                    _pixelBuffer[i] = b;     // B
                    _pixelBuffer[i + 1] = g; // G
                    _pixelBuffer[i + 2] = r; // R
                    _pixelBuffer[i + 3] = a; // A
                }

                // 更新位图
                using (var frameBuffer = _bitmap.Lock())
                {
                    var sourcePtr = Marshal.UnsafeAddrOfPinnedArrayElement(_pixelBuffer, 0);
                    var destPtr = frameBuffer.Address;
                    var totalBytes = _width * _height * BYTES_PER_PIXEL;
                    System.Buffer.MemoryCopy(sourcePtr.ToPointer(), destPtr.ToPointer(),
                        totalBytes, totalBytes);
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanTextureInterop", "清空位图失败");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _bitmap = null;
            _pixelBuffer = null;

            GC.SuppressFinalize(this);
        }

        ~VulkanTextureInterop()
        {
            Dispose();
        }
    }
}
