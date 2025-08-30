using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Services.Interfaces;
using DominoNext.Views.Controls.Editing;

namespace DominoNext.ViewModels.Editor.Commands
{
    /// <summary>
    /// �ع����ı༭������ViewModel - �򻯲�ί�и�ģ��
    /// </summary>
    public partial class EditorCommandsViewModel : ViewModelBase
    {
        #region 成员变量
        private readonly ICoordinateService _coordinateService;
        private PianoRollViewModel? _pianoRollViewModel;
        #endregion

        #region 工具处理器
        private readonly PencilToolHandler _pencilToolHandler;
        private readonly SelectToolHandler _selectToolHandler;
        private readonly EraserToolHandler _eraserToolHandler;
        private readonly KeyboardCommandHandler _keyboardCommandHandler;
        #endregion

        #region 性能优化
        private readonly System.Timers.Timer _updateTimer;
        private Point _pendingPosition;
        private bool _hasPendingUpdate;
        private UpdateType _pendingUpdateType;
        private const double UpdateInterval = 1; // 修改为1，避免0值导致的启动错误

        private enum UpdateType { Preview, Drag, Selection, CreatingNote, Resizing }
        #endregion

        #region 构造函数
        public EditorCommandsViewModel(ICoordinateService coordinateService)
        {
            _coordinateService = coordinateService;

            // 初始化工具处理器
            _pencilToolHandler = new PencilToolHandler();
            _selectToolHandler = new SelectToolHandler();
            _eraserToolHandler = new EraserToolHandler();
            _keyboardCommandHandler = new KeyboardCommandHandler();

            // 性能优化
            _updateTimer = new System.Timers.Timer(UpdateInterval);
            _updateTimer.Elapsed += OnUpdateTimerElapsed;
            _updateTimer.AutoReset = false;
        }

        public void SetPianoRollViewModel(PianoRollViewModel pianoRollViewModel)
        {
            _pianoRollViewModel = pianoRollViewModel;
            
            // 传递给各个ViewModel
            _pencilToolHandler.SetPianoRollViewModel(pianoRollViewModel);
            _selectToolHandler.SetPianoRollViewModel(pianoRollViewModel);
            _eraserToolHandler.SetPianoRollViewModel(pianoRollViewModel);
            _keyboardCommandHandler.SetPianoRollViewModel(pianoRollViewModel);
        }
        #endregion

        #region 交互处理
        [RelayCommand]
        private void HandleInteraction(EditorInteractionArgs args)
        {
            if (_pianoRollViewModel == null) return;

            #if DEBUG
            if (args.InteractionType != EditorInteractionType.Move || 
                _pianoRollViewModel.DragState.IsDragging || _pianoRollViewModel.ResizeState.IsResizing)
            {
                Debug.WriteLine($"�༭������: {args.InteractionType}, ����: {args.Tool}, λ��: {args.Position}");
            }
            #endif

            switch (args.InteractionType)
            {
                case EditorInteractionType.Press:
                    HandlePress(args);
                    break;
                case EditorInteractionType.Move:
                    HandleMove(args);
                    break;
                case EditorInteractionType.Release:
                    HandleRelease(args);
                    break;
            }
        }

        private void HandlePress(EditorInteractionArgs args)
        {
            var clickedNote = _pianoRollViewModel?.GetNoteAtPosition(args.Position);

            switch (args.Tool)
            {
                case EditorTool.Pencil:
                    _pencilToolHandler.HandlePress(args.Position, clickedNote, args.Modifiers);
                    break;
                case EditorTool.Select:
                    _selectToolHandler.HandlePress(args.Position, clickedNote, args.Modifiers);
                    break;
                case EditorTool.Eraser:
                    _eraserToolHandler.HandlePress(clickedNote);
                    break;
                case EditorTool.Cut:
                    // TODO: ʵ���и��
                    break;
            }
        }

        private void HandleMove(EditorInteractionArgs args)
        {
            if (_pianoRollViewModel == null) return;

            // 用以性能优化的Move事件
            ScheduleThrottledUpdate(args.Position, GetUpdateTypeForCurrentState());
        }

        private void HandleRelease(EditorInteractionArgs args)
        {
            if (_pianoRollViewModel == null) return;

            // 停止性能优化
            _hasPendingUpdate = false;
            _updateTimer.Stop();

            // 处理Release事件
            if (_pianoRollViewModel.ResizeState.IsResizing)
            {
                _pianoRollViewModel.ResizeModule.EndResize();
            }
            else if (_pianoRollViewModel.DragState.IsDragging)
            {
                _pianoRollViewModel.DragModule.EndDrag();
            }
            else if (_pianoRollViewModel.SelectionState.IsSelecting)
            {
                _pianoRollViewModel.SelectionModule.EndSelection(_pianoRollViewModel.Notes);
            }
            else if (_pianoRollViewModel.CreationModule.IsCreatingNote)
            {
                _pianoRollViewModel.CreationModule.FinishCreating();
            }
        }

        private UpdateType GetUpdateTypeForCurrentState()
        {
            if (_pianoRollViewModel == null) return UpdateType.Preview;

            if (_pianoRollViewModel.ResizeState.IsResizing)
                return UpdateType.Resizing;
            else if (_pianoRollViewModel.DragState.IsDragging)
                return UpdateType.Drag;
            else if (_pianoRollViewModel.SelectionState.IsSelecting)
                return UpdateType.Selection;
            else if (_pianoRollViewModel.CreationModule.IsCreatingNote)
                return UpdateType.CreatingNote;
            else
                return UpdateType.Preview;
        }

        private void ScheduleThrottledUpdate(Point position, UpdateType updateType)
        {
            _pendingPosition = position;
            _pendingUpdateType = updateType;
            _hasPendingUpdate = true;

            if (!_updateTimer.Enabled)
            {
                _updateTimer.Start();
            }
        }
        #endregion

        #region 键盘处理
        [RelayCommand]
        private void HandleKey(KeyCommandArgs args)
        {
            _keyboardCommandHandler.HandleKey(args);
        }
        #endregion

        #region 预览操作
        [RelayCommand]
        private void ClearPreview()
        {
            _pianoRollViewModel?.PreviewModule.ClearPreview();
        }

        [RelayCommand]
        private void DuplicateSelected()
        {
            _keyboardCommandHandler.HandleKey(new KeyCommandArgs 
            { 
                Key = Avalonia.Input.Key.D, 
                Modifiers = Avalonia.Input.KeyModifiers.Control 
            });
        }

        [RelayCommand]
        private void DeleteSelected()
        {
            _keyboardCommandHandler.HandleKey(new KeyCommandArgs 
            { 
                Key = Avalonia.Input.Key.Delete, 
                Modifiers = Avalonia.Input.KeyModifiers.None 
            });
        }

        [RelayCommand]
        private void QuantizeSelected()
        {
            _keyboardCommandHandler.HandleKey(new KeyCommandArgs 
            { 
                Key = Avalonia.Input.Key.Q, 
                Modifiers = Avalonia.Input.KeyModifiers.None 
            });
        }
        #endregion

        #region 定时器事件
        private void OnUpdateTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // 处理性能优化 - 在UI线程执行
            if (_hasPendingUpdate)
            {
                _hasPendingUpdate = false;

                // 用Dispatcher确保在UI线程执行
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        switch (_pendingUpdateType)
                        {
                            case UpdateType.Preview:
                                _pianoRollViewModel?.PreviewModule.UpdatePreview(_pendingPosition);
                                break;
                            case UpdateType.Drag:
                                _pianoRollViewModel?.DragModule.UpdateDrag(_pendingPosition);
                                break;
                            case UpdateType.Selection:
                                _pianoRollViewModel?.SelectionModule.UpdateSelection(_pendingPosition);
                                break;
                            case UpdateType.CreatingNote:
                                _pianoRollViewModel?.CreationModule.UpdateCreating(_pendingPosition);
                                break;
                            case UpdateType.Resizing:
                                _pianoRollViewModel?.ResizeModule.UpdateResize(_pendingPosition);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"���²���ʱ��������: {ex.Message}");
                    }
                });
            }
        }
        #endregion

        #region 资源释放
        public void Dispose()
        {
            _updateTimer?.Dispose();
        }
        #endregion
    }
}