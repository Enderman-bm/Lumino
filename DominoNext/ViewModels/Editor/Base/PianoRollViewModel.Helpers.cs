using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.Models.Project;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的辅助方法和工具方法
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 时间格式化方法
        /// <summary>
        /// 格式化时间（转换为分钟:秒:毫秒格式）
        /// </summary>
        private string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            
            var minutes = (int)(seconds / 60);
            var remainingSeconds = seconds % 60;
            var milliseconds = (int)((remainingSeconds - (int)remainingSeconds) * 1000);
            
            return $"{minutes:D2}:{(int)remainingSeconds:D2}:{milliseconds:D3}";
        }

        /// <summary>
        /// 格式化时间（转换为拍数格式）
        /// </summary>
        private string FormatTimeInBeats(double beats)
        {
            if (beats < 0) beats = 0;
            
            var measures = (int)(beats / BeatsPerMeasure);
            var remainingBeats = beats % BeatsPerMeasure;
            
            return $"{measures + 1}:{remainingBeats:F2}";
        }

        /// <summary>
        /// 将时间转换为像素位置
        /// </summary>
        private double TimeToPixel(double time)
        {
            return time * TimeToPixelScale;
        }

        /// <summary>
        /// 将像素位置转换为时间
        /// </summary>
        private double PixelToTime(double pixel)
        {
            return pixel / TimeToPixelScale;
        }
        #endregion

        #region 音高相关方法
        /// <summary>
        /// 将音高转换为显示名称
        /// </summary>
        public string GetNoteName(int pitch)
        {
            if (pitch < 0 || pitch > 127) return "Invalid";
            
            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var octave = (pitch / 12) - 1;
            var note = pitch % 12;
            
            return $"{noteNames[note]}{octave}";
        }

        /// <summary>
        /// 将显示名称转换为音高
        /// </summary>
        private int GetNotePitch(string noteName)
        {
            if (string.IsNullOrEmpty(noteName)) return -1;
            
            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            
            // 解析音符名称和八度
            var notePart = "";
            var octavePart = "";
            
            for (int i = 0; i < noteName.Length; i++)
            {
                if (char.IsLetter(noteName[i]) || noteName[i] == '#')
                {
                    notePart += noteName[i];
                }
                else if (char.IsDigit(noteName[i]))
                {
                    octavePart += noteName[i];
                }
            }
            
            var noteIndex = Array.IndexOf(noteNames, notePart);
            if (noteIndex == -1 || !int.TryParse(octavePart, out var octave)) return -1;
            
            return (octave + 1) * 12 + noteIndex;
        }

        /// <summary>
        /// 将音高转换为像素位置
        /// </summary>
        private double PitchToPixel(int pitch)
        {
            return (127 - pitch) * Calculations.KeyHeight;
        }

        /// <summary>
        /// 将像素位置转换为音高
        /// </summary>
        private int PixelToPitch(double pixel)
        {
            return 127 - (int)(pixel / Calculations.KeyHeight);
        }
        #endregion

        #region 颜色工具方法
        /// <summary>
        /// 生成音轨颜色
        /// </summary>
        private Color GenerateTrackColor(int trackIndex)
        {
            var hue = (trackIndex * 137.5) % 360; // 使用黄金角分布颜色
            var saturation = 0.7;
            var lightness = 0.5;
            
            return HslToRgb(hue, saturation, lightness);
        }

        /// <summary>
        /// 将颜色转换为十六进制字符串
        /// </summary>
        private string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// 将十六进制字符串转换为颜色
        /// </summary>
        private Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Colors.White;
            
            try
            {
                hex = hex.Replace("#", "");
                if (hex.Length == 6)
                {
                    var r = Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromRgb(r, g, b);
                }
            }
            catch
            {
                // 忽略转换错误，返回默认颜色
            }
            
            return Colors.White;
        }

        /// <summary>
        /// 获取音符颜色（基于力度）
        /// </summary>
        private Color GetNoteColorByVelocity(int velocity)
        {
            var intensity = velocity / 127.0;
            var hue = 240 - (int)(intensity * 240); // 从蓝色到红色
            var saturation = 0.8;
            var lightness = 0.3 + (intensity * 0.4); // 根据力度调整亮度
            
            return HslToRgb(hue, saturation, lightness);
        }
        #endregion

        #region 数学工具方法
        /// <summary>
        /// 限制值在指定范围内
        /// </summary>
        private T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;
            return value;
        }

        /// <summary>
        /// 线性插值
        /// </summary>
        private double Lerp(double start, double end, double t)
        {
            return start + (end - start) * t;
        }

        /// <summary>
        /// 平滑步进函数
        /// </summary>
        private double SmoothStep(double edge0, double edge1, double x)
        {
            var t = Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>
        /// 四舍五入到指定精度
        /// </summary>
        private double RoundToPrecision(double value, double precision)
        {
            return Math.Round(value / precision) * precision;
        }
        #endregion

        #region 文件工具方法
        /// <summary>
        /// 获取安全的文件名
        /// </summary>
        private string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "untitled";
            
            // 移除无效字符
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            
            // 限制长度
            if (fileName.Length > 200)
            {
                fileName = fileName.Substring(0, 200);
            }
            
            return fileName;
        }

        /// <summary>
        /// 检查文件是否存在且可访问
        /// </summary>
        private bool IsFileAccessible(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                
                using (File.OpenRead(filePath))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 创建备份文件路径
        /// </summary>
        private string CreateBackupFilePath(string originalFilePath)
        {
            if (string.IsNullOrEmpty(originalFilePath)) return string.Empty;
            
            var directory = Path.GetDirectoryName(originalFilePath);
            var fileName = Path.GetFileNameWithoutExtension(originalFilePath);
            var extension = Path.GetExtension(originalFilePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            return Path.Combine(directory ?? string.Empty, $"{fileName}_backup_{timestamp}{extension}");
        }
        #endregion

        #region 集合工具方法
        /// <summary>
        /// 安全地从集合中获取元素
        /// </summary>
        private T? SafeGet<T>(IList<T> list, int index) where T : class
        {
            if (list == null || index < 0 || index >= list.Count) return null;
            return list[index];
        }

        /// <summary>
        /// 安全地从集合中移除元素
        /// </summary>
        private bool SafeRemove<T>(ICollection<T> collection, T item) where T : class
        {
            if (collection == null || item == null) return false;
            if (!collection.Contains(item)) return false;
            
            return collection.Remove(item);
        }

        /// <summary>
        /// 交换集合中的两个元素
        /// </summary>
        private bool SwapElements<T>(IList<T> list, int index1, int index2)
        {
            if (list == null || index1 < 0 || index2 < 0 || 
                index1 >= list.Count || index2 >= list.Count) return false;
            
            if (index1 == index2) return true;
            
            var temp = list[index1];
            list[index1] = list[index2];
            list[index2] = temp;
            
            return true;
        }

        /// <summary>
        /// HSL颜色转换为RGB颜色
        /// </summary>
        private Color HslToRgb(double hue, double saturation, double lightness)
        {
            // 将色相转换为0-1范围
            hue = hue / 360.0;
            
            double r, g, b;
            
            if (saturation == 0)
            {
                r = g = b = lightness; // 灰色
            }
            else
            {
                var q = lightness < 0.5 ? lightness * (1 + saturation) : lightness + saturation - lightness * saturation;
                var p = 2 * lightness - q;
                
                r = HueToRgb(p, q, hue + 1.0 / 3.0);
                g = HueToRgb(p, q, hue);
                b = HueToRgb(p, q, hue - 1.0 / 3.0);
            }
            
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
        
        /// <summary>
        /// 色相转换为RGB分量
        /// </summary>
        private double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            
            return p;
        }

        /// <summary>
        /// 将元素移动到集合中的新位置
        /// </summary>
        private bool MoveElement<T>(IList<T> list, T element, int newIndex)
        {
            if (list == null || element == null) return false;
            
            var currentIndex = list.IndexOf(element);
            if (currentIndex == -1) return false;
            
            if (newIndex < 0 || newIndex >= list.Count) return false;
            if (currentIndex == newIndex) return true;
            
            list.RemoveAt(currentIndex);
            list.Insert(newIndex, element);
            
            return true;
        }
        #endregion

        #region 验证方法
        /// <summary>
        /// 验证音符数据
        /// </summary>
        private bool ValidateNote(Note note)
        {
            if (note == null) return false;
            
            // 验证音高范围
            if (note.Pitch < 0 || note.Pitch > 127) return false;
            
            // 验证力度范围
            if (note.Velocity < 1 || note.Velocity > 127) return false;
            
            // 验证时长
            if (note.Duration.Numerator <= 0) return false;
            
            // 验证开始时间
            if (note.StartPosition.Numerator < 0) return false;
            
            // 验证音轨索引
            if (note.TrackIndex < 0 || note.TrackIndex >= Tracks.Count) return false;
            
            return true;
        }

        /// <summary>
        /// 验证音轨数据
        /// </summary>
        private bool ValidateTrack(Track track)
        {
            if (track == null) return false;
            
            // 验证音轨名称
            if (string.IsNullOrWhiteSpace(track.Name)) return false;
            
            // 验证音轨颜色
            if (string.IsNullOrWhiteSpace(track.Color)) return false;
            
            // 验证MIDI通道
            if (track.Channel < 0 || track.Channel > 15) return false;
            
            return true;
        }

        /// <summary>
        /// 验证项目数据
        /// </summary>
        private bool ValidateProject(Project project)
        {
            if (project == null) return false;
            
            // 验证项目名称
            if (string.IsNullOrWhiteSpace(project.Name)) return false;
            
            // 验证拍号
            if (project.BeatsPerMeasure <= 0 || project.BeatValue <= 0) return false;
            
            // 验证速度
            if (project.Tempo <= 0) return false;
            
            return true;
        }
        #endregion

        #region 性能优化方法
        /// <summary>
        /// 批量更新集合（暂停通知）
        /// </summary>
        private void BatchUpdate<T>(ICollection<T> collection, Action updateAction)
        {
            if (collection == null || updateAction == null) return;
            
            try
            {
                // TODO: 如果集合支持暂停通知，在这里实现
                updateAction();
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出，确保操作完成
                System.Diagnostics.Debug.WriteLine($"批量更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 延迟执行操作
        /// </summary>
        private async Task DelayExecute(Action action, int delayMilliseconds)
        {
            if (action == null) return;
            
            await Task.Delay(delayMilliseconds);
            
            try
            {
                action();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"延迟执行失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 节流执行操作
        /// </summary>
        private void ThrottleExecute(Action action, ref DateTime lastExecuteTime, int throttleMilliseconds)
        {
            if (action == null) return;
            
            var now = DateTime.Now;
            var timeSinceLastExecute = (now - lastExecuteTime).TotalMilliseconds;
            
            if (timeSinceLastExecute >= throttleMilliseconds)
            {
                lastExecuteTime = now;
                action();
            }
        }
        #endregion}

        /// <summary>
        /// 设置MIDI文件时长（以四分音符为单位）
        /// </summary>
        /// <param name="durationInQuarterNotes">MIDI文件时长（四分音符数量）</param>
        public void SetMidiFileDuration(double durationInQuarterNotes)
        {
            if (durationInQuarterNotes <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"设置MIDI文件时长失败: 时长必须为正数，当前值: {durationInQuarterNotes}");
                return;
            }

            MidiFileDuration = durationInQuarterNotes;
            System.Diagnostics.Debug.WriteLine($"设置MIDI文件时长: {durationInQuarterNotes:F2} 四分音符");
        }
    }
}