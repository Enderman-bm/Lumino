using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// ��ԴԤ���ط��� - ȷ������UI��Դ����Ⱦǰ��ȫ����
    /// </summary>
    public class ResourcePreloadService
    {
        private static ResourcePreloadService? _instance;
        private bool _resourcesLoaded = false;
        private readonly object _lockObject = new object();

        public static ResourcePreloadService Instance => _instance ??= new ResourcePreloadService();

        /// <summary>
        /// ��Դ�Ƿ�����ȫ����
        /// </summary>
        public bool ResourcesLoaded
        {
            get
            {
                lock (_lockObject)
                {
                    return _resourcesLoaded;
                }
            }
            private set
            {
                lock (_lockObject)
                {
                    _resourcesLoaded = value;
                }
            }
        }

        /// <summary>
        /// Ԥ�������йؼ�UI��Դ
        /// </summary>
        public async Task PreloadResourcesAsync()
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine("��ʼԤ����UI��Դ...");

                    // ��֤�ؼ���Դ�Ƿ����
                    ValidateKeyResources();

                    System.Diagnostics.Debug.WriteLine("UI��ԴԤ�������");
                    ResourcesLoaded = true;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ԴԤ����ʧ��: {ex.Message}");
                // ��ʹʧ��Ҳ���Ϊ�Ѽ��أ��������޵ȴ�
                ResourcesLoaded = true;
            }
        }

        /// <summary>
        /// ��֤�ؼ���Դ�Ƿ����
        /// </summary>
        private void ValidateKeyResources()
        {
            // �ؼ���ˢ��Դ�б�
            string[] keyBrushResources = 
            {
                "MainCanvasBackgroundBrush",
                "GridLineBrush", 
                "KeyWhiteBrush",
                "KeyBlackBrush",
                "NoteBrush",
                "SelectionBrush",
                "VelocityIndicatorBrush",
                "MeasureLineBrush",
                "BorderLineBlackBrush"
            };

            foreach (var resourceKey in keyBrushResources)
            {
                try
                {
                    // ���Ի�ȡ��Դ����������ڻ᷵�ػ�����ɫ
                    var brush = RenderingUtils.GetResourceBrush(resourceKey, "#FFFFFFFF");
                    System.Diagnostics.Debug.WriteLine($"��Դ��֤�ɹ�: {resourceKey}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"��Դ��֤ʧ��: {resourceKey} - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// �ȴ���Դ�������
        /// </summary>
        public async Task WaitForResourcesAsync(int timeoutMs = 5000)
        {
            var startTime = DateTime.Now;
            while (!ResourcesLoaded && (DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                await Task.Delay(50);
            }

            if (!ResourcesLoaded)
            {
                System.Diagnostics.Debug.WriteLine("��Դ���س�ʱ������ִ��");
                ResourcesLoaded = true; // ��ֹ���޵ȴ�
            }
        }

        /// <summary>
        /// ������Դ����״̬�����������л�ʱ��
        /// </summary>
        public void ResetResourceState()
        {
            ResourcesLoaded = false;
        }
    }
}