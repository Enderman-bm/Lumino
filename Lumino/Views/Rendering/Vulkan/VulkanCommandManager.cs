using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Silk.NET.Vulkan;

namespace Lumino.Views.Rendering.Vulkan
{
    /// <summary>
    /// Vulkan命令管理器 - 高性能渲染命令提交和同步
    /// </summary>
    public class VulkanCommandManager : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;
        private readonly CommandPool _commandPool;
        private readonly Queue _graphicsQueue;
        
        // 命令缓冲区池
        private readonly ConcurrentQueue<CommandBuffer> _commandBufferPool = new();
        
        // 性能统计
        private long _totalCommandsSubmitted = 0;
        private long _totalBatchesSubmitted = 0;
        
        // 常量
        private const int MAX_COMMAND_BUFFERS = 16;

        public VulkanCommandManager(Vk vk, Device device, CommandPool commandPool, Queue graphicsQueue)
        {
            _vk = vk;
            _device = device;
            _commandPool = commandPool;
            _graphicsQueue = graphicsQueue;
            
            InitializeCommandBuffers();
        }

        /// <summary>
        /// 初始化命令缓冲区池
        /// </summary>
        private unsafe void InitializeCommandBuffers()
        {
            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = MAX_COMMAND_BUFFERS
            };

            var commandBuffers = new CommandBuffer[MAX_COMMAND_BUFFERS];
            fixed (CommandBuffer* commandBuffersPtr = commandBuffers)
            {
                if (_vk.AllocateCommandBuffers(_device, &allocInfo, commandBuffersPtr) == Result.Success)
                {
                    foreach (var buffer in commandBuffers)
                    {
                        _commandBufferPool.Enqueue(buffer);
                    }
                }
            }
        }

        /// <summary>
        /// 批量提交渲染命令 - 优化的GPU批处理
        /// </summary>
        public unsafe void SubmitRenderBatch(RenderBatch batch)
        {
            if (batch == null || batch.Commands.Count == 0) return;
            
            // 获取命令缓冲区
            if (!_commandBufferPool.TryDequeue(out var commandBuffer))
            {
                // 创建新的命令缓冲区
                var allocInfo = new CommandBufferAllocateInfo
                {
                    SType = StructureType.CommandBufferAllocateInfo,
                    CommandPool = _commandPool,
                    Level = CommandBufferLevel.Primary,
                    CommandBufferCount = 1
                };
                
                _vk.AllocateCommandBuffers(_device, &allocInfo, &commandBuffer);
            }
            
            // 记录命令
            RecordBatchCommands(commandBuffer, batch);
            
            // 提交到队列
            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer
            };
            
            _vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, new Fence());
            
            // 回收命令缓冲区
            _commandBufferPool.Enqueue(commandBuffer);
            
            _totalCommandsSubmitted += batch.Commands.Count;
            _totalBatchesSubmitted++;
        }

        /// <summary>
        /// 记录批处理命令
        /// </summary>
        private unsafe void RecordBatchCommands(CommandBuffer commandBuffer, RenderBatch batch)
        {
            var beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };
            
            _vk.BeginCommandBuffer(commandBuffer, &beginInfo);
            
            // 批量绘制优化
            foreach (var command in batch.Commands)
            {
                ExecuteRenderCommand(commandBuffer, command);
            }
            
            _vk.EndCommandBuffer(commandBuffer);
        }

        /// <summary>
        /// 执行渲染命令
        /// </summary>
        private unsafe void ExecuteRenderCommand(CommandBuffer commandBuffer, RenderCommand command)
        {
            switch (command.Type)
            {
                case RenderCommandType.DrawInstanced:
                    ExecuteDrawInstanced(commandBuffer, command);
                    break;
                case RenderCommandType.DrawIndexed:
                    ExecuteDrawIndexed(commandBuffer, command);
                    break;
            }
        }

        /// <summary>
        /// 执行实例化绘制
        /// </summary>
        private unsafe void ExecuteDrawInstanced(CommandBuffer commandBuffer, RenderCommand command)
        {
            _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, command.Pipeline);
            
            var vertexBuffers = new[] { command.VertexBuffer };
            var offsets = new[] { command.VertexOffset };
            fixed (Silk.NET.Vulkan.Buffer* vertexBuffersPtr = vertexBuffers)
            fixed (ulong* offsetsPtr = offsets)
            {
                _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, vertexBuffersPtr, offsetsPtr);
            }
            
            _vk.CmdDraw(commandBuffer, command.VertexCount, command.InstanceCount, 0, 0);
        }

        /// <summary>
        /// 执行索引绘制
        /// </summary>
        private unsafe void ExecuteDrawIndexed(CommandBuffer commandBuffer, RenderCommand command)
        {
            _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, command.Pipeline);
            
            var vertexBuffers = new[] { command.VertexBuffer };
            var offsets = new[] { command.VertexOffset };
            fixed (Silk.NET.Vulkan.Buffer* vertexBuffersPtr = vertexBuffers)
            fixed (ulong* offsetsPtr = offsets)
            {
                _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, vertexBuffersPtr, offsetsPtr);
            }
            
            _vk.CmdBindIndexBuffer(commandBuffer, command.IndexBuffer, 0, IndexType.Uint32);
            _vk.CmdDrawIndexed(commandBuffer, command.IndexCount, command.InstanceCount, 0, 0, 0);
        }

        /// <summary>
        /// 等待所有命令完成
        /// </summary>
        public unsafe void WaitForCompletion()
        {
            _vk.DeviceWaitIdle(_device);
        }

        /// <summary>
        /// 获取性能统计
        /// </summary>
        public CommandStats GetStats()
        {
            return new CommandStats
            {
                TotalCommandsSubmitted = _totalCommandsSubmitted,
                TotalBatchesSubmitted = _totalBatchesSubmitted,
                AvailableCommandBuffers = _commandBufferPool.Count
            };
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public unsafe void Dispose()
        {
            WaitForCompletion();
            
            // 清理命令缓冲区
            while (_commandBufferPool.TryDequeue(out var buffer))
            {
                _vk.FreeCommandBuffers(_device, _commandPool, 1, &buffer);
            }
        }
    }

    /// <summary>
    /// 渲染批处理
    /// </summary>
    public class RenderBatch
    {
        public List<RenderCommand> Commands { get; } = new();
        
        public void AddCommand(RenderCommand command)
        {
            Commands.Add(command);
        }
        
        public int Count => Commands.Count;
    }

    /// <summary>
    /// 渲染命令
    /// </summary>
    public class RenderCommand
    {
        public RenderCommandType Type { get; set; }
        public Pipeline Pipeline { get; set; }
        public Silk.NET.Vulkan.Buffer VertexBuffer { get; set; }
        public Silk.NET.Vulkan.Buffer IndexBuffer { get; set; }
        public uint VertexCount { get; set; }
        public uint IndexCount { get; set; }
        public uint InstanceCount { get; set; }
        public ulong VertexOffset { get; set; }
    }

    /// <summary>
    /// 渲染命令类型
    /// </summary>
    public enum RenderCommandType
    {
        DrawInstanced,
        DrawIndexed
    }

    /// <summary>
    /// 命令统计
    /// </summary>
    public struct CommandStats
    {
        public long TotalCommandsSubmitted;
        public long TotalBatchesSubmitted;
        public int AvailableCommandBuffers;
    }

    /// <summary>
    /// 批处理构建器
    /// </summary>
    public class BatchBuilder
    {
        private readonly RenderBatch _batch = new();
        
        public void AddDrawCommand(Pipeline pipeline, Silk.NET.Vulkan.Buffer vertexBuffer, 
            uint vertexCount, uint instanceCount, ulong offset = 0)
        {
            _batch.AddCommand(new RenderCommand
            {
                Type = RenderCommandType.DrawInstanced,
                Pipeline = pipeline,
                VertexBuffer = vertexBuffer,
                VertexCount = vertexCount,
                InstanceCount = instanceCount,
                VertexOffset = offset
            });
        }
        
        public void AddIndexedDrawCommand(Pipeline pipeline, Silk.NET.Vulkan.Buffer vertexBuffer,
            Silk.NET.Vulkan.Buffer indexBuffer, uint indexCount, uint instanceCount, ulong offset = 0)
        {
            _batch.AddCommand(new RenderCommand
            {
                Type = RenderCommandType.DrawIndexed,
                Pipeline = pipeline,
                VertexBuffer = vertexBuffer,
                IndexBuffer = indexBuffer,
                IndexCount = indexCount,
                InstanceCount = instanceCount,
                VertexOffset = offset
            });
        }
        
        public RenderBatch Build() => _batch;
    }
}