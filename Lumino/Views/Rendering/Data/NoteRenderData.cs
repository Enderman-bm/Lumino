using System;
using System.Runtime.InteropServices;
using Avalonia;
using Lumino.Services.Interfaces;

namespace Lumino.Views.Rendering.Data
{
    /// <summary>
    /// 轻量级音符渲染数据 - 用于替代NoteViewModel进行渲染
    /// 仅包含渲染所需的最小数据，减少内存占用
    /// 64字节对齐，适合GPU传输
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NoteRenderData
    {
        // 基本属性（32字节，与NoteData一致）
        public readonly int Pitch;
        public readonly float StartPosition;
        public readonly float Duration;
        public readonly int Velocity;
        public readonly int TrackIndex;
        public readonly int MidiChannel;
        
        // 渲染状态（8字节）
        public byte IsSelected;
        public byte IsPreview;
        public byte IsHovered;
        public byte LodLevel;
        private readonly int _padding1;
        
        // 缓存的屏幕坐标（24字节）
        public float ScreenX;
        public float ScreenY;
        public float ScreenWidth;
        public float ScreenHeight;
        private readonly float _padding2;
        private readonly float _padding3;

        // 总计64字节

        public NoteRenderData(NoteData note, byte isSelected = 0, byte isPreview = 0)
        {
            Pitch = note.Pitch;
            StartPosition = (float)note.StartPosition;
            Duration = (float)note.Duration;
            Velocity = note.Velocity;
            TrackIndex = note.TrackIndex;
            MidiChannel = note.MidiChannel;
            
            IsSelected = isSelected;
            IsPreview = isPreview;
            IsHovered = 0;
            LodLevel = 0;
            _padding1 = 0;
            
            ScreenX = 0;
            ScreenY = 0;
            ScreenWidth = 0;
            ScreenHeight = 0;
            _padding2 = 0;
            _padding3 = 0;
        }

        /// <summary>
        /// 计算并缓存屏幕坐标
        /// </summary>
        /// <param name="baseQuarterNoteWidth">四分音符的像素宽度</param>
        /// <param name="keyHeight">每个键的高度</param>
        /// <param name="scrollX">水平滚动偏移</param>
        /// <param name="scrollY">垂直滚动偏移</param>
        public void CalculateScreenRect(double baseQuarterNoteWidth, double keyHeight, double scrollX, double scrollY)
        {
            ScreenX = (float)(StartPosition * baseQuarterNoteWidth - scrollX);
            ScreenY = (float)((127 - Pitch) * keyHeight - scrollY);
            ScreenWidth = (float)Math.Max(4.0, Duration * baseQuarterNoteWidth);
            ScreenHeight = (float)keyHeight;
        }

        /// <summary>
        /// 获取屏幕矩形
        /// </summary>
        public Rect GetScreenRect()
        {
            return new Rect(ScreenX, ScreenY, ScreenWidth, ScreenHeight);
        }

        /// <summary>
        /// 检查是否在视口内可见
        /// </summary>
        public bool IsVisibleInViewport(Rect viewport)
        {
            return ScreenX + ScreenWidth >= viewport.Left &&
                   ScreenX <= viewport.Right &&
                   ScreenY + ScreenHeight >= viewport.Top &&
                   ScreenY <= viewport.Bottom;
        }

        /// <summary>
        /// 结束位置
        /// </summary>
        public float EndPosition => StartPosition + Duration;
    }

    /// <summary>
    /// 音符渲染批次 - 用于GPU批处理渲染
    /// </summary>
    public class NoteRenderBatch
    {
        /// <summary>
        /// 渲染数据数组
        /// </summary>
        public NoteRenderData[] Notes;
        
        /// <summary>
        /// 有效音符数量
        /// </summary>
        public int Count;
        
        /// <summary>
        /// 批次容量
        /// </summary>
        public int Capacity;

        /// <summary>
        /// 是否需要上传到GPU
        /// </summary>
        public bool IsDirty;

        public NoteRenderBatch(int capacity = 4096)
        {
            Capacity = capacity;
            Notes = new NoteRenderData[capacity];
            Count = 0;
            IsDirty = true;
        }

        /// <summary>
        /// 清空批次
        /// </summary>
        public void Clear()
        {
            Count = 0;
            IsDirty = true;
        }

        /// <summary>
        /// 添加音符到批次
        /// </summary>
        public bool TryAdd(NoteRenderData note)
        {
            if (Count >= Capacity)
            {
                return false;
            }
            Notes[Count++] = note;
            IsDirty = true;
            return true;
        }

        /// <summary>
        /// 扩展容量
        /// </summary>
        public void EnsureCapacity(int minCapacity)
        {
            if (Capacity >= minCapacity) return;
            
            var newCapacity = Math.Max(minCapacity, Capacity * 2);
            var newNotes = new NoteRenderData[newCapacity];
            Array.Copy(Notes, newNotes, Count);
            Notes = newNotes;
            Capacity = newCapacity;
            IsDirty = true;
        }
    }
}
