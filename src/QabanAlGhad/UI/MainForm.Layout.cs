using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    public sealed partial class MainForm
    {
        static readonly Color Ink = Color.FromArgb(24, 43, 57), Muted = Color.FromArgb(110, 124, 135),
                              Canvas = Color.FromArgb(246, 248, 250), Accent = Color.FromArgb(0, 121, 113);
        Panel screenHost = new Panel(), overviewPage = new Panel(), settingsPage = new Panel(), logsPage = new Panel();
        Button overviewNav = new Button(), settingsNav = new Button(), logsNav = new Button();
        Button operationIndicator = new Button();
        Size compactClient;
        bool pageSizingReady;
        float pageScale = 1;
        DataGridView readingGrid = new DataGridView(), eventGrid = new DataGridView(), activeLogGrid;
        Label CenterLabel(string text, float size, Color color, Color background)
        {
            return new Label { Text = text,           Font = new Font("Segoe UI", size),
                               ForeColor = color,     BackColor = background,
                               Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                               AutoEllipsis = true,   Margin = new Padding(0) };
        }
        TableLayoutPanel Centered(Control control)
        {
            var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, BackColor = Canvas,
                                              Margin = new Padding(0) };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            control.Anchor = AnchorStyles.None;
            host.Controls.Add(control, 0, 0);
            return host;
        }
        void StyleButton(Button b, string text, bool primary)
        {
            b.Text = text;
            b.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.UseVisualStyleBackColor = false;
            b.BackColor = primary ? Accent : Color.FromArgb(231, 237, 241);
            b.ForeColor = primary ? Color.White : Ink;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0, 105, 98) : Color.FromArgb(219, 229, 235);
            b.Height = 44;
            b.Width = 142;
            b.Margin = new Padding(5);
            b.Cursor = Cursors.Hand;
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.AccessibleName = text;
        }
        FlowLayoutPanel Buttons(params Control[] buttons)
        {
            var flow = new FlowLayoutPanel { AutoSize = true,      AutoSizeMode = AutoSizeMode.GrowAndShrink,
                                             WrapContents = false, FlowDirection = FlowDirection.RightToLeft,
                                             BackColor = Canvas,   Margin = new Padding(0) };
            flow.Controls.AddRange(buttons);
            return flow;
        }
        void BuildInterface()
        {
            Text = "قبان الغد";
            ClientSize = new Size(640, 304);
            MinimumSize = new Size(600, 332);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Font;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = Canvas;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                Icon = SystemIcons.Application;
            }
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
                                              Padding = new Padding(16, 10, 16, 8), BackColor = Canvas };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            Controls.Add(root);
            var header = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), RightToLeft = RightToLeft.No,
                                     BackColor = Canvas };
            StyleButton(updateInfo, "!", false);
            updateInfo.Size = new Size(24, 24);
            updateInfo.Location = new Point(0, 2);
            updateInfo.Margin = new Padding(0);
            updateInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            updateInfo.AccessibleName = "معلومات الإصدار والتحديث";
            updateInfo.SizeChanged += (sender, e) => ShapeUpdateIcon();
            ShapeUpdateIcon();
            header.Controls.Add(updateInfo);
            root.Controls.Add(header, 0, 0);
            versionCredit = CenterLabel(CurrentVersion.ToString(3) + " - Abdelrahman Sawaftah", 8, Muted, Canvas);
            versionCredit.RightToLeft = RightToLeft.No;
            root.Controls.Add(versionCredit, 0, 3);
            var navigation =
                new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3,         RowCount = 1,
                                       BackColor = Canvas,    Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            for (int column = 0; column < 3; column++)
                navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            StyleButton(overviewNav, "الوزن", true);
            StyleButton(logsNav, "السجل", false);
            StyleButton(settingsNav, "الإعدادات", false);
            var tabs = new[] { overviewNav, logsNav, settingsNav };
            for (int column = 0; column < tabs.Length; column++)
            {
                tabs[column].Dock = DockStyle.Fill;
                tabs[column].Margin = new Padding(0);
                navigation.Controls.Add(tabs[column], column, 0);
            }
            root.Controls.Add(navigation, 0, 1);
            screenHost.Dock = DockStyle.Fill;
            screenHost.BackColor = Canvas;
            screenHost.Margin = new Padding(0);
            root.Controls.Add(screenHost, 0, 2);
            foreach (var page in new[] { overviewPage, settingsPage, logsPage })
            {
                page.Dock = DockStyle.Fill;
                page.AutoScroll = true;
                page.BackColor = Canvas;
                screenHost.Controls.Add(page);
            }
            BuildOverview();
            BuildSettings();
            BuildLogs();
            ShowScreen(overviewPage, overviewNav);
            overviewNav.Click += (s, e) => ShowScreen(overviewPage, overviewNav);
            settingsNav.Click += (s, e) =>
            {
                if (settingsPage.Visible)
                    return;
                using (var gate = new SettingsUnlockDialog())
                {
                    if (gate.ShowDialog(this) == DialogResult.OK)
                        ShowScreen(settingsPage, settingsNav);
                }
            };
            logsNav.Click += (s, e) => ShowScreen(logsPage, logsNav);
            Shown += (sender, e) =>
            {
                compactClient = ClientSize;
                pageScale = compactClient.Width / 640f;
                pageSizingReady = true;
                FitPageSize(overviewPage);
            };
            RefreshInterface();
        }
        void ShapeUpdateIcon()
        {
            int diameter = Math.Min(updateInfo.Width, updateInfo.Height);
            if (diameter <= 0)
                return;
            using (var circle = new System.Drawing.Drawing2D.GraphicsPath())
            {
                circle.AddEllipse((updateInfo.Width - diameter) / 2, (updateInfo.Height - diameter) / 2, diameter,
                                  diameter);
                updateInfo.Region = new Region(circle);
            }
        }
        void ShowScreen(Panel page, Button selected)
        {
            foreach (var p in new[] { overviewPage, settingsPage, logsPage })
                p.Visible = p == page;
            page.BringToFront();
            FitPageSize(page);
            foreach (var b in new[] { overviewNav, settingsNav, logsNav })
            {
                bool on = b == selected;
                b.BackColor = on ? Accent : Color.FromArgb(231, 237, 241);
                b.ForeColor = on ? Color.White : Ink;
                b.FlatAppearance.MouseOverBackColor = on ? Color.FromArgb(0, 105, 98) : Color.FromArgb(219, 229, 235);
            }
        }
        void FitPageSize(Panel page)
        {
            if (!pageSizingReady || WindowState != FormWindowState.Normal)
                return;
            var area = Screen.FromControl(this).WorkingArea;
            Size desired =
                page == overviewPage ? compactClient : new Size((int)(820 * pageScale), (int)(640 * pageScale));
            int borderWidth = Width - ClientSize.Width, borderHeight = Height - ClientSize.Height;
            desired.Width =
                Math.Min(desired.Width, Math.Max(MinimumSize.Width - borderWidth, area.Width - borderWidth - 12));
            desired.Height =
                Math.Min(desired.Height, Math.Max(MinimumSize.Height - borderHeight, area.Height - borderHeight - 12));
            Point center = new Point(Left + Width / 2, Top + Height / 2);
            ClientSize = desired;
            Left = Math.Max(area.Left, Math.Min(center.X - Width / 2, area.Right - Width));
            Top = Math.Max(area.Top, Math.Min(center.Y - Height / 2, area.Bottom - Height));
        }
        void BuildOverview()
        {
            var host = new TableLayoutPanel { Dock = DockStyle.Fill,
                                              ColumnCount = 1,
                                              RowCount = 3,
                                              Padding = new Padding(0, 10, 0, 0),
                                              Margin = new Padding(0),
                                              BackColor = Canvas,
                                              RightToLeft = RightToLeft.No };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            overviewPage.Controls.Add(host);
            var hero = new SurfacePanel { Dock = DockStyle.Fill,    BackColor = Canvas,
                                          Surface = Canvas,         BorderColor = Color.FromArgb(150, 169, 180),
                                          Padding = new Padding(8), Margin = new Padding(0) };
            weight.Text = "kg";
            weight.Font = new Font("Segoe UI", 36, FontStyle.Bold);
            weight.ForeColor = Ink;
            weight.BackColor = Canvas;
            weight.TextAlign = ContentAlignment.MiddleCenter;
            weight.RightToLeft = RightToLeft.No;
            weight.Dock = DockStyle.Fill;
            hero.Controls.Add(weight);
            host.Controls.Add(hero, 0, 0);
            var controls =
                new TableLayoutPanel { Dock = DockStyle.Fill,   ColumnCount = 5,    RowCount = 1,
                                       Margin = new Padding(0), BackColor = Canvas, RightToLeft = RightToLeft.Yes };
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            controls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            StyleButton(start, "تشغيل", true);
            StyleButton(stop, "إيقاف", false);
            stop.Enabled = false;
            StyleButton(operationIndicator, "متوقف", false);
            operationIndicator.TabStop = false;
            operationIndicator.Cursor = Cursors.Default;
            foreach (var button in new[] { start, stop, operationIndicator })
            {
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(0);
                button.RightToLeft = RightToLeft.Yes;
            }
            controls.Controls.Add(start, 0, 0);
            controls.Controls.Add(stop, 2, 0);
            controls.Controls.Add(operationIndicator, 4, 0);
            host.Controls.Add(controls, 0, 2);
        }
        void BuildSettings()
        {
            var outer =
                new TableLayoutPanel { Dock = DockStyle.Top, Height = 445,       ColumnCount = 1,
                                       RowCount = 2,         BackColor = Canvas, Padding = new Padding(8, 20, 8, 0) };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 355));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            settingsPage.Controls.Add(outer);
            fields.Dock = DockStyle.Fill;
            fields.BackColor = Canvas;
            fields.ColumnCount = 2;
            fields.RowCount = 1;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outer.Controls.Add(fields, 0, 0);
            ConfigureNumber(baud, 300, 115200);
            ConfigureNumber(bits, 5, 8);
            ConfigureNumber(interval, 1000, 1000);
            ConfigureNumber(fresh, 1000, 30000);
            parity.DropDownStyle = ComboBoxStyle.DropDownList;
            parity.Items.AddRange(new object[] { "None", "Even", "Odd", "Mark", "Space" });
            parity.DrawMode = DrawMode.OwnerDrawFixed;
            parity.DrawItem += (sender, e) =>
            {
                e.DrawBackground();
                string text = e.Index < 0 ? parity.Text : Convert.ToString(parity.Items[e.Index]);
                using (var brush = new SolidBrush(e.ForeColor)) using (var format = new StringFormat {
                    Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center
                }) e.Graphics.DrawString(text, e.Font, brush, e.Bounds, format);
                e.DrawFocusRectangle();
            };
            var serialFields = SettingsCard("الميزان", 0);
            var serverFields = SettingsCard("ربط النظام", 1);
            MinimalField(serialFields, "المنفذ", port);
            MinimalField(serialFields, "السرعة", baud);
            MinimalField(serialFields, "بتات البيانات", bits);
            MinimalField(serialFields, "التكافؤ", parity);
            MinimalField(serialFields, "مهلة القراءة (ms)", fresh);
            MinimalField(serverFields, "عنوان النظام", endpoint);
            MinimalField(serverFields, "الشركة", company);
            token.UseSystemPasswordChar = true;
            MinimalField(serverFields, "رمز API", token);
            foreach (var grid in new[] { serialFields, serverFields })
            {
                grid.RowCount++;
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            }
            StyleButton(save, "حفظ", true);
            var diagnose = new Button();
            StyleButton(diagnose, "فحص الاتصال", false);
            diagnose.Click += async (sender, e) => await ShowDiagnostics(diagnose);
            outer.Controls.Add(Centered(Buttons(save, diagnose)), 0, 1);
        }
        TableLayoutPanel SettingsCard(string title, int column)
        {
            var card = new SurfacePanel { Dock = DockStyle.Fill, BackColor = Canvas, Padding = new Padding(16),
                                          Margin = new Padding(8) };
            var content =
                new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var heading = CenterLabel(title, 13, Ink, Color.White);
            heading.Font = new Font("Segoe UI", 13, FontStyle.Bold);
            content.Controls.Add(heading, 0, 0);
            var grid =
                new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 0, BackColor = Color.White };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.Controls.Add(grid, 0, 1);
            card.Controls.Add(content);
            fields.Controls.Add(card, column, 0);
            return grid;
        }
        void MinimalField(TableLayoutPanel grid, string title, Control input)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
            grid.Controls.Add(CenterLabel(title, 9, Muted, Color.White), 0, row);
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 8, 0, 8);
            input.Font = new Font("Segoe UI", 10);
            input.RightToLeft = RightToLeft.No;
            input.AccessibleName = title;
            var box = input as TextBox;
            if (box != null)
            {
                box.TextAlign = HorizontalAlignment.Center;
                box.BorderStyle = BorderStyle.FixedSingle;
                box.BackColor = Color.White;
            }
            var number = input as NumericUpDown;
            if (number != null)
                number.TextAlign = HorizontalAlignment.Center;
            grid.Controls.Add(input, 1, row);
        }
        void BuildLogs()
        {
            var outer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = Canvas,
                                               Padding = new Padding(8) };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            logsPage.Controls.Add(outer);
            logTimes.Dock = DockStyle.Fill;
            logTimes.TextAlign = ContentAlignment.MiddleCenter;
            outer.Controls.Add(logTimes, 0, 5);
            outer.Controls.Add(CenterLabel("القراءات", 11, Ink, Canvas), 0, 0);
            ConfigureLogGrid(readingGrid);
            readingGrid.Columns.Add("time", "الوقت");
            readingGrid.Columns.Add("weight", "الوزن kg");
            readingGrid.Columns.Add("frame", "الإطار");
            readingGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            outer.Controls.Add(readingGrid, 0, 1);
            outer.Controls.Add(CenterLabel("الأحداث والأعطال", 11, Ink, Canvas), 0, 2);
            ConfigureLogGrid(eventGrid);
            eventGrid.Columns.Add("time", "الوقت");
            eventGrid.Columns.Add("type", "الحدث");
            eventGrid.Columns.Add("detail", "التفاصيل");
            eventGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            outer.Controls.Add(eventGrid, 0, 3);
            var open = new Button();
            var copy = new Button();
            StyleButton(open, "فتح السجل", false);
            StyleButton(copy, "نسخ التفاصيل", false);
            outer.Controls.Add(Centered(Buttons(open, copy)), 0, 4);
            open.Click += (sender, e) =>
            {
                try
                {
                    Directory.CreateDirectory(dataDir);
                    Process.Start("explorer.exe", "\"" + dataDir + "\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            };
            copy.Click += (sender, e) =>
            {
                if (activeLogGrid == null || activeLogGrid.SelectedRows.Count == 0)
                    return;
                try
                {
                    var data = activeLogGrid.GetClipboardContent();
                    if (data != null)
                        Clipboard.SetDataObject(data, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            };
        }
        void ConfigureLogGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Color.FromArgb(231, 237, 241);
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.DefaultCellStyle.ForeColor = Ink;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(221, 239, 236);
            grid.DefaultCellStyle.SelectionForeColor = Ink;
            grid.RowTemplate.Height = 30;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            grid.Enter += (sender, e) => activeLogGrid = grid;
        }
        void RefreshInterface()
        {
            bool active = settings != null && !primary.WriteFailed &&
                          OperationStatus.Active(running, clock.ElapsedMilliseconds, received, settings.Freshness,
                                                 primary.LastBodyWritten);
            bool current =
                settings != null && ReadingPolicy.IsFresh(clock.ElapsedMilliseconds, received, settings.Freshness);
            operationIndicator.Text = active    ? "يعمل ويرسل"
                                      : running ? (current && primary.WriteFailed ? "تعذر الإرسال" : readingState)
                                      : settingsNeedRepair ? "راجع الإعدادات"
                                                           : "متوقف";
            if (logsPage.Visible)
                logTimes.Text = "آخر قراءة " + DisplayTime(lastReadingLocal) + "   |   آخر إرسال " +
                                DisplayTime(primary.LastWriteLocal);
            operationIndicator.BackColor = active ? Color.FromArgb(30, 144, 86) : Color.FromArgb(186, 58, 64);
            operationIndicator.ForeColor = Color.White;
            operationIndicator.FlatAppearance.MouseOverBackColor = operationIndicator.BackColor;
            operationIndicator.FlatAppearance.MouseDownBackColor = operationIndicator.BackColor;
        }
    }
}
