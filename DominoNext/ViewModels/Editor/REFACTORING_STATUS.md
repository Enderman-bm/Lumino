# �ع����������޸��ܽ�

�����ع��汾��ԭʼ�汾��ͬһ�����ռ��в�����������ͻ����Ҫ���������޸���

## �޸�����

1. **�������ع��汾����**��
   - `PianoRollViewModel` �� `PianoRollViewModelV2`
   - `EditorCommandsViewModel` �� `EditorCommandsViewModelV2`
   - `NoteEditingLayer` �� `NoteEditingLayerV2`

2. **ͳһö�ٶ���**��
   - ʹ�ö�����ö���ļ������ظ�����

3. **ģ��ӿڵ���**��
   - ��������ģ���е�ViewModel��������

## ����������ع���ʽ

���ǵ�������ͻ���⣬����������·�ʽ֮һ��

### ����һ�������µ������ռ�
```csharp
namespace Lumino.ViewModels.Editor.V2
{
    public partial class PianoRollViewModel : ViewModelBase
    {
        // �ع���Ĵ���
    }
}
```

### ��������ʹ�ú�׺����
```csharp
namespace Lumino.ViewModels.Editor
{
    public partial class PianoRollViewModelRefactored : ViewModelBase
    {
        // �ع���Ĵ���
    }
}
```

## ��ǰ״̬

Ŀǰ�Ѵ������ع��汾�����б�Ҫ�ļ���������������ͻ���±��������Ҫ��

1. ͳһ�����������ع��汾����
2. ������������
3. ����ʾ���÷�����

## ��һ������

1. �������յ���������
2. �������������ļ��е���������
3. ����Ǩ��ָ�Ϻ�ʾ������
4. ���������ı������