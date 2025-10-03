using System;
// Lumino - 视图定位器，负责根据 ViewModel 类型动态查找并创建对应的 View。
// 全局注释：本文件为视图定位器，自动匹配 MVVM 视图，禁止随意更改关键逻辑。
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Lumino.ViewModels;

namespace Lumino
{
    public class ViewLocator : IDataTemplate
    {

        public Control? Build(object? param)
        {
            if (param is null)
                return null;

            var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
