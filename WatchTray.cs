using System.Windows.Forms;

namespace MxThumbWheelReader;

internal sealed class WatchTray : IDisposable
{
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new(false);
    private Control? dispatcher;
    private ToolStripMenuItem? exit;
    private Exception? startupError;

    public WatchTray(Action stop)
    {
        thread = new Thread(() =>
        {
            try
            {
                using var control = new Control();
                _ = control.Handle;
                dispatcher = control;
                using var menu = new ContextMenuStrip();
                menu.Items.Add("MX Thumb Wheel – hlídání běží").Enabled = false;
                menu.Items.Add(new ToolStripSeparator());
                exit = new ToolStripMenuItem("Ukončit");
                exit.Click += (_, _) => { exit.Enabled = false; stop(); };
                menu.Items.Add(exit);
                using var icon = new NotifyIcon
                {
                    Icon = System.Drawing.SystemIcons.Application,
                    Text = "MX Thumb Wheel – hlídání",
                    ContextMenuStrip = menu,
                    Visible = true
                };
                ready.Set();
                // A message loop sleeps until Windows delivers a UI event; no timer.
                Application.Run();
            }
            catch (Exception ex) { startupError = ex; }
            finally { ready.Set(); stop(); }
        }) { IsBackground = true, Name = "MX tray" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (startupError is not null)
        {
            thread.Join();
            ready.Dispose();
            throw new System.ComponentModel.Win32Exception("Nelze vytvořit ikonu v systémové liště.", startupError);
        }
    }

    // Exercise the same menu action from the behavior tests.
    internal void ClickExit() => dispatcher!.Invoke((Action)(() => exit!.PerformClick()));

    public void Dispose()
    {
        try { dispatcher?.BeginInvoke((Action)Application.ExitThread); }
        catch (InvalidOperationException) { /* The message loop already ended. */ }
        thread.Join();
        ready.Dispose();
    }
}
