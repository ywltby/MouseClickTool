using System.Globalization;
using System.Reflection;
using System.Security.Principal;
using System.Text;

// 轻量诊断日志,只记状态转换与异常,不逐次点击,避免性能回退。
// 落盘位置优先"我的文档\MouskClickTool\logs"(与配置 INI 同目录层级,任何启动方式都可写),
// 失败回退 %TEMP%\MouseClickTool_log,再失败静默禁用;所有入口吞异常,日志本身绝不成为新的崩溃源
internal static class Log
{
    private const int KeepDays = 7;
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly object Gate = new();
    private static StreamWriter? w;
    private static string? dir;
    private static string? file;
    private static int day;
    private static long size;
    private static bool dead;
    private static bool cleaned;

    public static void Info(string msg) => Write("INFO", msg);

    public static void Warn(string msg) => Write("WARN", msg);

    public static void Error(string msg, Exception? ex = null) => Write("ERROR", ex == null ? msg : msg + "\r\n" + ex);

    // 进程环境横幅:每次运行一段,含排障所需的基本信息(是否管理员、CWD、exe 路径等)
    public static void Banner()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly().GetName();
            var admin = false;
            try
            {
                admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
            }

            var exe = Assembly.GetEntryAssembly()?.Location ?? AppDomain.CurrentDomain.BaseDirectory;
            Write("INFO", new string('=', 60));
            Write("INFO", $"{asm.Name} v{asm.Version} | clr={Environment.Version} | os={Environment.OSVersion} | x64proc={Environment.Is64BitProcess} | admin={admin} | {CultureInfo.CurrentUICulture.Name}");
            Write("INFO", $"exe={exe} | cwd={Environment.CurrentDirectory}");
            Write("INFO", file == null ? "log=DISABLED (no writable location)" : $"log file={file}");
        }
        catch
        {
        }
    }

    private static void Write(string level, string msg)
    {
        try
        {
            lock (Gate)
            {
                if (dead)
                {
                    return;
                }

                EnsureWriter();
                if (w == null)
                {
                    return;
                }

                var line = $"[{DateTime.Now:HH:mm:ss.fff}] {level,-5} [t{Environment.CurrentManagedThreadId}] {msg}\r\n";
                w.Write(line);
                size += line.Length;
                if (size > MaxBytes)
                {
                    Roll();
                }
            }
        }
        catch
        {
            // 瞬时 IO 失败只丢弃本次,不置 dead,下次写入会尝试重开
            w = null;
        }
    }

    private static void EnsureWriter()
    {
        var today = int.Parse(DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (w != null && day == today)
        {
            return;
        }

        w?.Dispose();
        w = null;

        // 优先 exe 同级 logs 目录(用户可直接随 exe 取走);只读目录(如 Program Files)回退"我的文档",再回退 %TEMP%
        var exeLogs = Path.Combine(AppContext.BaseDirectory, "logs");
        var docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MouseClickTool", "logs");
        var temp = Path.Combine(Path.GetTempPath(), "MouseClickTool_log");
        foreach (var d in new[] { dir, exeLogs, docs, temp })
        {
            if (d != null && TryOpen(d))
            {
                if (!cleaned)
                {
                    cleaned = true;
                    Cleanup();
                }

                return;
            }
        }

        dead = true; // 所有候选目录均不可写
    }

    private static bool TryOpen(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            file = Path.Combine(folder, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");
            w = new StreamWriter(file, true, Encoding.UTF8) { AutoFlush = true };
            dir = folder;
            day = int.Parse(DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            size = new FileInfo(file).Length;
            return true;
        }
        catch
        {
            w = null;
            return false;
        }
    }

    // 单文件超限滚动:当前文件改名为 .old 后重开新文件
    private static void Roll()
    {
        try
        {
            w?.Dispose();
            w = null;
            try
            {
                File.Delete(file + ".old");
                File.Move(file!, file + ".old");
            }
            catch
            {
            }

            if (!TryOpen(dir!))
            {
                dead = true;
            }
        }
        catch
        {
            dead = true;
        }
    }

    // 文件名 yyyy-MM-dd.log 的字典序即时间序,删除保留期外的旧日志
    private static void Cleanup()
    {
        try
        {
            var cut = DateTime.Now.AddDays(-KeepDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log";
            foreach (var f in Directory.GetFiles(dir!, "*.log"))
            {
                if (string.Compare(Path.GetFileName(f), cut, StringComparison.Ordinal) < 0)
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch
        {
        }
    }
}
