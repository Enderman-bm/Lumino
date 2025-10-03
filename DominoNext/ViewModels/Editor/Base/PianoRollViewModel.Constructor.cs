using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Models.Music;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor.Commands;
using DominoNext.ViewModels.Editor.Modules;
using DominoNext.ViewModels.Editor.State;
using DominoNext.ViewModels.Editor.Components;
using DominoNext.ViewModels.Editor.Enums;
using EnderDebugger;

namespace DominoNext.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel构造函数和初始化方法
    /// 包含构造函数、组件初始化、模块初始化和命令初始化
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 构造函数
        /// <summary>
        /// 设计时构造函数 - 仅用于XAML设计器
        /// 注意：这个构造函数仅用于设计时，生产环境应使用依赖注入
        /// </summary>
        public PianoRollViewModel() : this(CreateDesignTimeCoordinateService(), CreateDesignTimeEventCurveCalculationService()) { }

        /// <summary>
        /// 创建设计时使用的坐标服务
        /// </summary>
        private static ICoordinateService CreateDesignTimeCoordinateService()
        {
            // 仅用于设计时，避免在生产环境中调用
            return new DominoNext.Services.Implementation.CoordinateService();
        }

        /// <summary>
        /// 创建设计时使用的事件曲线计算服务
        /// </summary>
        private static IEventCurveCalculationService CreateDesignTimeEventCurveCalculationService()
        {
            return new DominoNext.Services.Implementation.EventCurveCalculationService();
        }

        /// <summary>
        /// 主构造函数 - 使用依赖注入创建实例
        /// 初始化所有组件、模块和状态，并建立事件订阅
        /// </summary>
        /// <param name="coordinateService">坐标服务，用于坐标转换</param>
        /// <param name="eventCurveCalculationService">事件曲线计算服务，用于事件值计算</param>
        public PianoRollViewModel(ICoordinateService? coordinateService, IEventCurveCalculationService? eventCurveCalculationService = null)
        {
            // 使用依赖注入原则，避免直接new具体实现类
            if (coordinateService == null)
            {
                throw new ArgumentNullException(nameof(coordinateService),
                    "PianoRollViewModel需要通过依赖注入容器创建，坐标服务不能为null。请使用IViewModelFactory创建实例。");
            }

            _coordinateService = coordinateService;
            _eventCurveCalculationService = eventCurveCalculationService ?? CreateDesignTimeEventCurveCalculationService();
            _logger = EnderLogger.Instance;

            // 初始化各个组件
            InitializeComponents();
            InitializeModules();
            InitializeCommands();

            // 订阅事件
            SubscribeToEvents();

            // 监听Notes集合变化，自动更新滚动范围
            Notes.CollectionChanged += OnNotesCollectionChanged;

            // 监听当前音轨变化，更新当前音轨音符集合
            PropertyChanged += OnCurrentTrackIndexChanged;

            // 监听事件类型变化
            PropertyChanged += OnEventTypePropertyChanged;
        }
        #endregion

        #region 初始化方法
        /// <summary>
        /// 初始化组件
        /// 创建所有核心组件实例并建立依赖关系
        /// </summary>
        private void InitializeComponents()
        {
            // 初始化组件 - 组件化架构
            _configuration = new PianoRollConfiguration();
            _viewport = new PianoRollViewport();
            _zoomManager = new PianoRollZoomManager();
            _calculations = new PianoRollCalculations(_zoomManager);
            _coordinates = new PianoRollCoordinates(_coordinateService, _calculations, _viewport);
            _commands = new PianoRollCommands(_configuration, _viewport);

            // 初始化滚动条管理器
            _scrollBarManager = new PianoRollScrollBarManager();

            // 初始化工具栏ViewModel
            _toolbar = new ToolbarViewModel(_configuration);
        }

        /// <summary>
        /// 初始化模块和状态
        /// 创建所有功能模块和状态管理器，并设置引用关系
        /// </summary>
        private void InitializeModules()
        {
            // 初始化状态
            _dragState = new DragState();
            _resizeState = new ResizeState();
            _selectionState = new SelectionState();

            // 初始化模块
            _dragModule = new NoteDragModule(_dragState, _coordinateService);
            _resizeModule = new NoteResizeModule(_resizeState, _coordinateService);
            _creationModule = new NoteCreationModule(_coordinateService);
            _selectionModule = new NoteSelectionModule(_selectionState, _coordinateService);
            _previewModule = new NotePreviewModule(_coordinateService);
            _velocityEditingModule = new VelocityEditingModule(_coordinateService);
            _eventCurveDrawingModule = new EventCurveDrawingModule(_eventCurveCalculationService);

            // 设置模块引用 - 让模块能够访问主ViewModel
            _dragModule.SetPianoRollViewModel(this);
            _resizeModule.SetPianoRollViewModel(this);
            _creationModule.SetPianoRollViewModel(this);
            _selectionModule.SetPianoRollViewModel(this);
            _previewModule.SetPianoRollViewModel(this);
            _velocityEditingModule.SetPianoRollViewModel(this);
            _eventCurveDrawingModule.SetPianoRollViewModel(this);

            // 设置滚动条管理器引用
            _scrollBarManager.SetPianoRollViewModel(this);
        }

        /// <summary>
        /// 初始化命令
        /// 创建编辑器命令ViewModel并建立连接
        /// </summary>
        private void InitializeCommands()
        {
            // 简化初始化命令
            EditorCommands = new EditorCommandsViewModel(_coordinateService);
            EditorCommands.SetPianoRollViewModel(this);
        }
        #endregion
    }
}