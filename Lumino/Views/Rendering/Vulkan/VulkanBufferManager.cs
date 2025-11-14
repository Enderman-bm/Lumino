using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Silk.NET.Vulkan;

namespace Lumino.Views.Rendering.Vulkan
{
    /// <summary>
    /// Vulkan缓冲区管理器 - 高性能GPU内存管理
    /// </summary>
    public class VulkanBufferManager : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;
        private readonly PhysicalDevice _physicalDevice;
        
        // GPU内存池 - 按类型和大小分类
        private readonly ConcurrentDictionary<BufferKey, ConcurrentQueue<BufferInfo>> _bufferPools = new();
        private readonly ConcurrentDictionary<BufferKey, BufferInfo> _activeBuffers = new();
        
        // 性能统计
        private long _totalAllocatedMemory = 0;
        private long _totalUsedMemory = 0;
        private int _bufferReuseCount = 0;
        
        // 常量定义
        private const int MAX_POOL_SIZE_PER_TYPE = 100;
        private const long MAX_TOTAL_MEMORY = 512 * 1024 * 1024; // 512MB

        public VulkanBufferManager(Vk vk, Device device, PhysicalDevice physicalDevice)
        {
            _vk = vk;
            _device = device;
            _physicalDevice = physicalDevice;
        }

        /// <summary>
        /// 获取或创建顶点缓冲区 - 带内存池优化
        /// </summary>
        public unsafe BufferInfo GetOrCreateVertexBuffer<T>(T[] data, BufferUsageFlags usage = BufferUsageFlags.VertexBufferBit) where T : unmanaged
        {
            var key = new BufferKey(BufferType.Vertex, (uint)(data.Length * sizeof(T)), usage);
            
            // 尝试从池获取
            if (_bufferPools.TryGetValue(key, out var pool) && pool.TryDequeue(out var bufferInfo))
            {
                _bufferReuseCount++;
                
                // 重用现有缓冲区，更新数据
                if (bufferInfo.Size >= (ulong)(data.Length * sizeof(T)))
                {
                    UpdateBufferData(bufferInfo, data);
                    _activeBuffers[key] = bufferInfo;
                    return bufferInfo;
                }
                
                // 缓冲区太小，释放并创建新的
                FreeBuffer(bufferInfo);
            }
            
            // 创建新的缓冲区
            return CreateNewBuffer(key, data, usage);
        }

        /// <summary>
        /// 获取或创建索引缓冲区 - 带内存池优化
        /// </summary>
        public unsafe BufferInfo GetOrCreateIndexBuffer(uint[] indices, BufferUsageFlags usage = BufferUsageFlags.IndexBufferBit)
        {
            var key = new BufferKey(BufferType.Index, (uint)(indices.Length * sizeof(uint)), usage);
            
            if (_bufferPools.TryGetValue(key, out var pool) && pool.TryDequeue(out var bufferInfo))
            {
                _bufferReuseCount++;
                
                if (bufferInfo.Size >= (ulong)(indices.Length * sizeof(uint)))
                {
                    UpdateBufferData(bufferInfo, indices);
                    _activeBuffers[key] = bufferInfo;
                    return bufferInfo;
                }
                
                FreeBuffer(bufferInfo);
            }
            
            return CreateNewBuffer(key, indices, usage);
        }

        /// <summary>
        /// 获取或创建实例缓冲区 - 针对大规模实例化渲染优化
        /// </summary>
        public unsafe BufferInfo GetOrCreateInstanceBuffer<T>(T[] instances, BufferUsageFlags usage = BufferUsageFlags.VertexBufferBit) where T : unmanaged
        {
            var key = new BufferKey(BufferType.Instance, (uint)(instances.Length * sizeof(T)), usage);
            
            if (_bufferPools.TryGetValue(key, out var pool) && pool.TryDequeue(out var bufferInfo))
            {
                _bufferReuseCount++;
                
                if (bufferInfo.Size >= (ulong)(instances.Length * sizeof(T)))
                {
                    UpdateBufferData(bufferInfo, instances);
                    _activeBuffers[key] = bufferInfo;
                    return bufferInfo;
                }
                
                FreeBuffer(bufferInfo);
            }
            
            return CreateNewBuffer(key, instances, usage);
        }

        /// <summary>
        /// 创建新的GPU缓冲区
        /// </summary>
        private unsafe BufferInfo CreateNewBuffer<T>(BufferKey key, T[] data, BufferUsageFlags usage) where T : unmanaged
        {
            var size = (ulong)(data.Length * sizeof(T));
            
            // 内存检查
            if (_totalAllocatedMemory + (long)size > MAX_TOTAL_MEMORY)
            {
                // 尝试清理未使用的缓冲区
                CleanupUnusedBuffers();
            }
            
            if (_totalAllocatedMemory + (long)size > MAX_TOTAL_MEMORY)
            {
                throw new InvalidOperationException($"GPU内存不足：请求 {size} 字节，已使用 {_totalAllocatedMemory} 字节");
            }
            
            var bufferInfo = CreateBuffer(size, usage | BufferUsageFlags.TransferDstBit);
            UpdateBufferData(bufferInfo, data);
            _activeBuffers[key] = bufferInfo;
            _totalAllocatedMemory += (long)size;
            
            return bufferInfo;
        }

        /// <summary.summary>
        /// 创建GPU缓冲区
        /// </summary>
        private unsafe BufferInfo CreateBuffer(ulong size, BufferUsageFlags usage)
        {
            var bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = usage,
                SharingMode = SharingMode.Exclusive
            };

            Silk.NET.Vulkan.Buffer buffer;
            if (_vk.CreateBuffer(_device, ref bufferInfo, null, out buffer) != Result.Success)
            {
                throw new InvalidOperationException("创建缓冲区失败");
            }

            MemoryRequirements memRequirements;
            _vk.GetBufferMemoryRequirements(_device, buffer, out memRequirements);

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size
            };

            PhysicalDeviceMemoryProperties memProperties;
            _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out memProperties);

            for (int i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                if ((memRequirements.MemoryTypeBits & (1u << i)) != 0 &&
                    (memProperties.MemoryTypes[i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) != 0)
                {
                    allocInfo.MemoryTypeIndex = (uint)i;
                    break;
                }
            }

            DeviceMemory memory;
            if (_vk.AllocateMemory(_device, ref allocInfo, null, out memory) != Result.Success)
            {
                throw new InvalidOperationException("分配内存失败");
            }

            _vk.BindBufferMemory(_device, buffer, memory, 0);

            return new BufferInfo
            {
                Buffer = buffer,
                Memory = memory,
                Size = size,
                UsageFlags = usage,
                MemoryFlags = MemoryPropertyFlags.DeviceLocalBit
            };
        }

        /// <summary>
        /// 更新缓冲区数据 - 使用staging buffer优化
        /// </summary>
        private unsafe void UpdateBufferData<T>(BufferInfo bufferInfo, T[] data) where T : unmanaged
        {
            var size = (ulong)(data.Length * sizeof(T));
            
            // 使用staging buffer进行高效数据传输
            var stagingBuffer = CreateStagingBuffer(size);
            
            // 映射内存并复制数据
            void* mappedData;
            _vk.MapMemory(_device, stagingBuffer.Memory, 0, size, 0, &mappedData);
            fixed (void* dataPtr = data)
            {
                System.Buffer.MemoryCopy(dataPtr, mappedData, (long)size, (long)size);
            }
            _vk.UnmapMemory(_device, stagingBuffer.Memory);
            
            // 执行GPU到GPU的数据传输
            CopyBuffer(stagingBuffer, bufferInfo, size);
            
            // 清理staging buffer
            FreeBuffer(stagingBuffer);
            
            _totalUsedMemory += (long)size;
        }

        /// <summary>
        /// 创建staging缓冲区
        /// </summary>
        private unsafe BufferInfo CreateStagingBuffer(ulong size)
        {
            return CreateBuffer(size, BufferUsageFlags.TransferSrcBit);
        }

        /// <summary>
        /// 复制缓冲区数据
        /// </summary>
        private unsafe void CopyBuffer(BufferInfo src, BufferInfo dst, ulong size)
        {
            // 简化的缓冲区复制 - 实际实现需要命令缓冲区
            // 这里只是占位符实现
        }

        /// <summary>
        /// 批量创建顶点缓冲区 - 针对大量音符优化
        /// </summary>
        public unsafe BufferInfo[] BatchCreateVertexBuffers<T>(T[][] dataArrays, BufferUsageFlags usage = BufferUsageFlags.VertexBufferBit) where T : unmanaged
        {
            var results = new BufferInfo[dataArrays.Length];
            
            // 并行创建缓冲区（如果数据量足够大）
            if (dataArrays.Length > 100)
            {
                Parallel.For(0, dataArrays.Length, i =>
                {
                    results[i] = GetOrCreateVertexBuffer(dataArrays[i], usage);
                });
            }
            else
            {
                for (int i = 0; i < dataArrays.Length; i++)
                {
                    results[i] = GetOrCreateVertexBuffer(dataArrays[i], usage);
                }
            }
            
            return results;
        }

        /// <summary>
        /// 释放缓冲区回到内存池
        /// </summary>
        public unsafe void ReturnBuffer(BufferInfo bufferInfo)
        {
            var key = new BufferKey(
                GetBufferTypeFromUsage(bufferInfo.UsageFlags),
                (uint)bufferInfo.Size,
                bufferInfo.UsageFlags);
            
            if (_bufferPools.TryGetValue(key, out var pool))
            {
                if (pool.Count < MAX_POOL_SIZE_PER_TYPE)
                {
                    pool.Enqueue(bufferInfo);
                    return;
                }
            }
            else
            {
                var newPool = new ConcurrentQueue<BufferInfo>();
                newPool.Enqueue(bufferInfo);
                _bufferPools[key] = newPool;
                return;
            }
            
            // 池已满，释放缓冲区
            FreeBuffer(bufferInfo);
            _totalAllocatedMemory -= (long)bufferInfo.Size;
        }

        /// <summary>
        /// 释放缓冲区
        /// </summary>
        private unsafe void FreeBuffer(BufferInfo bufferInfo)
        {
            if (bufferInfo.Buffer.Handle != 0)
            {
                _vk.DestroyBuffer(_device, bufferInfo.Buffer, null);
            }
            
            if (bufferInfo.Memory.Handle != 0)
            {
                _vk.FreeMemory(_device, bufferInfo.Memory, null);
            }
        }

        /// <summary>
        /// 清理未使用的缓冲区
        /// </summary>
        public void CleanupUnusedBuffers()
        {
            foreach (var kvp in _bufferPools)
            {
                while (kvp.Value.TryDequeue(out var bufferInfo))
                {
                    FreeBuffer(bufferInfo);
                    _totalAllocatedMemory -= (long)bufferInfo.Size;
                }
            }
            _bufferPools.Clear();
        }

        /// <summary>
        /// 获取内存使用统计
        /// </summary>
        public BufferStats GetStats()
        {
            return new BufferStats
            {
                TotalAllocatedMemory = _totalAllocatedMemory,
                TotalUsedMemory = _totalUsedMemory,
                BufferReuseCount = _bufferReuseCount,
                ActiveBufferCount = _activeBuffers.Count,
                PoolCount = _bufferPools.Count
            };
        }

        /// <summary>
        /// 获取缓冲区类型从使用标志
        /// </summary>
        private BufferType GetBufferTypeFromUsage(BufferUsageFlags usage)
        {
            if (usage.HasFlag(BufferUsageFlags.VertexBufferBit))
                return BufferType.Vertex;
            if (usage.HasFlag(BufferUsageFlags.IndexBufferBit))
                return BufferType.Index;
            if (usage.HasFlag(BufferUsageFlags.UniformBufferBit))
                return BufferType.Uniform;
            return BufferType.Generic;
        }

        public void Dispose()
        {
            CleanupUnusedBuffers();
        }
    }

    /// <summary>
    /// 缓冲区信息结构体
    /// </summary>
    public struct BufferInfo
    {
        public Silk.NET.Vulkan.Buffer Buffer;
        public DeviceMemory Memory;
        public ulong Size;
        public BufferUsageFlags UsageFlags;
        public MemoryPropertyFlags MemoryFlags;
    }

    /// <summary>
    /// 缓冲区类型枚举
    /// </summary>
    public enum BufferType
    {
        Vertex,
        Index,
        Uniform,
        Instance,
        Generic
    }

    /// <summary>
    /// 缓冲区键 - 用于内存池索引
    /// </summary>
    public readonly struct BufferKey : IEquatable<BufferKey>
    {
        public readonly BufferType Type;
        public readonly uint Size;
        public readonly BufferUsageFlags Usage;

        public BufferKey(BufferType type, uint size, BufferUsageFlags usage)
        {
            Type = type;
            Size = size;
            Usage = usage;
        }

        public bool Equals(BufferKey other)
        {
            return Type == other.Type && Size == other.Size && Usage == other.Usage;
        }

        public override bool Equals(object? obj)
        {
            return obj is BufferKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Type, Size, (int)Usage);
        }
    }

    /// <summary>
    /// 缓冲区统计信息
    /// </summary>
    public struct BufferStats
    {
        public long TotalAllocatedMemory;
        public long TotalUsedMemory;
        public int BufferReuseCount;
        public int ActiveBufferCount;
        public int PoolCount;
    }
}