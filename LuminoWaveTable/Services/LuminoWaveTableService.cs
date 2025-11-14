using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LuminoWaveTable.Core;
using LuminoWaveTable.Interfaces;
using LuminoWaveTable.Models;
using LuminoWaveTable.Native;
using EnderDebugger;

namespace LuminoWaveTable.Services
{
	/// <summary>
	/// 最小且完整的 LuminoWaveTable 服务实现。
	/// 目的是提供与 ILuminoWaveTableService 签名一致的实现，保证项目可编译。
	/// 真实逻辑基于现有模块，保持轻量并可扩展。
	/// </summary>
	public class LuminoWaveTableService : ILuminoWaveTableService
	{
		private readonly EnderLogger _logger;
		private readonly MidiMessageProcessor _messageProcessor;
		private readonly WaveTableManager _waveTableManager;
		private readonly PerformanceMonitor _performanceMonitor;

		private IntPtr _midiOutHandle;
		private bool _isInitialized;
		private bool _isDisposed;
		private bool _isPlaying;
		private int _currentDeviceId;
		private string _currentWaveTableId;
		private readonly object _lockObject = new object();

		private readonly Timer _performanceTimer;
		private readonly Timer _deviceScanTimer;
		private readonly List<LuminoMidiDeviceInfo> _cachedDevices;
		private DateTime _lastDeviceScan;
		private readonly TimeSpan _deviceScanInterval = TimeSpan.FromSeconds(30);

		public string ServiceName => "LuminoWaveTable";
		public string ServiceVersion => "1.0.0";
		public bool IsInitialized => _isInitialized;
		public bool IsPlaying => _isPlaying;

		public int CurrentDeviceId
		{
			get => _currentDeviceId;
			set
			{
				if (value != _currentDeviceId)
				{
					var oldDevice = _currentDeviceId;
					_currentDeviceId = value;
					_ = Task.Run(() => SwitchDeviceAsync(oldDevice, value));
				}
			}
		}

		public string CurrentWaveTableId
		{
			get => _currentWaveTableId;
			set
			{
				if (value != _currentWaveTableId)
				{
					var old = _currentWaveTableId;
					_currentWaveTableId = value;
					_ = Task.Run(() => SwitchWaveTableAsync(old, value));
				}
			}
		}

		public WaveTablePerformanceInfo PerformanceInfo => _performanceMonitor?.GetCurrentPerformance() ?? new WaveTablePerformanceInfo();

		public event EventHandler<WaveTableChangedEventArgs>? WaveTableChanged;
		public event EventHandler<DeviceChangedEventArgs>? DeviceChanged;
		public event EventHandler<PerformanceUpdatedEventArgs>? PerformanceUpdated;

		public LuminoWaveTableService()
		{
			_logger = EnderLogger.Instance;
			_messageProcessor = new MidiMessageProcessor();
			_waveTableManager = new WaveTableManager(GetWaveTablesDirectory());
			_performanceMonitor = new PerformanceMonitor();
			_cachedDevices = new List<LuminoMidiDeviceInfo>();

			_midiOutHandle = IntPtr.Zero;
			_currentDeviceId = -1;
			_currentWaveTableId = "lumino_gm_complete";

			_performanceTimer = new Timer(_ => UpdatePerformanceStats(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
			_deviceScanTimer = new Timer(_ => _ = ScanDevicesAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

			_logger.Info("LuminoWaveTableService", "Lumino播表服务已创建（最小实现）");
		}

		private string GetWaveTablesDirectory()
		{
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			var path = Path.Combine(appData, "Lumino", "WaveTables");
			if (!Directory.Exists(path)) Directory.CreateDirectory(path);
			return path;
		}

		public async Task InitializeAsync()
		{
			if (_isInitialized) return;
			try
			{
				await _waveTableManager.LoadCustomWaveTablesAsync();
				_waveTableManager.SetCurrentWaveTable(_currentWaveTableId);
				await ScanDevicesAsync();
				if (_cachedDevices.Count > 0)
				{
					var d = _cachedDevices.FirstOrDefault(x => x.IsDefault) ?? _cachedDevices.First();
					await OpenDeviceAsync(d.DeviceId);
				}
				_isInitialized = true;
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"Initialize failed: {ex.Message}");
				throw;
			}
		}

		public Task<List<LuminoMidiDeviceInfo>> GetMidiDevicesAsync()
		{
			lock (_cachedDevices)
			{
				return Task.FromResult(new List<LuminoMidiDeviceInfo>(_cachedDevices));
			}
		}

		public Task<List<LuminoWaveTableInfo>> GetWaveTablesAsync()
		{
			var list = _waveTableManager.GetAllWaveTables();
			return Task.FromResult(list);
		}

		public async Task PlayNoteAsync(int midiNote, int velocity = 100, int durationMs = 200, int channel = 0)
		{
			if (!_isInitialized || _isDisposed) return;
			if (_midiOutHandle == IntPtr.Zero || !WinmmNative.IsMidiOutHandleValid(_midiOutHandle)) return;

			try
			{
				var msg = _messageProcessor.CreateNoteOn(midiNote, velocity, channel);
				var res = WinmmNative.midiOutShortMsg(_midiOutHandle, msg);
				if (res != WinmmNative.MMSYSERR_NOERROR)
				{
					_logger.Error("LuminoWaveTableService", $"midiOutShortMsg failed: {WinmmNative.GetMidiErrorText(res)}");
					return;
				}

				_performanceMonitor?.RecordNotePlayed();
				_isPlaying = true;

				if (durationMs > 0)
				{
					_ = Task.Run(async () =>
					{
						await Task.Delay(durationMs);
						await StopNoteAsync(midiNote, channel);
					});
				}
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"PlayNoteAsync failed: {ex.Message}");
			}
		}

		public async Task StopNoteAsync(int midiNote, int channel = 0)
		{
			if (!_isInitialized || _isDisposed) return;
			if (_midiOutHandle == IntPtr.Zero || !WinmmNative.IsMidiOutHandleValid(_midiOutHandle)) return;

			try
			{
				var msg = _messageProcessor.CreateNoteOff(midiNote, channel);
				WinmmNative.midiOutShortMsg(_midiOutHandle, msg);
				var active = _messageProcessor.GetActiveNotes();
				_isPlaying = active.Count > 0;
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"StopNoteAsync failed: {ex.Message}");
			}
		}

		public async Task ChangeInstrumentAsync(int instrumentId, int channel = 0)
		{
			if (!_isInitialized || _isDisposed) return;
			if (_midiOutHandle == IntPtr.Zero || !WinmmNative.IsMidiOutHandleValid(_midiOutHandle)) return;

			try
			{
				var msg = _messageProcessor.CreateProgramChange(instrumentId, channel);
				WinmmNative.midiOutShortMsg(_midiOutHandle, msg);
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"ChangeInstrumentAsync failed: {ex.Message}");
			}
		}

		public async Task SendMidiMessageAsync(uint message)
		{
			if (!_isInitialized || _isDisposed) return;
			if (_midiOutHandle == IntPtr.Zero || !WinmmNative.IsMidiOutHandleValid(_midiOutHandle)) return;

			try
			{
				if (!_messageProcessor.ValidateMidiMessage(message))
				{
					_logger.Warn("LuminoWaveTableService", $"Invalid MIDI message: 0x{message:X8}");
					return;
				}
				WinmmNative.midiOutShortMsg(_midiOutHandle, message);
				_performanceMonitor?.RecordMessageSent();
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"SendMidiMessageAsync failed: {ex.Message}");
			}
		}

		public async Task SetWaveTableAsync(string waveTableId)
		{
			if (!_isInitialized || _isDisposed) return;
			try
			{
				var ok = _waveTableManager.SetCurrentWaveTable(waveTableId);
				if (ok) _currentWaveTableId = waveTableId;
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"SetWaveTableAsync failed: {ex.Message}");
			}
		}

		public Task<LuminoWaveTableInfo?> GetCurrentWaveTableAsync()
		{
			var wt = _waveTableManager.GetCurrentWaveTable();
			return Task.FromResult(wt);
		}

		public async Task ResetMidiStreamAsync()
		{
			if (!_isInitialized || _isDisposed) return;
			if (_midiOutHandle == IntPtr.Zero || !WinmmNative.IsMidiOutHandleValid(_midiOutHandle)) return;

			try
			{
				var msgs = _messageProcessor.CreateAllNotesOffAllChannels();
				foreach (var m in msgs) WinmmNative.midiOutShortMsg(_midiOutHandle, m);
				WinmmNative.midiOutReset(_midiOutHandle);
				_messageProcessor.Reset();
				_isPlaying = false;
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"ResetMidiStreamAsync failed: {ex.Message}");
			}
		}

		public async Task CleanupAsync()
		{
			try
			{
				if (_isPlaying) await ResetMidiStreamAsync();
				await CloseDeviceAsync();
				_performanceTimer?.Dispose();
				_deviceScanTimer?.Dispose();
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"CleanupAsync failed: {ex.Message}");
			}
		}

		public Task<WaveTablePerformanceInfo> GetPerformanceInfoAsync()
		{
			var info = _performanceMonitor?.GetCurrentPerformance() ?? new WaveTablePerformanceInfo();
			return Task.FromResult(info);
		}

		public async Task OptimizePerformanceAsync()
		{
			try
			{
				_performanceMonitor?.OptimizePerformance();
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"OptimizePerformanceAsync failed: {ex.Message}");
			}
		}

		public List<WaveTableEngineInfo> GetAvailableEngines()
		{
			return new List<WaveTableEngineInfo>
			{
				new WaveTableEngineInfo { Id = "default", Name = "Default", Provider = "builtin", IsAvailable = true }
			};
		}

		public void Dispose()
		{
			if (_isDisposed) return;
			try
			{
				CleanupAsync().Wait(TimeSpan.FromSeconds(2));
			}
			catch { }
			_isDisposed = true;
		}

		#region Private helpers

		private async Task ScanDevicesAsync()
		{
			try
			{
				var now = DateTime.Now;
				if (now - _lastDeviceScan < _deviceScanInterval) return;
				_lastDeviceScan = now;

				var devices = new List<LuminoMidiDeviceInfo>();
				var winmm = WinmmNative.GetMidiOutDevices();
				for (int i = 0; i < winmm.Count; i++)
				{
					var d = winmm[i];
					devices.Add(new LuminoMidiDeviceInfo
					{
						DeviceId = i,
						Name = d.szPname,
						IsDefault = i == 0,
						Technology = d.wTechnology,
						Voices = d.wVoices,
						Notes = d.wNotes,
						ChannelMask = d.wChannelMask,
						Support = (uint)d.dwSupport,
						IsAvailable = true
					});
				}

				lock (_cachedDevices)
				{
					_cachedDevices.Clear();
					_cachedDevices.AddRange(devices);
				}

				DeviceChanged?.Invoke(this, new DeviceChangedEventArgs { OldDeviceId = -1, NewDeviceId = _currentDeviceId, NewDevice = _cachedDevices.FirstOrDefault(d => d.DeviceId == _currentDeviceId) });
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"ScanDevicesAsync failed: {ex.Message}");
			}
		}

		private async Task<bool> OpenDeviceAsync(int deviceId)
		{
			try
			{
				if (_midiOutHandle != IntPtr.Zero) await CloseDeviceAsync();
				var res = WinmmNative.midiOutOpen(ref _midiOutHandle, (uint)deviceId, IntPtr.Zero, IntPtr.Zero, 0);
				if (res != WinmmNative.MMSYSERR_NOERROR)
				{
					_logger.Error("LuminoWaveTableService", $"midiOutOpen failed: {WinmmNative.GetMidiErrorText(res)}");
					return false;
				}
				_currentDeviceId = deviceId;
				return true;
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"OpenDeviceAsync failed: {ex.Message}");
				return false;
			}
		}

		private async Task CloseDeviceAsync()
		{
			try
			{
				if (_midiOutHandle != IntPtr.Zero && WinmmNative.IsMidiOutHandleValid(_midiOutHandle))
				{
					WinmmNative.midiOutReset(_midiOutHandle);
					WinmmNative.midiOutClose(_midiOutHandle);
				}
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"CloseDeviceAsync failed: {ex.Message}");
			}
			finally
			{
				_midiOutHandle = IntPtr.Zero;
				_currentDeviceId = -1;
			}
		}

		private async Task SwitchDeviceAsync(int oldDeviceId, int newDeviceId)
		{
			try
			{
				if (_isPlaying) await ResetMidiStreamAsync();
				var ok = await OpenDeviceAsync(newDeviceId);
				if (ok)
				{
					DeviceChanged?.Invoke(this, new DeviceChangedEventArgs { OldDeviceId = oldDeviceId, NewDeviceId = newDeviceId, NewDevice = _cachedDevices.FirstOrDefault(d => d.DeviceId == newDeviceId) });
				}
				else
				{
					_currentDeviceId = oldDeviceId;
				}
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"SwitchDeviceAsync failed: {ex.Message}");
			}
		}

		private async Task SwitchWaveTableAsync(string oldWaveTableId, string newWaveTableId)
		{
			try
			{
				var ok = _waveTableManager.SetCurrentWaveTable(newWaveTableId);
				if (ok)
				{
					WaveTableChanged?.Invoke(this, new WaveTableChangedEventArgs { OldWaveTableId = oldWaveTableId ?? string.Empty, NewWaveTableId = newWaveTableId ?? string.Empty, NewWaveTable = _waveTableManager.GetCurrentWaveTable() });
				}
				else
				{
					_currentWaveTableId = oldWaveTableId;
				}
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"SwitchWaveTableAsync failed: {ex.Message}");
			}
		}

		private void UpdatePerformanceStats()
		{
			try
			{
				var p = _performanceMonitor?.GetCurrentPerformance();
				if (p != null)
				{
					PerformanceUpdated?.Invoke(this, new PerformanceUpdatedEventArgs { PerformanceInfo = p });
				}
			}
			catch (Exception ex)
			{
				_logger.Error("LuminoWaveTableService", $"UpdatePerformanceStats failed: {ex.Message}");
			}
		}

		#endregion
	}
}