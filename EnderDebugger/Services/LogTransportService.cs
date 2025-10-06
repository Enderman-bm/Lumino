using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EnderDebugger.Services;

/// <summary>
/// 日志传输服务 - 使用命名管道进行进程间通信
/// 接收端(EnderDebugger)在独立进程中运行
/// </summary>
public class LogTransportService : IDisposable
{
    private const string PipeName = "LuminoEnderDebuggerPipe";
    private NamedPipeServerStream? _pipeServer;
    private StreamReader? _reader;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    
    public event Action<LogEntry>? LogReceived;
    
    /// <summary>
    /// 启动日志接收服务器
    /// </summary>
    public void StartServer()
    {
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLogsAsync(_cancellationTokenSource.Token));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LogTransportService] 启动服务器失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 异步接收日志数据
    /// </summary>
    private async Task ReceiveLogsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 创建命名管道服务器
                _pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte, // 使用 Byte 模式,兼容所有平台
                    PipeOptions.Asynchronous);
                
                Console.WriteLine("[LogTransportService] 等待客户端连接...");
                
                // 等待客户端连接
                await _pipeServer.WaitForConnectionAsync(cancellationToken);
                
                Console.WriteLine("[LogTransportService] 客户端已连接");
                
                _reader = new StreamReader(_pipeServer, Encoding.UTF8);
                
                // 持续读取日志数据
                while (!cancellationToken.IsCancellationRequested && _pipeServer.IsConnected)
                {
                    try
                    {
                        var line = await _reader.ReadLineAsync();
                        if (line == null)
                        {
                            Console.WriteLine("[LogTransportService] 客户端断开连接");
                            break;
                        }
                        
                        // 反序列化日志条目
                        var logEntry = JsonSerializer.Deserialize<LogEntry>(line);
                        if (logEntry != null)
                        {
                            // 触发日志接收事件
                            LogReceived?.Invoke(logEntry);
                        }
                    }
                    catch (IOException)
                    {
                        // 客户端断开连接
                        Console.WriteLine("[LogTransportService] 客户端连接中断");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LogTransportService] 读取日志失败: {ex.Message}");
                    }
                }
                
                // 清理当前连接
                _reader?.Dispose();
                _reader = null;
                _pipeServer?.Dispose();
                _pipeServer = null;
                
                // 如果未取消,继续等待新连接
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("[LogTransportService] 等待下一个客户端连接...");
                    await Task.Delay(100, cancellationToken); // 短暂延迟
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[LogTransportService] 接收任务已取消");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogTransportService] 接收循环错误: {ex.Message}");
                await Task.Delay(1000, cancellationToken); // 错误后延迟重试
            }
        }
        
        Console.WriteLine("[LogTransportService] 接收服务已停止");
    }
    
    public void Dispose()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _reader?.Dispose();
            _pipeServer?.Dispose();
            _receiveTask?.Wait(TimeSpan.FromSeconds(2));
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LogTransportService] 释放资源失败: {ex.Message}");
        }
    }
}

/// <summary>
/// 日志发送客户端 - 在 Lumino 进程中运行
/// </summary>
public class LogTransportClient : IDisposable
{
    private const string PipeName = "LuminoEnderDebuggerPipe";
    private NamedPipeClientStream? _pipeClient;
    private StreamWriter? _writer;
    private readonly System.Threading.Channels.Channel<LogEntry> _logQueue;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _sendTask;
    private bool _isConnected;
    
    public LogTransportClient()
    {
        // 使用 Channel 作为高性能异步队列
        _logQueue = System.Threading.Channels.Channel.CreateBounded<LogEntry>(
            new System.Threading.Channels.BoundedChannelOptions(10000)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest // 满时丢弃最旧的日志
            });
        
        _cancellationTokenSource = new CancellationTokenSource();
        _sendTask = Task.Run(() => SendLogsAsync(_cancellationTokenSource.Token));
    }
    
    /// <summary>
    /// 发送日志(非阻塞)
    /// </summary>
    public void SendLog(LogEntry logEntry)
    {
        // 尝试写入队列,不阻塞
        _logQueue.Writer.TryWrite(logEntry);
    }
    
    /// <summary>
    /// 异步发送日志到管道
    /// </summary>
    private async Task SendLogsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 如果未连接,尝试连接
                if (!_isConnected)
                {
                    await ConnectAsync(cancellationToken);
                }
                
                // 读取队列中的日志
                var logEntry = await _logQueue.Reader.ReadAsync(cancellationToken);
                
                if (_isConnected && _writer != null)
                {
                    try
                    {
                        // 序列化并发送
                        var json = JsonSerializer.Serialize(logEntry);
                        await _writer.WriteLineAsync(json);
                        await _writer.FlushAsync();
                    }
                    catch (IOException)
                    {
                        // 连接断开,标记为未连接
                        _isConnected = false;
                        DisconnectPipe();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogTransportClient] 发送日志失败: {ex.Message}");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
    
    /// <summary>
    /// 连接到管道服务器
    /// </summary>
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            _pipeClient = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            
            // 尝试连接,超时 500ms
            await _pipeClient.ConnectAsync(500, cancellationToken);
            
            _writer = new StreamWriter(_pipeClient, Encoding.UTF8)
            {
                AutoFlush = false // 手动刷新以提高性能
            };
            
            _isConnected = true;
            Console.WriteLine("[LogTransportClient] 已连接到日志服务器");
        }
        catch (TimeoutException)
        {
            // 连接超时,EnderDebugger 可能未运行
            _pipeClient?.Dispose();
            _pipeClient = null;
            await Task.Delay(2000, cancellationToken); // 等待后重试
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LogTransportClient] 连接失败: {ex.Message}");
            _pipeClient?.Dispose();
            _pipeClient = null;
            await Task.Delay(2000, cancellationToken);
        }
    }
    
    /// <summary>
    /// 断开管道连接
    /// </summary>
    private void DisconnectPipe()
    {
        try
        {
            _writer?.Dispose();
            _writer = null;
            _pipeClient?.Dispose();
            _pipeClient = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LogTransportClient] 断开连接失败: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _logQueue.Writer.Complete();
            _sendTask.Wait(TimeSpan.FromSeconds(2));
            DisconnectPipe();
            _cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LogTransportClient] 释放资源失败: {ex.Message}");
        }
    }
}
