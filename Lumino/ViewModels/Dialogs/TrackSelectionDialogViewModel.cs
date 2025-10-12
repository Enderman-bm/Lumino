using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.ViewModels;

namespace Lumino.ViewModels.Dialogs
{
    /// <summary>
    /// 音轨选择对话框ViewModel
    /// </summary>
    public partial class TrackSelectionDialogViewModel : ViewModelBase
    {
        private readonly TrackSelectorViewModel _trackSelector;

        /// <summary>
        /// 可选择的音轨列表
        /// </summary>
        public ObservableCollection<TrackSelectionItem> AvailableTracks { get; } = new();

        /// <summary>
        /// 选中的音轨索引
        /// </summary>
        public ObservableCollection<int> SelectedTrackIndices { get; } = new();

        /// <summary>
        /// 是否正在进行框选
        /// </summary>
        [ObservableProperty]
        private bool _isSelecting = false;

        /// <summary>
        /// 框选起始索引
        /// </summary>
        [ObservableProperty]
        private int _selectionStartIndex = -1;

        /// <summary>
        /// 框选结束索引
        /// </summary>
        [ObservableProperty]
        private int _selectionEndIndex = -1;

        public TrackSelectionDialogViewModel(TrackSelectorViewModel trackSelector)
        {
            _trackSelector = trackSelector;
            InitializeAvailableTracks();
        }

        /// <summary>
        /// 初始化可选择的音轨列表
        /// </summary>
        private void InitializeAvailableTracks()
        {
            for (int i = 0; i < _trackSelector.Tracks.Count; i++)
            {
                var track = _trackSelector.Tracks[i];
                var item = new TrackSelectionItem
                {
                    Index = i,
                    Name = track.TrackName,
                    IsSelected = SelectedTrackIndices.Contains(i)
                };
                
                // 订阅 IsSelected 属性变化事件
                item.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(TrackSelectionItem.IsSelected))
                    {
                        var selectionItem = sender as TrackSelectionItem;
                        if (selectionItem != null)
                        {
                            if (selectionItem.IsSelected)
                            {
                                if (!SelectedTrackIndices.Contains(selectionItem.Index))
                                {
                                    SelectedTrackIndices.Add(selectionItem.Index);
                                }
                            }
                            else
                            {
                                SelectedTrackIndices.Remove(selectionItem.Index);
                            }
                        }
                    }
                };
                
                AvailableTracks.Add(item);
            }
        }

        /// <summary>
        /// 切换音轨选择状态
        /// </summary>
        [RelayCommand]
        public void ToggleTrackSelection(TrackSelectionItem item)
        {
            if (item == null) return;

            item.IsSelected = !item.IsSelected;

            if (item.IsSelected)
            {
                if (!SelectedTrackIndices.Contains(item.Index))
                {
                    SelectedTrackIndices.Add(item.Index);
                }
            }
            else
            {
                SelectedTrackIndices.Remove(item.Index);
            }
        }

        /// <summary>
        /// 开始框选
        /// </summary>
        [RelayCommand]
        public void StartSelection(int startIndex)
        {
            IsSelecting = true;
            SelectionStartIndex = startIndex;
            SelectionEndIndex = startIndex;
        }

        /// <summary>
        /// 更新框选
        /// </summary>
        [RelayCommand]
        public void UpdateSelection(int endIndex)
        {
            if (!IsSelecting) return;
            SelectionEndIndex = endIndex;
        }

        /// <summary>
        /// 结束框选
        /// </summary>
        [RelayCommand]
        public void EndSelection()
        {
            if (!IsSelecting) return;

            IsSelecting = false;

            // 应用框选
            int start = Math.Min(SelectionStartIndex, SelectionEndIndex);
            int end = Math.Max(SelectionStartIndex, SelectionEndIndex);

            for (int i = start; i <= end; i++)
            {
                if (i >= 0 && i < AvailableTracks.Count)
                {
                    var item = AvailableTracks[i];
                    item.IsSelected = true;
                    if (!SelectedTrackIndices.Contains(item.Index))
                    {
                        SelectedTrackIndices.Add(item.Index);
                    }
                }
            }

            SelectionStartIndex = -1;
            SelectionEndIndex = -1;
        }

        /// <summary>
        /// 全选
        /// </summary>
        [RelayCommand]
        public void SelectAll()
        {
            foreach (var item in AvailableTracks)
            {
                item.IsSelected = true;
                if (!SelectedTrackIndices.Contains(item.Index))
                {
                    SelectedTrackIndices.Add(item.Index);
                }
            }
        }

        /// <summary>
        /// 全不选
        /// </summary>
        [RelayCommand]
        public void SelectNone()
        {
            foreach (var item in AvailableTracks)
            {
                item.IsSelected = false;
            }
            SelectedTrackIndices.Clear();
        }

        /// <summary>
        /// 反选
        /// </summary>
        [RelayCommand]
        public void InvertSelection()
        {
            foreach (var item in AvailableTracks)
            {
                item.IsSelected = !item.IsSelected;
                if (item.IsSelected)
                {
                    if (!SelectedTrackIndices.Contains(item.Index))
                    {
                        SelectedTrackIndices.Add(item.Index);
                    }
                }
                else
                {
                    SelectedTrackIndices.Remove(item.Index);
                }
            }
        }
    }

    /// <summary>
    /// 音轨选择项
    /// </summary>
    public partial class TrackSelectionItem : ObservableObject
    {
        /// <summary>
        /// 音轨索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 音轨名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 是否选中
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;
    }
}