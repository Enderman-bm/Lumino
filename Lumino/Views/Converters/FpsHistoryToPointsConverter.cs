using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// FPS历史数据到Polyline Points转换器
    /// 将FPS历史数据列表转换为用于绘制曲线的Points集合
    /// </summary>
    public class FpsHistoryToPointsConverter : IValueConverter
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly FpsHistoryToPointsConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is List<double> fpsHistory)
            {
                // 创建Points集合
                var points = new Points();
                
                if (fpsHistory.Count == 0)
                {
                    return points;
                }
                
                // 画布宽度和高度
                double canvasWidth = 200;
                double canvasHeight = 30;
                
                // 计算每个点的X坐标间隔
                double xStep = canvasWidth / Math.Max(1, fpsHistory.Count - 1);
                
                // 找到最大和最小FPS值，用于缩放Y坐标
                double maxFps = fpsHistory.Max();
                double minFps = fpsHistory.Min();
                double fpsRange = maxFps - minFps;
                
                // 避免除以零
                if (fpsRange < 0.1)
                {
                    fpsRange = 1;
                }
                
                // 生成Points
                for (int i = 0; i < fpsHistory.Count; i++)
                {
                    double x = i * xStep;
                    
                    // 将FPS值映射到画布高度范围内（底部为0，顶部为maxFps）
                    double y = canvasHeight - ((fpsHistory[i] - minFps) / fpsRange) * canvasHeight;
                    
                    // 确保Y坐标在画布范围内
                    y = Math.Max(0, Math.Min(canvasHeight, y));
                    
                    points.Add(new Point(x, y));
                }
                
                return points;
            }
            
            return new Points();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // 不需要反向转换
            return null;
        }
    }
}