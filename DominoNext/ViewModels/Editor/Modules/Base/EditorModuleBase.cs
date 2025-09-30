using System;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.Models.Music;

namespace DominoNext.ViewModels.Editor.Modules.Base
{
    /// <summary>
    /// 编辑器模块基类 - 提供通用功能和规范
    /// 遵循MVVM设计原则，减少代码重复
    /// </summary>
    public abstract class EditorModuleBase
    {
        #region 服务依赖
        protected readonly ICoordinateService _coordinateService;
        protected PianoRollViewModel? _pianoRollViewModel;
        #endregion

        #region 通用常量
        /// <summary>
        /// 标准防抖动像素阈值
        /// </summary>
        protected const double STANDARD_ANTI_SHAKE_PIXEL_THRESHOLD = 1.0;
        
        /// <summary>
        /// 标准防抖动时间阈值（毫秒）
        /// </summary>
        protected const double STANDARD_ANTI_SHAKE_TIME_THRESHOLD_MS = 100.0;
        #endregion

        #region 构造函数
        protected EditorModuleBase(ICoordinateService coordinateService)
        {
            _coordinateService = coordinateService ?? throw new ArgumentNullException(nameof(coordinateService));
        }
        #endregion

        #region 通用方法
        /// <summary>
        /// 设置钢琴卷帘ViewModel引用
        /// </summary>
        public virtual void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        /// <summary>
        /// 通用音符位置验证
        /// </summary>
        protected static bool IsValidNotePosition(int pitch, double timeValue)
        {
            return pitch >= 0 && pitch <= 127 && timeValue >= 0;
        }

        /// <summary>
        /// 通用坐标转换 - 获取音高
        /// </summary>
        protected int GetPitchFromPosition(Point position)
        {
            if (_pianoRollViewModel == null) return 0;
            return _pianoRollViewModel.GetPitchFromScreenY(position.Y);
        }

        /// <summary>
        /// 通用坐标转换 - 获取时间
        /// </summary>
        protected double GetTimeFromPosition(Point position)
        {
            if (_pianoRollViewModel == null) return 0;
            return _pianoRollViewModel.GetTimeFromScreenX(position.X);
        }

        /// <summary>
        /// 通用坐标转换 - 获取量化后的时间分数
        /// </summary>
        protected MusicalFraction GetQuantizedTimeFromPosition(Point position)
        {
            if (_pianoRollViewModel == null) return new MusicalFraction(0, 1);
            
            var timeValue = GetTimeFromPosition(position);
            var timeFraction = MusicalFraction.FromDouble(timeValue);
            return _pianoRollViewModel.SnapToGrid(timeFraction);
        }

        /// <summary>
        /// 通用防抖动检查 - 基于像素距离
        /// </summary>
        protected static bool IsMovementBelowPixelThreshold(Point start, Point current, double threshold = STANDARD_ANTI_SHAKE_PIXEL_THRESHOLD)
        {
            var deltaX = current.X - start.X;
            var deltaY = current.Y - start.Y;
            var totalMovement = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            return totalMovement < threshold;
        }

        /// <summary>
        /// 通用防抖动检查 - 基于时间
        /// </summary>
        protected static bool IsTimeBelowThreshold(DateTime startTime, double thresholdMs = STANDARD_ANTI_SHAKE_TIME_THRESHOLD_MS)
        {
            return (DateTime.Now - startTime).TotalMilliseconds < thresholdMs;
        }

        /// <summary>
        /// 安全的音符缓存失效调用
        /// </summary>
        protected static void SafeInvalidateNoteCache(NoteViewModel? note)
        {
            note?.InvalidateCache();
        }

        /// <summary>
        /// 批量音符缓存失效调用
        /// </summary>
        protected static void SafeInvalidateNotesCache(System.Collections.Generic.IEnumerable<NoteViewModel> notes)
        {
            foreach (var note in notes)
            {
                SafeInvalidateNoteCache(note);
            }
        }
        #endregion

        #region 抽象方法 - 子类必须实现
        /// <summary>
        /// 模块名称 - 用于调试和日志
        /// </summary>
        public abstract string ModuleName { get; }
        #endregion

        #region 虚拟方法 - 子类可选择重写
        /// <summary>
        /// 模块初始化
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// 模块清理
        /// </summary>
        public virtual void Cleanup() { }
        #endregion
    }
}