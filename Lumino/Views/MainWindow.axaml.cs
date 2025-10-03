// 文件用途：
// MainWindow 是应用程序的主窗口类，负责初始化和显示主界面。
// 使用限制：
// 1. 仅供 Lumino 项目使用。
// 2. 修改此文件需经过代码审查。

using System;
using Avalonia.Controls;
using EnderDebugger;

namespace Lumino.Views
{
    public partial class MainWindow : Window
    {
        private readonly EnderLogger _logger;

        public MainWindow()
        {
            InitializeComponent();
            _logger = new EnderLogger("MainWindow");
            _logger.Info("Initialization", "[EnderDebugger][{DateTime.Now}][EnderLogger][MainWindow] 主窗口已初始化。");
        }
    }
}