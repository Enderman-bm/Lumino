using System;
using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.State;
using Lumino.ViewModels.Editor.Modules.Base;
using Lumino.ViewModels.Editor.Services;
using System.Diagnostics;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// ����Ԥ������ģ�� - ���ڷ�������ʵ��
    /// �ع���ʹ�û����ͨ�÷��񣬼����ظ�����
    /// </summary>
    public class NotePreviewModule : EditorModuleBase
    {
        public override string ModuleName => "NotePreview";

        public NoteViewModel? PreviewNote { get; private set; }

        public NotePreviewModule(ICoordinateService coordinateService) : base(coordinateService)
        {
        }

        /// <summary>
        /// ����Ԥ������ - ʹ�û����ͨ�÷���
        /// </summary>
        public void UpdatePreview(Point position)
        {
            if (_pianoRollViewModel == null) return;

            // �ڴ�������ʱ����ʾͨ��Ԥ��
            if (_pianoRollViewModel.CreationModule.IsCreatingNote)
            {
                ClearPreview();
                return;
            }

            // �ڵ�����Сʱ����ʾͨ��Ԥ��
            if (_pianoRollViewModel.ResizeState.IsResizing)
            {
                ClearPreview();
                return;
            }

            if ((EditorTool)_pianoRollViewModel.CurrentTool != EditorTool.Pencil)
            {
                ClearPreview();
                return;
            }

            // ����Ƿ���ͣ�������ϣ�ʹ��֧�ֹ���ƫ�����ķ���
            var hoveredNote = _pianoRollViewModel.SelectionModule.GetNoteAtPosition(position, _pianoRollViewModel.Notes, 
                _pianoRollViewModel.TimeToPixelScale, _pianoRollViewModel.KeyHeight);
            if (hoveredNote != null)
            {
                // ��ͣ������ʱ����ʾԤ��������Ϊ����ʾ��ק��꣩
                ClearPreview();
                return;
            }

            // ����Ƿ���ͣ�ڿɵ�����С��������Ե��
            if (hoveredNote != null)
            {
                var handle = _pianoRollViewModel.GetResizeHandleAtPosition(position, hoveredNote);
                if (handle == ResizeHandle.StartEdge || handle == ResizeHandle.EndEdge)
                {
                    // ��ͣ�ڵ�����Ե�ϣ�����ʾԤ������
                    ClearPreview();
                    return;
                }
            }

            // ʹ�û����ͨ������ת������֤
            var pitch = GetPitchFromPosition(position);
            var timeValue = GetTimeFromPosition(position);

            if (EditorValidationService.IsValidNotePosition(pitch, timeValue))
            {
                // ʹ�û����ͨ����������
                var quantizedPosition = GetQuantizedTimeFromPosition(position);

                // ֻ��Ԥ������ʵ�ʸı�ʱ�Ÿ��£����Ӹ�׼ȷ�ıȽ�
                bool shouldUpdate = false;
                
                if (PreviewNote == null)
                {
                    shouldUpdate = true;
                }
                else if (PreviewNote.Pitch != pitch)
                {
                    shouldUpdate = true;
                }
                else if (!PreviewNote.StartPosition.Equals(quantizedPosition))
                {
                    shouldUpdate = true;
                }
                else if (!PreviewNote.Duration.Equals(_pianoRollViewModel.UserDefinedNoteDuration))
                {
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    PreviewNote = new NoteViewModel
                    {
                        Pitch = pitch,
                        StartPosition = quantizedPosition,
                        Duration = _pianoRollViewModel.UserDefinedNoteDuration,
                        Velocity = 100,
                        IsPreview = true
                    };

                    OnPreviewUpdated?.Invoke();
                }
            }
            else
            {
                ClearPreview();
            }
        }

        /// <summary>
        /// ���Ԥ������
        /// </summary>
        public void ClearPreview()
        {
            if (PreviewNote != null)
            {
                PreviewNote = null;
                OnPreviewUpdated?.Invoke();
            }
        }

        // �¼�
        public event Action? OnPreviewUpdated;
    }
}