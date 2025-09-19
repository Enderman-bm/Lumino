using System;
using Avalonia;
using Avalonia.Media;

namespace Lumino.Views.Rendering.Utils
{
    /// <summary>
    /// ��Ⱦ������ - �ṩͨ�õ���Դ��ȡ�ͻ�ˢ����
    /// ����MVVM�淶������̬���߷����������������ViewModel
    /// </summary>
    public static class RenderingUtils
    {
        /// <summary>
        /// ��ȡ��Դ��ˢ�������Դ��������ʹ�û�����ɫ
        /// </summary>
        /// <param name="key">��Դ����</param>
        /// <param name="fallbackHex">���˵�ʮ��������ɫֵ</param>
        /// <returns>��ˢ����</returns>
        public static IBrush GetResourceBrush(string key, string fallbackHex)
        {
            try
            {
                if (Application.Current?.Resources.TryGetResource(key, null, out var obj) == true && obj is IBrush brush)
                    return brush;
            }
            catch { }

            try
            {
                return new SolidColorBrush(Color.Parse(fallbackHex));
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        /// <summary>
        /// ��ȡ��Դ���ʣ������Դ��������ʹ�û�����ɫ
        /// </summary>
        /// <param name="brushKey">��ˢ��Դ����</param>
        /// <param name="fallbackHex">���˵�ʮ��������ɫֵ</param>
        /// <param name="thickness">���ʴ�ϸ��Ĭ��Ϊ1</param>
        /// <returns>���ʶ���</returns>
        public static IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            return new Pen(brush, thickness);
        }

        /// <summary>
        /// ��������ָ��͸���ȵĻ�ˢ
        /// </summary>
        /// <param name="originalBrush">ԭʼ��ˢ</param>
        /// <param name="opacity">͸���ȣ�0.0-1.0��</param>
        /// <returns>�µĻ�ˢ����</returns>
        public static IBrush CreateBrushWithOpacity(IBrush originalBrush, double opacity)
        {
            if (originalBrush is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                return new SolidColorBrush(color, opacity);
            }
            return originalBrush;
        }

        /// <summary>
        /// ��������ָ��͸���ȵĴ�ɫ��ˢ
        /// </summary>
        /// <param name="colorHex">ʮ��������ɫֵ</param>
        /// <param name="opacity">͸���ȣ�0.0-1.0��</param>
        /// <returns>�µĻ�ˢ����</returns>
        public static IBrush CreateBrushWithOpacity(string colorHex, double opacity)
        {
            try
            {
                var color = Color.Parse(colorHex);
                return new SolidColorBrush(color, opacity);
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
    }
}