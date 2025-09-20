using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// 事件曲线视图模型
    /// </summary>
    public class EventCurveViewModel : ObservableObject
    {
        private string _name = string.Empty;
        private EventType _eventType;
        private int _ccNumber;
        private bool _isVisible = true;
        private bool _isSelected;
        private double _minValue;
        private double _maxValue = 127;
        private string _color = "#FF0000";

        /// <summary>
        /// 曲线名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 事件类型
        /// </summary>
        public EventType EventType
        {
            get => _eventType;
            set => SetProperty(ref _eventType, value);
        }

        /// <summary>
        /// CC号（当事件类型为ControlChange时）
        /// </summary>
        public int CCNumber
        {
            get => _ccNumber;
            set => SetProperty(ref _ccNumber, value);
        }

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 最小值
        /// </summary>
        public double MinValue
        {
            get => _minValue;
            set => SetProperty(ref _minValue, value);
        }

        /// <summary>
        /// 最大值
        /// </summary>
        public double MaxValue
        {
            get => _maxValue;
            set => SetProperty(ref _maxValue, value);
        }

        /// <summary>
        /// 颜色
        /// </summary>
        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public EventCurveViewModel()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">曲线名称</param>
        /// <param name="eventType">事件类型</param>
        public EventCurveViewModel(string name, EventType eventType)
        {
            _name = name;
            _eventType = eventType;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">曲线名称</param>
        /// <param name="eventType">事件类型</param>
        /// <param name="ccNumber">CC号</param>
        public EventCurveViewModel(string name, EventType eventType, int ccNumber)
        {
            _name = name;
            _eventType = eventType;
            _ccNumber = ccNumber;
        }
    }
}