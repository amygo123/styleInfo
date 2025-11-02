using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StyleInfoWin
{
    public class ResultForm : Form
    {
        readonly TextBox _boxInput   = new();
        readonly TextBox _boxResult  = new();
        readonly Button  _btnQuery   = new() { Text = "查询" };
        readonly Button  _btnCopy    = new() { Text = "复制结果" };
        readonly Button  _btnClose   = new() { Text = "关闭(Esc)" };
        readonly Button  _btnRefresh = new() { Text = "刷新库存" };

        readonly AppConfig _cfg;
        readonly InventoryClient _invClient;

        static readonly Dictionary<string,(DateTime ts, List<InventoryRow> rows)> _cache = new();

        readonly Panel _invPanel       = new();
        readonly Label _lblInvTitle    = new();
        readonly Label _lblInvUpdate   = new();
        readonly Label _lblInvSummary  = new();
        readonly FlowLayoutPanel _top3 = new();
        readonly DataGridView _grid    = new();
        readonly FlowLayoutPanel _others = new();

        public ResultForm(AppConfig cfg, string input, string result)
        {
            _cfg = cfg;
            _invClient = new InventoryClient(cfg);

            Text   = "StyleInfo";
            Width  = Math.max(800, cfg.window.width);
            Height = Math.max(560, cfg.window.height);
            KeyPreview = true;
            TopMost = cfg.window.alwaysOnTop;

            BuildTopBar(input);
            BuildResult(result);
            BuildInventoryArea();

            var hint = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.Gray,
                Text = $"提示：Esc 关闭；Enter 查询；Ctrl+Enter 刷新库存。" };
            Controls.Add(hint);

            _btnQuery.Click    += async (_,__) => await RunMainQueryAsync();
            _btnCopy.Click     += (_,__) => { try { Clipboard.SetText(_boxResult.Text); } catch {} };
            _btnClose.Click    += (_,__) => Close();
            _btnRefresh.Click  += async (_,__) => await LoadInventoryAsync(force:true);

            KeyDown += async (_,e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
                else if (e.KeyCode == Keys.Enter && !e.Control) await RunMainQueryAsync();
                else if (e.Control && e.KeyCode == Keys.Enter) await LoadInventoryAsync(true);
            };

            Shown += async (_,__) => await LoadInventoryAsync(force:false);
        }

        void BuildTopBar(string input)
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, WrapContents = false, AutoScroll = true };
            var lbl = new Label { Text = "选中文本：", AutoSize = true, Margin = new Padding(6,10,0,0) };

            _boxInput.Width = Width - 520;
            _boxInput.Text  = input;

            top.Controls.AddRange(new Control[]{ lbl, _boxInput, _btnQuery, _btnCopy, _btnRefresh, _btnClose });
            Controls.Add(top);
        }

        void BuildResult(string result)
        {
            _boxResult.Multiline = true;
            _boxResult.ReadOnly  = true;
            _boxResult.ScrollBars= ScrollBars.Vertical;
            _boxResult.Dock      = DockStyle.Top;
            _boxResult.Font      = new Font("Consolas", _cfg.window.fontSize);
            _boxResult.Height    = 180;
            _boxResult.Text      = result;
            Controls.Add(_boxResult);
        }

        void BuildInventoryArea()
        {
            _invPanel.Dock = DockStyle.Fill;
            _invPanel.Padding = new Padding(8,6,8,6);

            var header = new Panel { Dock = DockStyle.Top, Height = 28 };
            _lblInvTitle.Text = "库存（颜色 × 尺码）";
            _lblInvTitle.AutoSize = true;
            _lblInvTitle.Font = new Font(Font, FontStyle.Bold);

            _lblInvUpdate.Text = "最近更新：--";
            _lblInvUpdate.AutoSize = true;
            _lblInvUpdate.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            header.Controls.Add(_lblInvTitle);
            header.Controls.Add(_lblInvUpdate);
            header.Resize += (_,__) =>
            {
                _lblInvUpdate.Left = header.Width - _lblInvUpdate.Width - 4;
                _lblInvUpdate.Top  = 6;
            };
            _invPanel.Controls.Add(header);

            _lblInvSummary.Dock   = DockStyle.Top;
            _lblInvSummary.Height = 22;
            _lblInvSummary.ForeColor = Color.DimGray;
            _invPanel.Controls.Add(_lblInvSummary);

            _top3.Dock = DockStyle.Top;
            _top3.Height = 36;
            _top3.WrapContents = false;
            _top3.AutoScroll = true;
            _invPanel.Controls.Add(_top3);

            _grid.Dock = DockStyle.Top;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            _grid.Height = 240;
            _grid.CellFormatting += Grid_CellFormatting;
            _invPanel.Controls.Add(_grid);

            var otherTitle = new Label { Text="其它仓（展开查看矩阵）", Dock=DockStyle.Top, Height=20, ForeColor=Color.Gray };
            _invPanel.Controls.Add(otherTitle);

            _others.Dock = DockStyle.Fill;
            _others.AutoScroll = true;
            _others.WrapContents = false;
            _others.FlowDirection = FlowDirection.TopDown;
            _invPanel.Controls.Add(_others);

            Controls.Add(_invPanel);
        }

        async Task RunMainQueryAsync()
        {
            var textNow = _boxInput.Text.Trim();
            _boxResult.Text = string.IsNullOrWhiteSpace(textNow) ? "" : $"查询：{textNow}";
            await LoadInventoryAsync(force:true);
        }

        async Task LoadInventoryAsync(bool force)
        {
            var key = _boxInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(key)) return;

            if (!force && _cache.TryGetValue(key, out var cached) &&
                (DateTime.Now - cached.ts).TotalSeconds < _cfg.inventory_cache_ttl_seconds)
            {
                BindInventory(cached.rows);
                return;
            }

            try
            {
                _lblInvUpdate.Text = "正在加载…";
                var rows = await _invClient.FetchAsync(key, CancellationToken.None);
                _cache[key] = (DateTime.Now, rows);
                BindInventory(rows);
                _lblInvUpdate.Text = $"最近更新：{DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                _lblInvUpdate.Text = $"库存获取失败：{ex.Message}";
            }
        }

        void BindInventory(List<InventoryRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                _grid.DataSource = null;
                _others.Controls.Clear();
                _top3.Controls.Clear();
                _lblInvSummary.Text = "无库存数据";
                _lblInvTitle.Text = "库存";
                return;
            }

            var byWh = rows.GroupBy(r => r.Warehouse)
                           .Select(g => new {
                               Warehouse = g.Key,
                               InSum = g.Sum(x => x.QtyIn),
                               OutSum= g.Sum(x => x.QtyOut),
                               Rows  = g.ToList()
                           })
                           .OrderByDescending(x => x.OutSum)
                           .ToList();

            var top3 = byWh.Take(3).ToList();
            var main = top3.FirstOrDefault();

            if (main != null)
            {
                var dt = PivotHelper.ToMatrix(main.Rows);
                _grid.DataSource = dt;
                _lblInvTitle.Text = $"库存（主仓：{main.Warehouse}）";
            }
            else
            {
                _grid.DataSource = null;
                _lblInvTitle.Text = "库存";
            }

            int lowTh    = Math.Max(1, _cfg.inventory_low_threshold);
            int mainLow  = main?.Rows.Count(r => r.QtyOut <= lowTh && r.QtyOut >= 0) ?? 0;
            int mainBad  = main?.Rows.Count(r => r.QtyOut < 0) ?? 0;
            int othersOut= byWh.Skip(1).Sum(x => x.OutSum);

            _lblInvSummary.Text = $"主仓可用合计：{main?.OutSum ?? 0}｜低库存(≤{lowTh})：{mainLow}｜异常(<0)：{mainBad}｜其它仓可用合计：{othersOut}";

            _top3.Controls.Clear();
            int idx = 1;
            foreach (var t in top3)
            {
                var card = new Panel { Height = 28, Width = 320, Margin = new Padding(0,4,8,4) };
                var lab  = new Label { AutoSize = true, Text = $"#{idx} {t.Warehouse}｜在库/可用：{t.InSum}/{t.OutSum}", Left = 6, Top = 6 };
                var btn  = new Button { Text = "快速查看矩阵", Width = 110, Height = 22, Left = card.Width - 120, Top = 3, Anchor = AnchorStyles.Right|AnchorStyles.Top };
                var captured = t;
                btn.Click += (_,__) => ShowWarehouseMatrix(captured.Warehouse, captured.Rows);
                card.Controls.Add(lab);
                card.Controls.Add(btn);
                _top3.Controls.Add(card);
                idx++;
            }

            _others.Controls.Clear();
            var top3Names = new HashSet<string>(top3.Select(x => x.Warehouse));
            foreach (var x in byWh)
            {
                if (top3Names.Contains(x.Warehouse)) continue;
                var panel = new Panel { Width = _others.ClientSize.Width - 28, Height = 28, BackColor = System.Drawing.Color.WhiteSmoke, Margin = new Padding(0,2,0,2) };
                panel.Anchor = AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;

                var lab  = new Label { AutoSize = true, Text = $"{x.Warehouse}｜在库/可用：{x.InSum}/{x.OutSum}｜颜色：{x.Rows.Select(r=>r.Color).Distinct().Count()}｜尺码：{x.Rows.Select(r=>r.Size).Distinct().Count()}", Left = 6, Top = 6 };
                var btn  = new Button { Text = "查看矩阵", Width = 88, Height = 22, Left = panel.Width - 96, Top = 3, Anchor = AnchorStyles.Right|AnchorStyles.Top };
                var captured = x;
                btn.Click += (_,__) => ShowWarehouseMatrix(captured.Warehouse, captured.Rows);

                panel.Controls.Add(lab);
                panel.Controls.Add(btn);
                _others.Controls.Add(panel);
            }
        }

        void ShowWarehouseMatrix(string warehouse, List<InventoryRow> rows)
        {
            var dt = PivotHelper.ToMatrix(rows);
            var f  = new Form { Text = $"{warehouse}：库存矩阵", StartPosition = FormStartPosition.CenterParent, Width = Math.Max(800, Width-60), Height = Math.Max(500, Height-60) };
            var g  = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, DataSource = dt, RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false };
            g.CellFormatting += Grid_CellFormatting;
            f.Controls.Add(g);
            f.ShowDialog(this);
        }

        void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex <= 0) return;
            var grid = (DataGridView)sender!;
            var val  = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
            if (string.IsNullOrWhiteSpace(val)) return;

            var parts = val.Split('/');
            if (parts.Length != 2) return;
            if (!int.TryParse(parts[1].Trim(), out var outQty)) return;

            if (outQty < 0)
            {
                e.CellStyle.BackColor = System.Drawing.Color.MistyRose;
                e.CellStyle.ForeColor = System.Drawing.Color.DarkRed;
            }
            else if (outQty == 0)
            {
                e.CellStyle.ForeColor = System.Drawing.Color.Gray;
            }
            else if (outQty <= _cfg.inventory_low_threshold)
            {
                e.CellStyle.BackColor = System.Drawing.Color.LemonChiffon;
            }
        }
    }
}
