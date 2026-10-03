// main.
using System.Reflection;
using System.Windows.Forms;

try
{
    var entryAssembly = Assembly.GetEntryAssembly();
    using Stream assemblyStream = entryAssembly?.GetManifestResourceStream("MouseClickTool.dll") ?? throw new InvalidOperationException("Embedded MouseClickTool assembly was not found.");
    using MemoryStream assemblyBytes = new();
    assemblyStream.CopyTo(assemblyBytes);
    Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
    Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
    Assembly.Load(assemblyBytes.ToArray()).CreateInstance("MouseClickTool");

    // 消息循环正常返回才会走到这里(被 TerminateProcess 杀死不会执行),用于区分"自愿退出"与"外部终止"
    try
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MouseClickTool", "logs");
        Directory.CreateDirectory(dir);
        File.AppendAllText(Path.Combine(dir, "main-returned.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} main returned normally\r\n");
    }
    catch
    {
    }
}
catch (Exception ex)
{
    LoadFail(ex);
}

// 核心程序集尚未加载,Log 类不可用,兜底信息直接写 %TEMP%
static void LoadFail(Exception ex)
{
    var path = Path.Combine(Path.GetTempPath(), "MouseClickTool_load.log");
    try
    {
        File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} load failed\r\n{ex}\r\n\r\n");
    }
    catch
    {
    }

    MessageBox.Show($"startup failed:\r\n{ex.Message}\r\n\r\nlog: {path}", "MouseClickTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
