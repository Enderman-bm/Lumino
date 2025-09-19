using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumino.ViewModels.Editor.State
{
    /// <summary>
    /// ���ȱ༭״̬����
    /// </summary>
    public partial class VelocityEditingState : ObservableObject
    {
        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private Point _startPosition;

        [ObservableProperty]
        private Point? _currentPosition;

        [ObservableProperty]
        private List<NoteViewModel>? _editingNotes;

        [ObservableProperty]
        private List<Point> _editingPath = new();

        // ����ԭʼ����ֵ���ڳ���
        public Dictionary<NoteViewModel, int> OriginalVelocities { get; private set; } = new();

        /// <summary>
        /// ��ʼ�༭
        /// </summary>
        public void StartEditing(Point position)
        {
            IsEditing = true;
            StartPosition = position;
            CurrentPosition = position;
            EditingPath.Clear();
            EditingPath.Add(position);
            OriginalVelocities.Clear();
        }

        /// <summary>
        /// ���µ�ǰλ��
        /// </summary>
        public void UpdatePosition(Point position)
        {
            CurrentPosition = position;
        }

        /// <summary>
        /// ���ӵ��༭·��
        /// </summary>
        public void AddToPath(Point position)
        {
            EditingPath.Add(position);
            CurrentPosition = position;
        }

        /// <summary>
        /// ���ñ༭�е�����
        /// </summary>
        public void SetEditingNotes(List<NoteViewModel> notes)
        {
            EditingNotes = notes.ToList();
        }

        /// <summary>
        /// ���ӵ����༭����
        /// </summary>
        public void AddEditingNote(NoteViewModel note)
        {
            EditingNotes ??= new List<NoteViewModel>();
            if (!EditingNotes.Contains(note))
            {
                EditingNotes.Add(note);
            }
        }

        /// <summary>
        /// ����ԭʼ����ֵ
        /// </summary>
        public void SaveOriginalVelocities(IEnumerable<NoteViewModel> notes)
        {
            foreach (var note in notes)
            {
                SaveOriginalVelocity(note);
            }
        }

        /// <summary>
        /// ���浥��������ԭʼ����ֵ
        /// </summary>
        public void SaveOriginalVelocity(NoteViewModel note)
        {
            if (!OriginalVelocities.ContainsKey(note))
            {
                OriginalVelocities[note] = note.Velocity;
            }
        }

        /// <summary>
        /// �����༭
        /// </summary>
        public void EndEditing()
        {
            IsEditing = false;
            CurrentPosition = null;
            EditingNotes?.Clear();
            EditingPath.Clear();
            OriginalVelocities.Clear();
        }

        /// <summary>
        /// ����״̬
        /// </summary>
        public void Reset()
        {
            EndEditing();
        }
    }
}