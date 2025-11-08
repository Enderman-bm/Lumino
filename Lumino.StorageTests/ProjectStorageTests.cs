using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lumino.Models.Music;
using Lumino.Services.Implementation;
using Lumino.Services.Interfaces;
using Xunit;

namespace Lumino.StorageTests
{
    public class ProjectStorageTests
    {
        [Fact]
        public async Task SaveAndLoadProject_Roundtrip_PreservesNotesAndMetadata()
        {
            var temp = Path.GetTempPath();
            var file = Path.Combine(temp, $"test_project_{Guid.NewGuid()}.lmpf");

            try
            {
                var service = new ProjectStorageService();

                var notes = new List<Note>
                {
                    new Note { Pitch = 60, Velocity = 100, StartPosition = MusicalFraction.FromDouble(0), Duration = MusicalFraction.FromDouble(1.0), TrackIndex = 0 },
                    new Note { Pitch = 64, Velocity = 90, StartPosition = MusicalFraction.FromDouble(1.0), Duration = MusicalFraction.FromDouble(0.5), TrackIndex = 1 }
                };

                var metadata = new ProjectMetadata
                {
                    Title = "UnitTest",
                    Artist = "Tester",
                    Tempo = 123.0,
                    Created = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };

                metadata.Tracks.Add(new TrackMetadata { TrackNumber = 0, TrackName = "Conductor", IsConductorTrack = true });
                metadata.Tracks.Add(new TrackMetadata { TrackNumber = 1, TrackName = "Piano", MidiChannel = 0, Instrument = "Piano", ColorTag = "#FF0000" });

                var ok = await service.SaveProjectAsync(file, notes, metadata, System.Threading.CancellationToken.None);
                Assert.True(ok, "SaveProjectAsync should return true");

                var (loadedNotes, loadedMetadata) = await service.LoadProjectAsync(file, System.Threading.CancellationToken.None);

                Assert.NotNull(loadedNotes);
                Assert.NotNull(loadedMetadata);

                var ln = new List<Note>(loadedNotes);
                Assert.Equal(notes.Count, ln.Count);
                Assert.Equal(metadata.Title, loadedMetadata.Title);
                Assert.Equal(metadata.Tracks.Count, loadedMetadata.Tracks.Count);
                Assert.Equal(metadata.Tracks[1].TrackName, loadedMetadata.Tracks[1].TrackName);
                Assert.Equal(metadata.Tracks[1].ColorTag, loadedMetadata.Tracks[1].ColorTag);
            }
            finally
            {
                try { if (File.Exists(file)) File.Delete(file); } catch { }
            }
        }

        [Fact]
        public async Task SaveProject_CancelDuringWrite_OriginalFileUnchangedAndTempDeleted()
        {
            var temp = Path.GetTempPath();
            var file = Path.Combine(temp, $"test_project_cancel_{Guid.NewGuid()}.lmpf");

            // 创建一个已有的原始文件，以便在保存被取消时验证其未被覆盖
            var originalContent = System.Text.Encoding.UTF8.GetBytes("ORIGINAL_CONTENT");
            try
            {
                File.WriteAllBytes(file, originalContent);

                var service = new ProjectStorageService();

                // 创建大量音符以使写入过程耗时，便于在写入期间触发取消
                var notes = new List<Note>();
                for (int i = 0; i < 20000; i++)
                {
                    notes.Add(new Note { Pitch = (byte)(i % 128), Velocity = 100, StartPosition = MusicalFraction.FromDouble(i * 0.001), Duration = MusicalFraction.FromDouble(0.125), TrackIndex = i % 4 });
                }

                var metadata = new ProjectMetadata
                {
                    Title = "CancelTest",
                    Artist = "Tester",
                    Tempo = 120.0,
                    Created = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };

                // 使用可取消的令牌，启动保存并在短延迟后取消
                using var cts = new System.Threading.CancellationTokenSource();

                var saveTask = service.SaveProjectAsync(file, notes, metadata, cts.Token);

                // 轻微延迟，然后取消；写入应该正在进行中
                await Task.Delay(50);
                cts.Cancel();

                var result = await saveTask;

                // 保存在被取消后应该返回 false（服务捕获异常并返回false）
                Assert.False(result, "SaveProjectAsync should return false when cancelled");

                // 原始文件应保持不变
                var finalBytes = File.ReadAllBytes(file);
                Assert.Equal(originalContent, finalBytes);

                // 没有残留的临时文件：临时文件以 "<originalFileName>.tmp." 开头
                var dir = Path.GetDirectoryName(file) ?? Path.GetTempPath();
                var prefix = Path.GetFileName(file) + ".tmp.";
                var leftoverTemps = Directory.EnumerateFiles(dir).Where(p => Path.GetFileName(p).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
                Assert.Empty(leftoverTemps);
            }
            finally
            {
                try { if (File.Exists(file)) File.Delete(file); } catch { }
            }
        }
    }
}
