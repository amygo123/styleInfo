using System.Threading.Tasks;
using System.Windows.Forms;

namespace StyleInfoWin
{
    public class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly AppConfig _cfg;

        public TrayApp()
        {
            _cfg = AppConfig.Load();
            _tray = new NotifyIcon
            {
                Text = "StyleInfo",
                Visible = true,
                Icon = System.Drawing.SystemIcons.Information
            };

            var menu   = new ContextMenuStrip();
            var miIn   = new ToolStripMenuItem("手动输入查询…", null, async (_,__) => await ShowInputAndQueryAsync());
            var miExit = new ToolStripMenuItem("退出", null, (_,__) => ExitThread());
            menu.Items.Add(miIn);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);
            _tray.ContextMenuStrip = menu;

            _tray.BalloonTipTitle = "StyleInfo";
            _tray.BalloonTipText  = $"已启动。热键：{_cfg.hotkey}";
            _tray.ShowBalloonTip(2000);

            _ = ShowInputAndQueryAsync();
        }

        private async Task ShowInputAndQueryAsync()
        {
            string text = Microsoft.VisualBasic.Interaction.InputBox("输入要查询的文本", "StyleInfo", "");
            if (string.IsNullOrWhiteSpace(text)) return;

            var result = $"查询：{text}";
            using var f = new ResultForm(_cfg, text, result);
            f.ShowDialog();
        }
    }
}
