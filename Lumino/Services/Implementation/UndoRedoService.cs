using System;
using System.Collections.Generic;
using Lumino.Services.Interfaces;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 撤销重做服务实现
    /// </summary>
    public class UndoRedoService : IUndoRedoService
    {
        private readonly Stack<IUndoRedoOperation> _undoStack = new();
        private readonly Stack<IUndoRedoOperation> _redoStack = new();
        private readonly int _maxHistorySize;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="maxHistorySize">最大历史记录大小</param>
        public UndoRedoService(int maxHistorySize = 100)
        {
            _maxHistorySize = maxHistorySize;
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// 当前撤销操作的描述
        /// </summary>
        public string? UndoDescription => CanUndo ? _undoStack.Peek().Description : null;

        /// <summary>
        /// 当前重做操作的描述
        /// </summary>
        public string? RedoDescription => CanRedo ? _redoStack.Peek().Description : null;

        /// <summary>
        /// 当撤销重做状态改变时触发
        /// </summary>
        public event EventHandler? StateChanged;

        /// <summary>
        /// 执行操作并添加到历史记录
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        public void ExecuteAndRecord(IUndoRedoOperation operation)
        {
            operation.Execute();
            _undoStack.Push(operation);
            _redoStack.Clear(); // 执行新操作后，清空重做栈

            // 限制历史记录大小
            if (_undoStack.Count > _maxHistorySize)
            {
                var tempStack = new Stack<IUndoRedoOperation>();
                for (int i = 0; i < _maxHistorySize; i++)
                {
                    tempStack.Push(_undoStack.Pop());
                }
                _undoStack.Clear();
                while (tempStack.Count > 0)
                {
                    _undoStack.Push(tempStack.Pop());
                }
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 撤销上一个操作
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;

            var operation = _undoStack.Pop();
            operation.Undo();
            _redoStack.Push(operation);

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 重做下一个操作
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;

            var operation = _redoStack.Pop();
            operation.Execute();
            _undoStack.Push(operation);

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 清空历史记录
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}