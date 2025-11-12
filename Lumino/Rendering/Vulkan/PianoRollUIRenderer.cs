using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Vulkan;
using EnderDebugger;

namespace Lumino.Rendering.Vulkan
{
    /// <summary>
    /// 钢琴卷帘UI组件渲染系统
    /// 负责网格、琴键、播放头等UI元素的高效Vulkan渲染
    /// </summary>
    public class PianoRollUIRenderer : IDisposable
    {
        private readonly VulkanNoteRenderEngine _noteEngine;
        private readonly Vk _vk;
        private readonly Device _device;

        // UI组件配置
        private GridConfiguration _gridConfig = new();
        private KeyboardConfiguration _keyboardConfig = new();
        private PlayheadConfiguration _playheadConfig = new();

        // UI渲染缓存
        private readonly LineRenderer _lineRenderer;
        private readonly RectangleRenderer _rectRenderer;
        private readonly TextRenderer _textRenderer;

        public PianoRollUIRenderer(VulkanNoteRenderEngine noteEngine, Vk vk, Device device)
        {
            if (noteEngine == null) throw new ArgumentNullException(nameof(noteEngine));
            if (vk == null) throw new ArgumentNullException(nameof(vk));
            
            _noteEngine = noteEngine;
            _vk = vk;
            _device = device;

            _lineRenderer = new LineRenderer(vk, device);
            _rectRenderer = new RectangleRenderer(vk, device);
            _textRenderer = new TextRenderer(vk, device);

            EnderLogger.Instance.Info("PianoRollUIRenderer", "钢琴卷帘UI渲染器已初始化");
        }

        /// <summary>
        /// 配置网格参数
        /// </summary>
        public void ConfigureGrid(GridConfiguration config)
        {
            _gridConfig = config;
        }

        /// <summary>
        /// 配置键盘参数
        /// </summary>
        public void ConfigureKeyboard(KeyboardConfiguration config)
        {
            _keyboardConfig = config;
        }

        /// <summary>
        /// 配置播放头参数
        /// </summary>
        public void ConfigurePlayhead(PlayheadConfiguration config)
        {
            _playheadConfig = config;
        }

        /// <summary>
        /// 渲染网格
        /// </summary>
        public void RenderGrid(RenderFrame frame, float viewWidth, float viewHeight, float timeStart, float timeEnd, float pitchStart, float pitchEnd)
        {
            var gridSpacingX = _gridConfig.TimeGridSpacing;
            var gridSpacingY = _gridConfig.PitchGridSpacing;

            // 绘制垂直线（时间网格）
            for (float t = timeStart; t <= timeEnd; t += gridSpacingX)
            {
                float screenX = (t - timeStart) / (timeEnd - timeStart) * viewWidth;
                _lineRenderer.DrawLine(
                    new Vector2(screenX, 0),
                    new Vector2(screenX, viewHeight),
                    _gridConfig.GridLineColor,
                    _gridConfig.GridLineThickness,
                    frame
                );
            }

            // 绘制水平线（音高网格）
            for (float p = pitchStart; p <= pitchEnd; p += gridSpacingY)
            {
                float screenY = (pitchEnd - p) / (pitchEnd - pitchStart) * viewHeight;
                _lineRenderer.DrawLine(
                    new Vector2(0, screenY),
                    new Vector2(viewWidth, screenY),
                    _gridConfig.GridLineColor,
                    _gridConfig.GridLineThickness,
                    frame
                );
            }
        }

        /// <summary>
        /// 渲染强调网格线（拍子/小节标记）
        /// </summary>
        public void RenderAccentGrid(RenderFrame frame, float viewWidth, float viewHeight, float timeStart, float timeEnd, 
            int beatsPerMeasure, float beatDuration)
        {
            var lineSpacing = beatDuration * beatsPerMeasure;
            var lineColor = _gridConfig.AccentGridLineColor;
            var lineThickness = _gridConfig.AccentGridLineThickness;

            for (float t = timeStart; t <= timeEnd; t += lineSpacing)
            {
                float screenX = (t - timeStart) / (timeEnd - timeStart) * viewWidth;
                _lineRenderer.DrawLine(
                    new Vector2(screenX, 0),
                    new Vector2(screenX, viewHeight),
                    lineColor,
                    lineThickness,
                    frame
                );
            }
        }

        /// <summary>
        /// 渲染钢琴键盘
        /// </summary>
        public void RenderKeyboard(RenderFrame frame, float keyboardWidth, float keyboardHeight, float pitchStart, float pitchEnd)
        {
            const int pitchesPerOctave = 12;
            var whiteKeyCount = (int)Math.Ceiling((pitchEnd - pitchStart) / pitchesPerOctave * 7);
            var keyHeightBase = keyboardHeight / (pitchEnd - pitchStart);

            for (int pitch = (int)Math.Ceiling(pitchStart); pitch < (int)Math.Floor(pitchEnd); pitch++)
            {
                float screenY = (pitchEnd - pitch) / (pitchEnd - pitchStart) * keyboardHeight;
                var isBlackKey = IsBlackKey(pitch);

                if (isBlackKey)
                {
                    // 绘制黑键
                    _rectRenderer.DrawRectangle(
                        new Vector2(keyboardWidth * 0.6f, screenY - keyHeightBase / 2),
                        keyboardWidth * 0.4f,
                        keyHeightBase,
                        _keyboardConfig.BlackKeyColor,
                        frame
                    );
                }
                else
                {
                    // 绘制白键
                    _rectRenderer.DrawRectangle(
                        new Vector2(0, screenY - keyHeightBase / 2),
                        keyboardWidth,
                        keyHeightBase,
                        _keyboardConfig.WhiteKeyColor,
                        frame
                    );

                    // 绘制键盘边框
                    _lineRenderer.DrawLine(
                        new Vector2(0, screenY - keyHeightBase / 2),
                        new Vector2(keyboardWidth, screenY - keyHeightBase / 2),
                        _keyboardConfig.KeyBorderColor,
                        _keyboardConfig.KeyBorderThickness,
                        frame
                    );
                }

                // 绘制按下按键的高亮
                if (_keyboardConfig.PressedKeys.Contains(pitch))
                {
                    _rectRenderer.DrawRectangle(
                        new Vector2(0, screenY - keyHeightBase / 2),
                        keyboardWidth,
                        keyHeightBase,
                        _keyboardConfig.PressedKeyColor,
                        frame,
                        fillOnly: false
                    );
                }
            }
        }

        /// <summary>
        /// 渲染播放头
        /// </summary>
        public void RenderPlayhead(RenderFrame frame, float screenX, float viewHeight)
        {
            // 绘制播放头线
            _lineRenderer.DrawLine(
                new Vector2(screenX, 0),
                new Vector2(screenX, viewHeight),
                _playheadConfig.PlayheadLineColor,
                _playheadConfig.PlayheadLineThickness,
                frame
            );

            // 绘制播放头头部（三角形）
            var headY = 0f;
            var headSize = _playheadConfig.PlayheadHeadSize;
            _rectRenderer.DrawRectangle(
                new Vector2(screenX - headSize / 2, headY),
                headSize,
                headSize / 2,
                _playheadConfig.PlayheadHeadColor,
                frame
            );
        }

        /// <summary>
        /// 渲染选区框
        /// </summary>
        public void RenderSelectionBox(RenderFrame frame, Vector2 start, Vector2 end)
        {
            var rectColor = new Vector4(0.3f, 0.5f, 1.0f, 0.3f);
            var borderColor = new Vector4(0.3f, 0.5f, 1.0f, 1.0f);

            float x = Math.Min(start.X, end.X);
            float y = Math.Min(start.Y, end.Y);
            float width = Math.Abs(end.X - start.X);
            float height = Math.Abs(end.Y - start.Y);

            // 填充框
            _rectRenderer.DrawRectangle(
                new Vector2(x, y),
                width,
                height,
                rectColor,
                frame
            );

            // 边框
            _lineRenderer.DrawLine(new Vector2(x, y), new Vector2(x + width, y), borderColor, 1.0f, frame);
            _lineRenderer.DrawLine(new Vector2(x + width, y), new Vector2(x + width, y + height), borderColor, 1.0f, frame);
            _lineRenderer.DrawLine(new Vector2(x + width, y + height), new Vector2(x, y + height), borderColor, 1.0f, frame);
            _lineRenderer.DrawLine(new Vector2(x, y + height), new Vector2(x, y), borderColor, 1.0f, frame);
        }

        /// <summary>
        /// 检查音高是否为黑键
        /// </summary>
        private bool IsBlackKey(int pitch)
        {
            int localPitch = pitch % 12;
            return localPitch == 1 || localPitch == 3 || localPitch == 6 || localPitch == 8 || localPitch == 10;
        }

        public void Dispose()
        {
            _lineRenderer?.Dispose();
            _rectRenderer?.Dispose();
            _textRenderer?.Dispose();
        }
    }

    /// <summary>
    /// 网格配置
    /// </summary>
    public class GridConfiguration
    {
        public float TimeGridSpacing { get; set; } = 1.0f;  // 时间间隔
        public float PitchGridSpacing { get; set; } = 1.0f; // 音高间隔
        public Vector4 GridLineColor { get; set; } = new(0.3f, 0.3f, 0.3f, 0.5f);
        public float GridLineThickness { get; set; } = 0.5f;
        public Vector4 AccentGridLineColor { get; set; } = new(0.5f, 0.5f, 0.5f, 0.8f);
        public float AccentGridLineThickness { get; set; } = 1.0f;
    }

    /// <summary>
    /// 键盘配置
    /// </summary>
    public class KeyboardConfiguration
    {
        public Vector4 WhiteKeyColor { get; set; } = new(1.0f, 1.0f, 1.0f, 1.0f);
        public Vector4 BlackKeyColor { get; set; } = new(0.1f, 0.1f, 0.1f, 1.0f);
        public Vector4 KeyBorderColor { get; set; } = new(0.5f, 0.5f, 0.5f, 1.0f);
        public float KeyBorderThickness { get; set; } = 1.0f;
        public Vector4 PressedKeyColor { get; set; } = new(1.0f, 0.5f, 0.0f, 1.0f);
        public HashSet<int> PressedKeys { get; set; } = new();
    }

    /// <summary>
    /// 播放头配置
    /// </summary>
    public class PlayheadConfiguration
    {
        public Vector4 PlayheadLineColor { get; set; } = new(1.0f, 0.2f, 0.2f, 1.0f);
        public float PlayheadLineThickness { get; set; } = 2.0f;
        public Vector4 PlayheadHeadColor { get; set; } = new(1.0f, 0.2f, 0.2f, 1.0f);
        public float PlayheadHeadSize { get; set; } = 10.0f;
    }

    /// <summary>
    /// 直线渲染器
    /// </summary>
    public class LineRenderer : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;

        public LineRenderer(Vk vk, Device device)
        {
            _vk = vk;
            _device = device;
        }

        public void DrawLine(Vector2 start, Vector2 end, Vector4 color, float thickness, RenderFrame frame)
        {
            // 生成线段几何体
            // 使用两个三角形形成一个矩形线
            var direction = Vector2.Normalize(end - start);
            var perpendicular = new Vector2(-direction.Y, direction.X) * (thickness / 2.0f);

            var geom = new NoteGeometry();
            geom.Vertices.Add(start + perpendicular);
            geom.Vertices.Add(start - perpendicular);
            geom.Vertices.Add(end - perpendicular);
            geom.Vertices.Add(end + perpendicular);

            geom.Indices.AddRange(new uint[] { 0, 1, 2, 0, 2, 3 });

            // 添加到渲染帧
            // frame.AddGeometry(geom, color); // 实现方式需要根据实际渲染框架调整
        }

        public void Dispose() { }
    }

    /// <summary>
    /// 矩形渲染器
    /// </summary>
    public class RectangleRenderer : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;

        public RectangleRenderer(Vk vk, Device device)
        {
            _vk = vk;
            _device = device;
        }

        public void DrawRectangle(Vector2 position, float width, float height, Vector4 color, RenderFrame frame, bool fillOnly = true)
        {
            var geom = new NoteGeometry();

            // 添加矩形顶点
            geom.Vertices.Add(position);
            geom.Vertices.Add(new Vector2(position.X + width, position.Y));
            geom.Vertices.Add(new Vector2(position.X + width, position.Y + height));
            geom.Vertices.Add(new Vector2(position.X, position.Y + height));

            // 添加三角形索引（两个三角形组成矩形）
            geom.Indices.AddRange(new uint[] { 0, 1, 2, 0, 2, 3 });

            // 添加到渲染帧
            // frame.AddGeometry(geom, color); // 实现方式需要根据实际渲染框架调整
        }

        public void Dispose() { }
    }

    /// <summary>
    /// 文本渲染器
    /// </summary>
    public class TextRenderer : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;

        public TextRenderer(Vk vk, Device device)
        {
            _vk = vk;
            _device = device;
        }

        public void DrawText(string text, Vector2 position, Vector4 color, float fontSize, RenderFrame frame)
        {
            // 文本渲染需要使用独立的文本渲染管线或外部库支持
            // 这里作为接口定义，实际实现可使用：
            // - FreeType + signed distance field (SDF)
            // - 预生成的位图字体
            // - GPU文本渲染库
        }

        public void Dispose() { }
    }
}
