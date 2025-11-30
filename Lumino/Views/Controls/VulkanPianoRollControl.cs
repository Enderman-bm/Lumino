using Avalonia;
using Avalonia.Controls;
using System.Collections.Generic;
using Avalonia.Media;
using System.Linq;
using Avalonia.Threading;
using Lumino.Rendering.Vulkan;
using Lumino.Services.Implementation;
using Lumino.ViewModels.Editor;
using System;
using System.Diagnostics;
using EnderDebugger;
using System.Numerics;
using Silk.NET.Vulkan;

namespace Lumino.Views.Controls
{
    /// <summary>
    /// Vulkan钢琴卷帘控件
    /// 使用VulkanNoteRenderEngine进行高性能渲染
    /// </summary>
    public class VulkanPianoRollControl : Control
    {
        private VulkanManager? _vulkanManager;
        private VulkanNoteRenderEngine? _noteEngine;
        private PianoRollUIRenderer? _uiRenderer;
        private DispatcherTimer _renderTimer;
        
        public static readonly StyledProperty<PianoRollViewModel> ViewModelProperty =
            AvaloniaProperty.Register<VulkanPianoRollControl, PianoRollViewModel>(nameof(ViewModel));

        public PianoRollViewModel ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public VulkanPianoRollControl()
        {
            InitializeVulkan();
            
            _renderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16.67) // 60 FPS
            };
            _renderTimer.Tick += OnRenderTimerTick;
            _renderTimer.Start();
        }

        private void InitializeVulkan()
        {
            try
            {
                // 获取全局VulkanRenderService实例
                var renderService = VulkanRenderService.Instance;
                
                if (renderService.IsInitialized && renderService.VulkanManager != null)
                {
                    _vulkanManager = renderService.VulkanManager;
                    
                    // 使用VulkanManager暴露的方法创建渲染引擎
                    _noteEngine = new VulkanNoteRenderEngine(
                        _vulkanManager.GetVk(),
                        _vulkanManager.GetDevice(),
                        _vulkanManager.GetGraphicsQueue(),
                        _vulkanManager.GetCommandPool(),
                        _vulkanManager.GetRenderPass(),
                        _vulkanManager.GetNotePipeline(),
                        _vulkanManager.GetNotePipelineLayout()
                    );
                    
                    _uiRenderer = new PianoRollUIRenderer(_noteEngine, _vulkanManager.GetVk(), _vulkanManager.GetDevice());
                    
                    EnderLogger.Instance.Info("VulkanPianoRollControl", "Vulkan引擎初始化成功");
                }
                else
                {
                    EnderLogger.Instance.Warn("VulkanPianoRollControl", "Vulkan服务未初始化，无法创建渲染引擎");
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("VulkanPianoRollControl", $"初始化Vulkan失败: {ex.Message}");
            }
        }

        private void OnRenderTimerTick(object? sender, EventArgs e)
        {
            if (_noteEngine != null && ViewModel != null)
            {
                try
                {
                    // 1. 开始帧
                    var frame = _noteEngine.BeginFrame();
                    
                    // 准备音符数据列表
                    var noteDataList = new List<NoteDrawData>();

                    // 2. 提交音符数据
                    if (ViewModel.Notes != null)
                    {
                        foreach (var note in ViewModel.Notes)
                        {
                            // 转换音符坐标
                            // 将ViewModel中的音符数据转换为渲染引擎需要的格式
                            var noteData = new NoteDrawData(
                                new Vector2((float)note.StartPosition.ToDouble(), (float)note.Pitch),
                                (float)note.Duration.ToDouble(),
                                1.0f,
                                0.1f,
                                (byte)note.Pitch,
                                (byte)note.Velocity,
                                0
                            );
                            
                            _noteEngine.DrawNote(noteData, frame);
                            noteDataList.Add(noteData);
                        }
                    }

                    // 3. 预渲染节拍 (Sono's Advice)
                    // 根据Sono的建议，预渲染节拍到纹理，并在像素中存储元数据
                    if (ViewModel.MidiFileDuration > 0)
                    {
                        _noteEngine.PrerenderBeats(0, (int)Math.Ceiling(ViewModel.MidiFileDuration), noteDataList);
                    }

                    // 4. 渲染UI
                    if (_uiRenderer != null)
                    {
                        var gridConfig = new GridConfiguration
                        {
                            TimeGridSpacing = (float)ViewModel.TimeToPixelScale,
                            PitchGridSpacing = 1.0f,
                            GridLineColor = new Vector4(0.5f, 0.5f, 0.5f, 0.5f)
                        };
                        _uiRenderer.ConfigureGrid(gridConfig);
                        
                        // 渲染网格
                        // 假设视图范围从0到TotalWidth，音高从0到128
                        double totalWidth = 0;
                        if (ViewModel.Notes != null)
                        {
                            totalWidth = ViewModel.Calculations.CalculateEffectiveSongLength(
                                ViewModel.Notes.Select(n => n.StartPosition + n.Duration));
                        }
                        
                        _uiRenderer.RenderGrid(
                            frame, 
                            (float)Bounds.Width, 
                            (float)Bounds.Height, 
                            0, 
                            (float)totalWidth, 
                            0, 
                            128
                        );
                        
                        // 渲染播放头
                        float playheadX = (float)(ViewModel.TimelinePosition * ViewModel.TimeToPixelScale);
                        _uiRenderer.RenderPlayhead(frame, playheadX, (float)Bounds.Height);
                    }

                    // 5. 提交渲染
                    unsafe
                    {
                        if (_vulkanManager == null) return;

                        // 从命令池分配命令缓冲区
                        var allocInfo = new CommandBufferAllocateInfo
                        {
                            SType = StructureType.CommandBufferAllocateInfo,
                            CommandPool = _vulkanManager.GetCommandPool(),
                            Level = CommandBufferLevel.Primary,
                            CommandBufferCount = 1
                        };

                        CommandBuffer commandBuffer;
                        var result = _vulkanManager.GetVk().AllocateCommandBuffers(_vulkanManager.GetDevice(), &allocInfo, &commandBuffer);
                        if (result != Result.Success)
                        {
                            EnderLogger.Instance.Error("VulkanPianoRollControl", $"分配命令缓冲区失败: {result}");
                            return;
                        }

                        _noteEngine.SubmitFrame(frame, commandBuffer);
                    }
                }
                catch (Exception ex)
                {
                    EnderLogger.Instance.Error("VulkanPianoRollControl", $"渲染帧失败: {ex.Message}");
                }
            }
            
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            
            // 如果Vulkan不可用，回退到Skia渲染或显示占位符
            if (_noteEngine == null)
            {
                // 尝试使用Skia渲染
                // 这里应该调用Skia渲染逻辑，或者至少不显示错误信息遮挡UI
                // 暂时只绘制背景，避免"Vulkan Engine Not Initialized"文字遮挡
                
                // var bounds = Bounds;
                // context.DrawRectangle(Brushes.Black, null, bounds);
                
                // var text = new FormattedText(
                //     "Vulkan Engine Not Initialized",
                //     System.Globalization.CultureInfo.CurrentCulture,
                //     FlowDirection.LeftToRight,
                //     Typeface.Default,
                //     20,
                //     Brushes.White
                // );
                // context.DrawText(text, new Point(10, 10));
            }
        }
    }
}
