using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    public sealed partial class MainForm
    {
        static readonly Color Ink = UiTheme.Navy, Muted = UiTheme.Muted, Canvas = UiTheme.Canvas, Accent = UiTheme.Teal;
        Panel screenHost = new Panel(), overviewPage = new Panel(), settingsPage = new Panel(), logsPage = new Panel();
        Button overviewNav = new NavButton(), settingsNav = new NavButton(), logsNav = new NavButton();
        Label operationIndicator = new StatusBadge();
        readonly TextBox logSearch = new TextBox();
        readonly ComboBox logFilter = new ComboBox();
        bool followLogs = true;
        Size compactClient;
        bool pageSizingReady;
        float pageScale = 1;
        DataGridView readingGrid = new LogGrid(), eventGrid = new LogGrid(), activeLogGrid;
        TableLayoutPanel settingsOuter;
        readonly Panel settingsViewport = new Panel();
        SurfacePanel serialCard, serverCard;

        Label CenterLabel(string text, float size, Color color, Color background)
        {
            var label = UiTheme.Label(text, new Font("Segoe UI", size));
            label.ForeColor = color;
            label.BackColor = background;
            label.TextAlign = ContentAlignment.MiddleCenter;
            return label;
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
        void StyleButton(Button button, string text, bool primary)
        {
            UiTheme.Button(button, text, primary);
        }
        FlowLayoutPanel Buttons(params Control[] buttons)
        {
            var flow = new FlowLayoutPanel { AutoSize = true,
                                             AutoSizeMode = AutoSizeMode.GrowAndShrink,
                                             WrapContents = false,
                                             FlowDirection = FlowDirection.RightToLeft,
                                             RightToLeft = RightToLeft.No,
                                             BackColor = Canvas,
                                             Margin = new Padding(0) };
            flow.Controls.AddRange(buttons);
            return flow;
        }
        void BuildInterface()
        {
            Text = "قبان الغد";
            ClientSize = new Size(640, 320);
            MinimumSize = new Size(600, 340);
            StartPosition = FormStartPosition.CenterScreen;
            Font = UiTheme.Body;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = Canvas;
            ForeColor = Ink;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                Icon = SystemIcons.Application;
            }
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
                                              Padding = new Padding(22, 10, 22, 6), BackColor = Canvas };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            Controls.Add(root);
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                                                RightToLeft = RightToLeft.Yes, Margin = new Padding(0) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var title = UiTheme.Label("قبان الغد", new Font("Segoe UI", 14, FontStyle.Bold));
            header.Controls.Add(title, 0, 0);
            StyleButton(updateInfo, "!", false);
            updateInfo.Size = new Size(23, 23);
            updateInfo.Anchor = AnchorStyles.None;
            updateInfo.Margin = new Padding(0);
            updateInfo.AccessibleName = "معلومات الإصدار والتحديث";
            helpTips.SetToolTip(updateInfo, "معلومات الإصدار والتحديث");
            updateInfo.SizeChanged += (sender, e) => ShapeUpdateIcon();
            ShapeUpdateIcon();
            header.Controls.Add(updateInfo, 1, 0);
            root.Controls.Add(header, 0, 0);
            var navigation = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
                                                    RightToLeft = RightToLeft.Yes, Margin = new Padding(0) };
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            StyleButton(overviewNav, "الوزن", false);
            StyleButton(logsNav, "السجل", false);
            StyleButton(settingsNav, "الإعدادات", false);
            var tabs = new[] { overviewNav, logsNav, settingsNav };
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].Dock = DockStyle.Fill;
                tabs[i].Margin = new Padding(0);
                tabs[i].TabIndex = i;
                navigation.Controls.Add(tabs[i], i, 0);
            }
            root.Controls.Add(navigation, 0, 1);
            screenHost.Dock = DockStyle.Fill;
            screenHost.Margin = new Padding(0);
            screenHost.BackColor = Canvas;
            root.Controls.Add(screenHost, 0, 2);
            foreach (var page in new[] { overviewPage, settingsPage, logsPage })
            {
                page.Dock = DockStyle.Fill;
                page.AutoScroll = true;
                page.BackColor = Canvas;
                screenHost.Controls.Add(page);
            }
            versionCredit = CenterLabel(CurrentVersion.ToString(3) + " - Abdelrahman Sawaftah", 8, Muted, Canvas);
            versionCredit.RightToLeft = RightToLeft.No;
            root.Controls.Add(versionCredit, 0, 3);
            BuildOverview();
            BuildSettings();
            BuildLogs();
            ShowScreen(overviewPage, overviewNav);
            overviewNav.Click += (sender, e) =>
            {
                if (ResolveUnsavedSettings())
                    ShowScreen(overviewPage, overviewNav);
            };
            logsNav.Click += (sender, e) =>
            {
                if (ResolveUnsavedSettings())
                    ShowScreen(logsPage, logsNav);
            };
            settingsNav.Click += (sender, e) =>
            {
                if (settingsPage.Visible)
                    return;
                using (var gate = new SettingsUnlockDialog())
                {
                    if (gate.ShowDialog(this) == DialogResult.OK)
                        ShowScreen(settingsPage, settingsNav);
                }
            };
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
            int d = Math.Min(updateInfo.Width, updateInfo.Height);
            if (d <= 0)
                return;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddEllipse((updateInfo.Width - d) / 2, (updateInfo.Height - d) / 2, d, d);
                var previous = updateInfo.Region;
                updateInfo.Region = new Region(path);
                if (previous != null)
                    previous.Dispose();
            }
        }
        void ShowScreen(Panel page, Button selected)
        {
            foreach (var candidate in new[] { overviewPage, settingsPage, logsPage })
                candidate.Visible = candidate == page;
            page.BringToFront();
            FitPageSize(page);
            foreach (var button in new[] { overviewNav, settingsNav, logsNav })
            {
                bool active = button == selected;
                ((NavButton)button).Selected = active;
                button.BackColor = Color.White;
                button.ForeColor = active ? Accent : Muted;
                button.Invalidate();
            }
            if (page == settingsPage)
                ArrangeSettings();
            RefreshInterface();
        }
        void FitPageSize(Panel page)
        {
            if (!pageSizingReady || WindowState != FormWindowState.Normal)
                return;
            var area = Screen.FromControl(this).WorkingArea;
            Size desired = page == overviewPage ? compactClient
                           : page == logsPage   ? new Size((int)(920 * pageScale), (int)(640 * pageScale))
                                                : new Size((int)(800 * pageScale), (int)(600 * pageScale));
            int bw = Width - ClientSize.Width, bh = Height - ClientSize.Height;
            desired.Width = Math.Min(desired.Width, Math.Max(MinimumSize.Width - bw, area.Width - bw - 16));
            desired.Height = Math.Min(desired.Height, Math.Max(MinimumSize.Height - bh, area.Height - bh - 16));
            var center = new Point(Left + Width / 2, Top + Height / 2);
            ClientSize = desired;
            Left = Math.Max(area.Left, Math.Min(center.X - Width / 2, area.Right - Width));
            Top = Math.Max(area.Top, Math.Min(center.Y - Height / 2, area.Bottom - Height));
        }
        void BuildOverview()
        {
            var host = new TableLayoutPanel {
                Dock = DockStyle.Fill,   ColumnCount = 1,   RowCount = 3, Padding = new Padding(0, 12, 0, 0),
                Margin = new Padding(0), BackColor = Canvas
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            overviewPage.Controls.Add(host);
            host.Controls.Add(CenterLabel("الوزن الحالي", 9.5f, Muted, Canvas), 0, 0);
            weight.Text = "كغ";
            weight.Font = new Font("Segoe UI", 48, FontStyle.Bold);
            weight.ForeColor = Ink;
            weight.BackColor = Canvas;
            weight.Dock = DockStyle.Fill;
            weight.Margin = new Padding(0);
            weight.RightToLeft = RightToLeft.No;
            weight.AccessibleName = "الوزن الحالي بالكيلوغرام";
            host.Controls.Add(weight, 0, 1);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
                                                RightToLeft = RightToLeft.Yes, Margin = new Padding(0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            StyleButton(start, "تشغيل", true);
            StyleButton(stop, "إيقاف", false);
            stop.Enabled = false;
            start.Dock = DockStyle.Fill;
            stop.Dock = DockStyle.Fill;
            start.Margin = new Padding(0, 6, 0, 6);
            stop.Margin = new Padding(8, 6, 8, 6);
            operationIndicator.Text = "متوقف";
            operationIndicator.Font = UiTheme.Body;
            operationIndicator.AccessibleRole = AccessibleRole.StaticText;
            operationIndicator.TabStop = false;
            operationIndicator.Dock = DockStyle.Fill;
            operationIndicator.Margin = new Padding(0, 6, 0, 6);
            operationIndicator.RightToLeft = RightToLeft.Yes;
            footer.Controls.Add(start, 0, 0);
            footer.Controls.Add(stop, 1, 0);
            footer.Controls.Add(operationIndicator, 2, 0);
            host.Controls.Add(footer, 0, 2);
        }
        void BuildSettings()
        {
            settingsOuter =
                new TableLayoutPanel { Dock = DockStyle.Top,   Height = 392,       ColumnCount = 1,
                                       RowCount = 1,           BackColor = Canvas, Padding = new Padding(0, 8, 0, 0),
                                       Margin = new Padding(0) };
            settingsOuter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settingsOuter.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            settingsViewport.Dock = DockStyle.Fill;
            settingsViewport.AutoScroll = true;
            settingsViewport.BackColor = Canvas;
            settingsViewport.Controls.Add(settingsOuter);
            settingsPage.Controls.Add(settingsViewport);
            fields.Dock = DockStyle.Fill;
            fields.BackColor = Canvas;
            fields.ColumnCount = 2;
            fields.RowCount = 1;
            fields.Margin = new Padding(0);
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            settingsOuter.Controls.Add(fields, 0, 0);
            ConfigureNumber(baud, 300, 115200);
            ConfigureNumber(bits, 5, 8);
            ConfigureNumber(interval, 1000, 1000);
            ConfigureNumber(fresh, 1000, 30000);
            parity.DropDownStyle = ComboBoxStyle.DropDownList;
            parity.Items.AddRange(new object[] { "None", "Even", "Odd", "Mark", "Space" });
            parity.FlatStyle = FlatStyle.Popup;
            parity.DrawMode = DrawMode.OwnerDrawFixed;
            parity.FontChanged += (sender, e) => parity.ItemHeight = Math.Max(18, parity.Font.Height + 2);
            parity.DrawItem += (sender, e) =>
            {
                e.DrawBackground();
                string value = e.Index < 0 ? parity.Text : Convert.ToString(parity.Items[e.Index]);
                TextRenderer.DrawText(e.Graphics, UiTheme.ParityName(value), e.Font, e.Bounds, e.ForeColor,
                                      TextFormatFlags.RightToLeft | TextFormatFlags.HorizontalCenter |
                                          TextFormatFlags.VerticalCenter);
                e.DrawFocusRectangle();
            };
            var serial = SettingsCard("إعدادات الميزان", 0);
            var server = SettingsCard("ربط النظام", 1);
            MinimalField(serial, "منفذ الميزان", port);
            MinimalField(serial, "سرعة الاتصال", baud);
            MinimalField(serial, "بتات البيانات", bits);
            MinimalField(serial, "التكافؤ", parity);
            MinimalField(serial, "صلاحية القراءة (مللي ثانية)", fresh);
            MinimalField(server, "عنوان الإرسال", endpoint);
            MinimalField(server, "رقم الشركة", company);
            token.UseSystemPasswordChar = true;
            MinimalField(server, "رمز الوصول", token);
            port.DropDownStyle = ComboBoxStyle.DropDown;
            port.FlatStyle = FlatStyle.Flat;
            port.DropDown += (sender, e) => RefreshPorts();
            helpTips.SetToolTip(port, "اختر منفذ الميزان أو أدخل اسمه، مثل COM6.");
            endpoint.TextAlign = HorizontalAlignment.Left;
            endpoint.AccessibleDescription = "عنوان HTTPS الكامل للنظام الأساسي";
            endpoint.DoubleClick += (sender, e) => EditEndpoint();
            helpTips.SetToolTip(endpoint, "انقر مرتين لعرض العنوان وتحريره كاملًا.");
            StyleButton(save, "حفظ وتطبيق", true);
            var diagnose = new ActionButton();
            StyleButton(diagnose, "تشخيص الاتصال", false);
            diagnose.Width = 152;
            diagnose.Click += async (sender, e) => await ShowDiagnostics(diagnose);
            var settingsActions = Centered(Buttons(save, diagnose));
            settingsActions.Dock = DockStyle.Bottom;
            settingsActions.Height = 52;
            settingsPage.Controls.Add(settingsActions);
            settingsPage.SizeChanged += (sender, e) => ArrangeSettings();
            settingsViewport.SizeChanged += (sender, e) => ArrangeSettings();
            FontChanged += (sender, e) => ArrangeSettings();
        }
        TableLayoutPanel SettingsCard(string title, int column)
        {
            var card = new SurfacePanel { Dock = DockStyle.Fill, Surface = Color.White, BorderColor = UiTheme.Border,
                                          Padding = new Padding(14, 8, 14, 8), Margin = new Padding(5, 0, 5, 4) };
            if (column == 0)
                serialCard = card;
            else
                serverCard = card;
            var content =
                new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var titleLabel = UiTheme.Label(title, new Font("Segoe UI", 13, FontStyle.Bold));
            content.Controls.Add(titleLabel, 0, 0);
            var grid = new TableLayoutPanel { Dock = DockStyle.Top,    ColumnCount = 1, RowCount = 0,
                                              BackColor = Color.White, AutoSize = true, Margin = new Padding(0) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.Controls.Add(grid, 0, 1);
            card.Controls.Add(content);
            fields.Controls.Add(card, column, 0);
            return grid;
        }
        void MinimalField(TableLayoutPanel grid, string title, Control input)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            var field = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                                               BackColor = Color.White, Margin = new Padding(0, 0, 0, 8) };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var label = UiTheme.Label(title, UiTheme.Small);
            label.ForeColor = Muted;
            field.Controls.Add(label, 0, 0);
            var host = new InputSurface { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 26, 0),
                                          Margin = new Padding(0, 0, 0, 4), AccessibleName = title };
            input.Dock = DockStyle.None;
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            input.Font = UiTheme.Body;
            input.RightToLeft = RightToLeft.No;
            input.AccessibleName = title;
            input.BackColor = Color.White;
            input.ForeColor = Ink;
            input.Margin = new Padding(0);
            var text = input as TextBox;
            if (text != null)
            {
                text.BorderStyle = BorderStyle.None;
                text.TextAlign = HorizontalAlignment.Left;
            }
            var number = input as NumericUpDown;
            if (number != null)
            {
                number.BorderStyle = BorderStyle.None;
                number.TextAlign = HorizontalAlignment.Left;
            }
            var choice = input as ComboBox;
            if (choice != null)
                choice.FlatStyle =
                    choice.DropDownStyle == ComboBoxStyle.DropDownList ? FlatStyle.Popup : FlatStyle.Flat;
            host.Controls.Add(input);
            host.Layout += (sender, e) =>
                input.SetBounds(host.Padding.Left, Math.Max(0, (host.ClientSize.Height - input.Height) / 2),
                                Math.Max(1, host.ClientSize.Width - host.Padding.Horizontal), input.Height);
            input.Enter += (sender, e) => host.Invalidate();
            input.Leave += (sender, e) => host.Invalidate();
            field.Controls.Add(host, 0, 1);
            grid.Controls.Add(field, 0, row);
        }
        void ArrangeSettings()
        {
            if (serialCard == null || serverCard == null || settingsOuter == null || fields.ClientSize.Width <= 0)
                return;
            float scale = Math.Max(pageScale, Font.Height / (float)UiTheme.Body.Height);
            bool stacked = fields.ClientSize.Width < 640 * scale;
            int sh = (int)(384 * scale), rh = (int)(256 * scale);
            fields.SuspendLayout();
            fields.ColumnStyles.Clear();
            fields.RowStyles.Clear();
            fields.ColumnCount = stacked ? 1 : 2;
            fields.RowCount = stacked ? 2 : 1;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, stacked ? 100 : 50));
            if (!stacked)
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.SetCellPosition(serialCard, new TableLayoutPanelCellPosition(0, 0));
            fields.SetCellPosition(serverCard, new TableLayoutPanelCellPosition(stacked ? 0 : 1, stacked ? 1 : 0));
            if (stacked)
            {
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, sh));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, rh));
            }
            else
                fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            settingsOuter.Height = (stacked ? sh + rh : sh) + (int)(8 * scale);
            serverCard.Dock = stacked ? DockStyle.Fill : DockStyle.Top;
            if (!stacked)
                serverCard.Height = Math.Max(1, rh - serverCard.Margin.Vertical);
            fields.ResumeLayout();
        }
        void EditEndpoint()
        {
            using (var dialog = new Form())
            {
                UiTheme.Dialog(dialog, "عنوان الإرسال", new Size(580, 160));
                var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                                                  Padding = new Padding(20, 12, 20, 12) };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                root.Controls.Add(UiTheme.Label("عنوان الإرسال الكامل", UiTheme.Strong), 0, 0);
                var address = new TextBox { Text = endpoint.Text,           RightToLeft = RightToLeft.No,
                                            BackColor = Color.White,        ForeColor = Ink,
                                            BorderStyle = BorderStyle.None, AccessibleName = "عنوان الإرسال الكامل" };
                var addressHost = new InputSurface { Dock = DockStyle.Top, Height = 36,
                                                     Padding = new Padding(10, 0, 10, 0), Margin = new Padding(0) };
                addressHost.Controls.Add(address);
                addressHost.Layout += (sender, e) => address.SetBounds(
                    addressHost.Padding.Left, Math.Max(0, (addressHost.ClientSize.Height - address.Height) / 2),
                    Math.Max(1, addressHost.ClientSize.Width - addressHost.Padding.Horizontal), address.Height);
                address.Enter += (sender, e) => addressHost.Invalidate();
                address.Leave += (sender, e) => addressHost.Invalidate();
                root.Controls.Add(addressHost, 0, 1);
                var apply = new ActionButton();
                StyleButton(apply, "تطبيق", true);
                apply.DialogResult = DialogResult.OK;
                apply.Enabled = !running;
                var cancel = new ActionButton();
                StyleButton(cancel, "إلغاء", false);
                cancel.DialogResult = DialogResult.Cancel;
                root.Controls.Add(Centered(Buttons(apply, cancel)), 0, 2);
                dialog.Controls.Add(root);
                dialog.AcceptButton = apply;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    endpoint.Text = address.Text.Trim();
            }
        }
        NavButton readingsView, eventsView;
        void SelectLogView(bool events)
        {
            readingGrid.Visible = !events;
            eventGrid.Visible = events;
            (events ? eventGrid : readingGrid).BringToFront();
            readingsView.Selected = !events;
            eventsView.Selected = events;
            readingsView.ForeColor = events ? Muted : Accent;
            eventsView.ForeColor = events ? Accent : Muted;
            readingsView.Invalidate();
            eventsView.Invalidate();
            logFilter.Enabled = events;
            activeLogGrid = events ? eventGrid : readingGrid;
        }
        void BuildLogs()
        {
            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,   ColumnCount = 1,   RowCount = 3, Padding = new Padding(0, 12, 0, 0),
                Margin = new Padding(0), BackColor = Canvas
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            logsPage.Controls.Add(root);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill,
                                              AutoSize = true,
                                              AutoSizeMode = AutoSizeMode.GrowAndShrink,
                                              MinimumSize = new Size(0, 42),
                                              FlowDirection = FlowDirection.RightToLeft,
                                              RightToLeft = RightToLeft.No,
                                              WrapContents = true,
                                              Margin = new Padding(0, 0, 0, 8) };
            readingsView = new NavButton();
            eventsView = new NavButton();
            StyleButton(readingsView, "القراءات", false);
            StyleButton(eventsView, "الأحداث", false);
            readingsView.Size = eventsView.Size = new Size(86, 34);
            readingsView.Margin = eventsView.Margin = new Padding(0, 2, 0, 2);
            readingsView.Click += (sender, e) => SelectLogView(false);
            eventsView.Click += (sender, e) => SelectLogView(true);
            tools.Controls.Add(readingsView);
            tools.Controls.Add(eventsView);
            logSearch.Font = UiTheme.Body;
            logSearch.BorderStyle = BorderStyle.None;
            logSearch.AccessibleName = "البحث في السجلات";
            logSearch.RightToLeft = RightToLeft.Yes;
            helpTips.SetToolTip(logSearch, "بحث في السجل");
            var searchBox = new InputSurface { Size = new Size(172, 32), Margin = new Padding(8, 3, 8, 3) };
            searchBox.Controls.Add(logSearch);
            searchBox.Layout += (sender, e) => logSearch.SetBounds(8, (searchBox.Height - logSearch.Height) / 2,
                                                                   Math.Max(1, searchBox.Width - 16), logSearch.Height);
            logSearch.Enter += (sender, e) => searchBox.Invalidate();
            logSearch.Leave += (sender, e) => searchBox.Invalidate();
            var searchHint = new Label { Text = "بحث في السجل",
                                         Font = UiTheme.Small,
                                         ForeColor = Muted,
                                         BackColor = Color.White,
                                         TextAlign = ContentAlignment.MiddleRight,
                                         RightToLeft = RightToLeft.Yes,
                                         Cursor = Cursors.IBeam };
            searchBox.Controls.Add(searchHint);
            searchHint.BringToFront();
            searchHint.Click += (sender, e) => logSearch.Focus();
            searchBox.Layout += (sender, e) =>
                searchHint.SetBounds(8, 2, Math.Max(1, searchBox.Width - 16), Math.Max(1, searchBox.Height - 4));
            logSearch.TextChanged += (sender, e) => searchHint.Visible =
                logSearch.TextLength == 0 && !logSearch.Focused;
            logSearch.Enter += (sender, e) => searchHint.Visible = logSearch.TextLength == 0;
            logSearch.Leave += (sender, e) => searchHint.Visible = logSearch.TextLength == 0;
            tools.Controls.Add(searchBox);
            logFilter.Font = UiTheme.Body;
            logFilter.Width = 136;
            logFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            logFilter.FlatStyle = FlatStyle.Popup;
            logFilter.Items.AddRange(new object[] { "جميع الأحداث", "الأعطال فقط" });
            logFilter.SelectedIndex = 0;
            logFilter.AccessibleName = "تصفية الأحداث";
            logFilter.Margin = new Padding(4, 4, 4, 3);
            logFilter.RightToLeft = RightToLeft.Yes;
            tools.Controls.Add(logFilter);
            var follow = new ActionButton();
            StyleButton(follow, "متابعة تلقائية", false);
            follow.AccessibleName = "تبديل متابعة السجل";
            follow.Size = new Size(112, 32);
            follow.Margin = new Padding(4, 2, 4, 2);
            follow.Click += (sender, e) =>
            {
                followLogs = !followLogs;
                follow.Text = followLogs ? "متابعة تلقائية" : "المتابعة متوقفة";
            };
            tools.Controls.Add(follow);
            logSearch.TextChanged += (sender, e) => FilterLogs();
            logFilter.SelectedIndexChanged += (sender, e) => FilterLogs();
            root.Controls.Add(tools, 0, 0);
            ConfigureLogGrid(readingGrid);
            readingGrid.Columns.Add("time", "الوقت");
            readingGrid.Columns.Add("weight", "الوزن (كغ)");
            readingGrid.Columns.Add("frame", "إطار القراءة");
            readingGrid.RightToLeft = RightToLeft.No;
            readingGrid.Columns[0].DisplayIndex = 2;
            readingGrid.Columns[1].DisplayIndex = 1;
            readingGrid.Columns[2].DisplayIndex = 0;
            readingGrid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            readingGrid.AccessibleName = "سجل القراءات";
            ((LogGrid)readingGrid).EmptyText = "بانتظار وصول القراءات";
            ConfigureLogGrid(eventGrid);
            eventGrid.Columns.Add("time", "الوقت");
            eventGrid.Columns.Add("type", "الحدث");
            eventGrid.Columns.Add("detail", "التفاصيل");
            eventGrid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            eventGrid.Columns[2].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            eventGrid.AccessibleName = "سجل الأحداث والأعطال";
            ((LogGrid)eventGrid).EmptyText = "لا توجد أحداث مسجلة";
            foreach (var grid in new[] { readingGrid, eventGrid })
            {
                grid.Columns[0].FillWeight = 20;
                grid.Columns[1].FillWeight = 22;
                grid.Columns[2].FillWeight = 58;
            }
            var tableHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0) };
            tableHost.Controls.Add(readingGrid);
            tableHost.Controls.Add(eventGrid);
            root.Controls.Add(tableHost, 0, 1);
            var footer =
                new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var open = new ActionButton();
            var copy = new ActionButton();
            var diagnose = new ActionButton();
            StyleButton(open, "مجلد السجل", false);
            StyleButton(copy, "نسخ المحدد", false);
            StyleButton(diagnose, "تشخيص الاتصال", false);
            diagnose.Width = 152;
            open.Width = 104;
            copy.Width = 104;
            diagnose.Width = 152;
            open.Click += (sender, e) =>
            {
                try
                {
                    Directory.CreateDirectory(dataDir);
                    Process.Start("explorer.exe", "\"" + dataDir + "\"");
                }
                catch
                {
                    AppMessage.Show(this, "تعذر فتح مجلد السجلات.", "السجلات");
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
                catch
                {
                    AppMessage.Show(this, "تعذر نسخ الصفوف المحددة.", "السجل");
                }
            };
            diagnose.Click += async (sender, e) => await ShowDiagnostics(diagnose);
            var buttons = Buttons(diagnose, copy, open);
            buttons.Anchor = AnchorStyles.Right;
            footer.Controls.Add(buttons, 1, 0);
            logTimes.Dock = DockStyle.Fill;
            logTimes.Font = UiTheme.Small;
            logTimes.ForeColor = Muted;
            logTimes.AutoEllipsis = true;
            logTimes.TextAlign = ContentAlignment.MiddleCenter;
            footer.Controls.Add(logTimes, 0, 0);
            root.Controls.Add(footer, 0, 2);
            SelectLogView(false);
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
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = Color.FromArgb(231, 237, 242);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.Soft;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            grid.ColumnHeadersDefaultCellStyle.Font = UiTheme.Strong;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.DefaultCellStyle.Font = UiTheme.Body;
            grid.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.DefaultCellStyle.ForeColor = Ink;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(229, 241, 241);
            grid.DefaultCellStyle.SelectionForeColor = Ink;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            grid.RowTemplate.Height = 30;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            grid.Enter += (sender, e) => activeLogGrid = grid;
            grid.FontChanged += (sender, e) =>
            {
                grid.DefaultCellStyle.Font = grid.Font;
                grid.ColumnHeadersDefaultCellStyle.Font =
                    grid.Font.Size == UiTheme.Body.Size ? UiTheme.Strong : new Font(grid.Font, FontStyle.Bold);
            };
        }
        void RefreshInterface()
        {
            if (!running && !closingInProgress)
                start.Enabled = serialSession != null && serialSession.Completion.IsCompleted;
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
            bool fault =
                updateInstallFailed || settingsNeedRepair || primaryCredentialUnavailable ||
                (running && (primary.WriteFailed || readingState == "انقطع المنفذ" || readingState == "لا تصل قراءة" ||
                             readingState == "قراءة غير صالحة" || readingState == "وحدة غير مدعومة" ||
                             readingState == "إطار أطول من الحد المسموح"));
            if (updateInstallFailed && !running)
                operationIndicator.Text = "متوقف بعد فشل التحديث";
            if (primaryCredentialUnavailable && !running)
                operationIndicator.Text = "راجع رمز النظام";
            operationIndicator.BackColor = active    ? Color.FromArgb(226, 244, 235)
                                           : fault   ? Color.FromArgb(251, 233, 237)
                                           : running ? Color.FromArgb(255, 244, 214)
                                                     : Color.FromArgb(231, 237, 242);
            operationIndicator.ForeColor = active    ? Color.FromArgb(20, 99, 68)
                                           : fault   ? UiTheme.Danger
                                           : running ? Color.FromArgb(128, 88, 16)
                                                     : UiTheme.Muted;
            operationIndicator.AccessibleName = "حالة القبان: " + operationIndicator.Text;
        }
    }
}
