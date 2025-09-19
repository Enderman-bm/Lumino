using Lumino.Services.Interfaces;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// ��Ⱦͬ������ӿ�
    /// </summary>
    public interface IRenderSyncService
    {
        /// <summary>
        /// ע����Ҫͬ����Ⱦ��Ŀ��
        /// </summary>
        void RegisterTarget(IRenderSyncTarget target);

        /// <summary>
        /// ע����ȾĿ��
        /// </summary>
        void UnregisterTarget(IRenderSyncTarget target);

        /// <summary>
        /// ������ק״̬
        /// </summary>
        void SetDragState(bool isDragging);

        /// <summary>
        /// ͬ��ˢ������ע�����ȾĿ��
        /// </summary>
        void SyncRefresh();

        /// <summary>
        /// ����ͬ��ˢ�£�������ק��ʵʱ������
        /// </summary>
        void ImmediateSyncRefresh();

        /// <summary>
        /// ѡ����ˢ���ض�Ŀ�꣬���ⲻ��Ҫ��ȫ��ˢ��
        /// </summary>
        void SelectiveRefresh(IRenderSyncTarget specificTarget);
    }

    /// <summary>
    /// ��Ⱦͬ��Ŀ��ӿ�
    /// </summary>
    public interface IRenderSyncTarget
    {
        /// <summary>
        /// ˢ����Ⱦ
        /// </summary>
        void RefreshRender();
    }
}