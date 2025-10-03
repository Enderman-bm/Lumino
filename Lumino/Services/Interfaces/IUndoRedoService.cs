using System;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// 撤销重做操作接口
    /// </summary>
    public interface IUndoRedoOperation
    {
        /// <summary>
        /// 执行操作
        /// </summary>
        void Execute();

        /// <summary>
        /// 撤销操作
        /// </summary>
        void Undo();

        /// <summary>
        /// 获取操作描述
        /// </summary>
        string Description { get; }
    }

    /// <summary>
    /// 撤销重做服务接口
    /// </summary>
    public interface IUndoRedoService
    {
        /// <summary>
        /// 是否可以撤销
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// 是否可以重做
        /// </summary>
        bool CanRedo { get; }

        /// <summary>
        /// 当前撤销操作的描述
        /// </summary>
        string? UndoDescription { get; }

        /// <summary>
        /// 当前重做操作的描述
        /// </summary>
        string? RedoDescription { get; }

        /// <summary>
        /// 执行操作并添加到历史记录
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        void ExecuteAndRecord(IUndoRedoOperation operation);

        /// <summary>
        /// 撤销上一个操作
        /// </summary>
        void Undo();

        /// <summary>
        /// 重做下一个操作
        /// </summary>
        void Redo();

        /// <summary>
        /// 清空历史记录
        /// </summary>
        void Clear();

        /// <summary>
        /// 当撤销重做状态改变时触发
        /// </summary>
        event EventHandler? StateChanged;
    }
}