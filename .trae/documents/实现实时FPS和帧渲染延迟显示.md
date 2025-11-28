# 实现实时FPS和帧渲染延迟显示

## 1. 实现目标

* 将软件主页下方黑色边栏（显示"就绪"的位置）修改为实时刷新的FPS显示和帧渲染延迟显示

* 刷新频率为50ms一次

* 推送更改到两个远程仓库

## 2. 实现步骤

### 2.1 修改MainWindowViewModel.cs

* 添加FPS和FrameDelay属性，使用ObservableProperty特性实现数据绑定

* 添加定时器，每50ms更新一次FPS和帧延迟值

* 实现FPS计算逻辑，基于Avalonia的渲染事件

* 实现帧延迟计算逻辑

### 2.2 修改MainWindow\.axaml

* 更新状态栏XAML，将固定文本"就绪"替换为绑定到FPS和FrameDelay属性的动态显示

* 调整布局，确保FPS和帧延迟显示清晰美观

### 2.3 测试和编译

* 编译项目，确保没有编译错误

* 运行应用程序，验证FPS和帧延迟显示是否正常工作

* 检查刷新频率是否符合要求（50ms一次）

### 2.4 提交和推送

* 提交更改，使用中文提交信息

* 推送更改到两个远程仓库

## 3. 技术细节

### 3.1 FPS计算

* 使用Avalonia的渲染事件来跟踪帧计数

* 每50ms计算一次平均FPS

* 平滑处理FPS值，避免波动过大

### 3.2 帧延迟计算

* 测量每次渲染的时间间隔

* 计算平均帧延迟

* 显示为毫秒值

### 3.3 定时器实现

* 使用Avalonia.Threading.DispatcherTimer确保在UI线程上更新属性

* 设置定时器间隔为50ms

## 4. 预期效果

* 状态栏显示格式："FPS: XX | 帧延迟: XX ms"

* 数值每50ms更新一次

* 显示稳定，无明显闪烁

* 不影响应用程序性能

## 5. 文件修改清单

* `d:\source\Lumino\Lumino\ViewModels\MainWindowViewModel.cs`：添加FPS和帧延迟相关属性和逻辑

* `d:\source\Lumino\Lumino\Views\MainWindow.axaml`：更新状态栏显示

## 6. 提交信息

* 中文提交信息："实现实时FPS和帧渲染延迟功能

<br />

<br />

补充一点：增加fps变化曲线绘制，修改刷新时间为500ms

