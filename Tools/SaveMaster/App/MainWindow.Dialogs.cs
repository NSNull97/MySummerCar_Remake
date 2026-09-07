using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using MySummerRemake.SaveMaster.Core;

namespace MySummerRemake.SaveMaster;

internal sealed partial class MainWindow
{
    private static string NativeSaveDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow",
        "DefaultCompany", "MySummerCar_Remake", "Saves", "Native");

    private void SaveReviewed(bool copy)
    {
        if (isOpening || session?.CanEdit != true || !copy && !session.IsDirty) return;
        Run(() =>
        {
            var issues = session.Validate();
            if (issues.Any(i => i.Severity == ValidationSeverity.Error))
            {
                ShowValidation();
                SetStatus("Сохранение заблокировано: сначала исправьте ошибки проверки.", true);
                return;
            }
            if (!ReviewChanges(copy ? "Сохранить копию" : "Сохранить с резервной копией", true)) return;
            SaveWriteResult result;
            if (copy)
            {
                using var dialog = new SaveFileDialog
                {
                    Title = "Новый файл — существующие файлы не перезаписываются",
                    Filter = "Сохранение ремейка|*.save.json", DefaultExt = "save.json", AddExtension = true,
                    FileName = "edited-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".save.json",
                    InitialDirectory = Path.GetDirectoryName(session.FilePath), OverwritePrompt = true
                };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                result = session.SaveAs(dialog.FileName);
            }
            else
            {
                EnsureGameClosed();
                result = session.Save();
            }
            AfterChange();
            SetStatus("Сохранено: " + result.Path);
            MessageBox.Show(this, "Сохранено и повторно проверено.\n\n" + result.Path +
                (string.IsNullOrEmpty(result.BackupPath) ? "\n\nСоздан отдельный файл. Исходный сейв сохранён." : "\n\nРезервная копия:\n" + result.BackupPath),
                "Сохранение готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private static void EnsureGameClosed()
    {
        // Guard recognized player builds. Hash conflict protection in the core also covers writes by other tools.
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string name;
                try { name = process.ProcessName; }
                catch (InvalidOperationException) { continue; }
                if (name.Equals("MySummerCar_Remake", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("My Summer Car Remake", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("MySummerCar_Remake_Game", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Закройте запущенный ремейк перед записью в его сейв. Игра может перезаписать правки своим состоянием из памяти. Сейчас можно сохранить отдельную копию.");
            }
        }
    }

    private void ReviewOnly()
    {
        if (!isOpening && session is not null) ReviewChanges("Изменения в рабочей копии", false);
    }

    private bool ReviewChanges(string title, bool saving)
    {
        if (session is null) return false;
        using var dialog = Dialog(title, 1140, 700);
        var layout = DialogLayout(dialog, 78);
        var info = Theme.Label($"{session.Changes.Count:N0} изменённых полей\n" +
            (saving ? "Проверьте список. Запись выполняется после нажатия зелёной кнопки. Закройте игру перед заменой её сейва." : "Было — значение при открытии файла. Стало — ваша рабочая копия."), 11);
        info.Dock = DockStyle.Fill;
        layout.Controls.Add(info, 0, 0);
        var review = Theme.Grid();
        Theme.Column(review, "РАЗДЕЛ / ПОЛЕ", "field", 48);
        Theme.Column(review, "БЫЛО", "before", 26);
        Theme.Column(review, "СТАЛО", "after", 26);
        foreach (var change in session.Changes) review.Rows.Add(change.DomainId + change.JsonPointer, change.Before, change.After);
        layout.Controls.Add(review, 0, 1);
        var buttons = DialogButtons(layout);
        if (saving)
        {
            var confirm = Theme.Button("Подтвердить сохранение", (_, _) => dialog.DialogResult = DialogResult.OK, true, 260);
            buttons.Controls.Add(confirm);
        }
        buttons.Controls.Add(Theme.Button(saving ? "Отмена" : "Закрыть", (_, _) => dialog.DialogResult = DialogResult.Cancel, false, 120));
        return dialog.ShowDialog(this) == DialogResult.OK;
    }

    private void ShowValidation()
    {
        if (isOpening || session is null) return;
        Run(() =>
        {
            var issues = session.Validate();
            using var dialog = Dialog("Проверка сохранения", 1140, 690);
            var layout = DialogLayout(dialog, 72);
            var info = Theme.Label(issues.Count == 0
                ? "Ошибок локальной проверки не найдено.\nПроверены поддерживаемые правила редактора; полную проверку контента выполняет игра при загрузке."
                : $"Ошибок: {issues.Count(i => i.Severity == ValidationSeverity.Error)}  •  Предупреждений: {issues.Count(i => i.Severity != ValidationSeverity.Error)}\nДвойной щелчок по строке откроет соответствующий раздел.", 11);
            info.Dock = DockStyle.Fill;
            layout.Controls.Add(info, 0, 0);
            var results = Theme.Grid();
            Theme.Column(results, "УРОВЕНЬ", "level", 13);
            Theme.Column(results, "ПОЛЕ", "path", 32);
            Theme.Column(results, "ПРОВЕРКА", "message", 55);
            foreach (var issue in issues) results.Rows.Add(issue.Severity == ValidationSeverity.Error ? "Ошибка" : "Внимание", issue.DomainId + issue.JsonPointer, issue.Message);
            results.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= issues.Count) return;
                var issue = issues[e.RowIndex];
                dialog.Close();
                search.Text = issue.DomainId + issue.JsonPointer;
                // Search indexes also contain domain and pointer separately.
                if (issue.JsonPointer.Length > 0) search.Text = issue.JsonPointer;
                else search.Text = issue.DomainId;
            };
            layout.Controls.Add(results, 0, 1);
            DialogButtons(layout).Controls.Add(Theme.Button("Закрыть", (_, _) => dialog.Close()));
            dialog.ShowDialog(this);
        });
    }

    private async Task ShowSlotsAsync()
    {
        if (isOpening) return;
        string? pathToOpen = null;
        bool pickAnotherFile = false;
        Run(() =>
        {
            using var dialog = Dialog("Мои сохранения", 1100, 600);
            var layout = DialogLayout(dialog, 65);
            var info = Theme.Label("Стандартная папка ремейка. Для другой сборки или резервной копии используйте «Открыть…».\n" + NativeSaveDirectory, 10);
            info.Dock = DockStyle.Fill;
            layout.Controls.Add(info, 0, 0);
            var slots = Theme.Grid();
            Theme.Column(slots, "СЛОТ", "slot", 22);
            Theme.Column(slots, "ИЗМЕНЁН", "date", 26);
            Theme.Column(slots, "РАЗМЕР", "size", 18);
            Theme.Column(slots, "ФАЙЛ", "path", 34);
            var files = Directory.Exists(NativeSaveDirectory)
                ? Directory.EnumerateDirectories(NativeSaveDirectory).Select(d => Path.Combine(d, "current.save.json")).Where(File.Exists).Order().ToArray()
                : [];
            foreach (var path in files)
            {
                var file = new FileInfo(path);
                slots.Rows.Add(Path.GetFileName(Path.GetDirectoryName(path)) ?? "", file.LastWriteTime.ToString("dd.MM.yyyy HH:mm"), $"{file.Length / 1024.0:N1} КБ", file.Name);
            }
            string? selected = null;
            void Choose()
            {
                if (slots.CurrentCell is not { RowIndex: >= 0 } cell || cell.RowIndex >= files.Length) return;
                selected = files[cell.RowIndex];
                dialog.DialogResult = DialogResult.OK;
            }
            slots.CellDoubleClick += (_, _) => Choose();
            layout.Controls.Add(slots, 0, 1);
            var buttons = DialogButtons(layout);
            buttons.Controls.Add(Theme.Button("Открыть слот", (_, _) => Choose(), true, 155));
            buttons.Controls.Add(Theme.Button("Другой файл…", (_, _) => { dialog.DialogResult = DialogResult.Retry; }, false, 155));
            var result = dialog.ShowDialog(this);
            if (result == DialogResult.OK && selected is not null) pathToOpen = selected;
            if (result == DialogResult.Retry) pickAnotherFile = true;
        });
        if (pathToOpen is not null) await OpenFileAsync(pathToOpen);
        if (pickAnotherFile) await PickFileAsync();
    }

    private void EditRaw()
    {
        if (isOpening || session is null || activeDomain.Length == 0) return;
        var domain = session.Domains.First(d => d.Id == activeDomain);
        var editable = session.CanEditDomain(domain.Id);
        using var dialog = Dialog("JSON — " + SchemaCatalog.DomainTitle(domain.Id), 1090, 780);
        var layout = DialogLayout(dialog, 68);
        var info = Theme.Label("Расширенный редактор всего раздела, включая коллекции. Идентичность и версии защищены.\nИзменение попадёт в рабочую копию одной операцией; запись в файл проходит отдельную проверку.", 10);
        info.Dock = DockStyle.Fill;
        layout.Controls.Add(info, 0, 0);
        var json = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, AcceptsTab = true,
            ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10),
            MaxLength = 2 * 1024 * 1024, ReadOnly = !editable,
            Text = domain.Payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            BackColor = Theme.Paper, ForeColor = Theme.Ink
        };
        layout.Controls.Add(json, 0, 1);
        var buttons = DialogButtons(layout);
        if (editable)
        {
            buttons.Controls.Add(Theme.Button("Применить JSON", (_, _) => Run(() =>
            {
                session.SetJson(domain.Id, "", json.Text);
                var priorDomain = activeDomain;
                domainSnapshots = session.Domains;
                RebuildTree();
                tree.SelectedNode = tree.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Tag is NodeTarget t && t.Domain == priorDomain && t.Pointer == "");
                AfterChange();
                dialog.Close();
                SetStatus("JSON применён к рабочей копии. Проверьте список изменений перед сохранением.");
            }), true, 190));
        }
        buttons.Controls.Add(Theme.Button("Закрыть", (_, _) => dialog.Close()));
        dialog.ShowDialog(this);
    }

    private void ShowWorkshop(string? renderPath = null)
    {
        if (isOpening || session is null) return;
        var domain = session.Domains.FirstOrDefault(d => d.Id == "vehicle.satsuma");
        if (domain?.Payload["vehicles"] is not JsonArray vehicles || vehicles.Count == 0)
        {
            MessageBox.Show(this, "В этом сохранении нет записей Satsuma.", "Мастерская");
            return;
        }
        using var dialog = Dialog("Мастерская Satsuma", 890, 710);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 5 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        dialog.Controls.Add(outer);
        var help = Theme.Label("Крепёж и проводка\nИзменения обратимы до сохранения. Крепёж затягивается по определениям ремейка; отсутствующие детали не создаются.", 12, true);
        help.Dock = DockStyle.Fill;
        outer.Controls.Add(help, 0, 0);
        var vehiclePicker = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        for (var i = 0; i < vehicles.Count; i++) vehiclePicker.Items.Add($"Satsuma {i + 1}  •  {vehicles[i]?["stableVehicleId"] ?? vehicles[i]?["stableEntityId"] ?? vehicles[i]?["vehicleStableEntityId"]}");
        vehiclePicker.SelectedIndex = 0;
        outer.Controls.Add(vehiclePicker, 0, 1);
        var bolts = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        void SetBolts(bool tighten) => Run(() =>
        {
            var current = session.Domains.First(d => d.Id == domain.Id);
            var updated = SchemaActions.SetFasteners(current.Payload, vehiclePicker.SelectedIndex, tighten);
            session.SetJson(domain.Id, "", updated.ToJsonString());
            AfterChange();
            SetStatus(tighten ? "Крепёж изменён в рабочей копии: затяжка по индивидуальным пределам." : "Крепёж ослаблен в рабочей копии. Проверьте перед сохранением.");
            dialog.Close();
        });
        bolts.Controls.Add(Theme.Button("Затянуть крепёж", (_, _) => SetBolts(true), true, 205));
        bolts.Controls.Add(Theme.Button("Ослабить крепёж", (_, _) => SetBolts(false), false, 205));
        outer.Controls.Add(bolts, 0, 2);
        var wiringPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        wiringPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        wiringPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        wiringPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        var wiringTitle = Theme.Label("Соединения проводки — выберите нужные", 11, true);
        wiringTitle.Dock = DockStyle.Fill;
        wiringPanel.Controls.Add(wiringTitle, 0, 0);
        var wires = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false };
        foreach (var pair in SchemaCatalog.WireConnections) wires.Items.Add(new Choice(pair.Key, pair.Value));
        wiringPanel.Controls.Add(wires, 0, 1);
        var terminals = new CheckBox { Text = "Затянуть клеммы подключённых кабелей (0–8)", AutoSize = true };
        wiringPanel.Controls.Add(terminals, 0, 2);
        void LoadWires()
        {
            var electrical = vehicles[vehiclePicker.SelectedIndex]?["electrical"];
            var connected = (electrical?["installedConnectionIds"] as JsonArray)?.Select(n => n?.ToString() ?? "").ToHashSet() ?? [];
            for (var i = 0; i < wires.Items.Count; i++) wires.SetItemChecked(i, connected.Contains(((Choice)wires.Items[i]).Value));
            wires.Enabled = electrical is not null;
            terminals.Enabled = electrical is not null;
        }
        LoadWires();
        vehiclePicker.SelectedIndexChanged += (_, _) => LoadWires();
        outer.Controls.Add(wiringPanel, 0, 3);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0), WrapContents = false };
        footer.Controls.Add(Theme.Button("Выбрать все", (_, _) => { for (var i = 0; i < wires.Items.Count; i++) wires.SetItemChecked(i, true); }, false, 148));
        footer.Controls.Add(Theme.Button("Снять все", (_, _) => { for (var i = 0; i < wires.Items.Count; i++) wires.SetItemChecked(i, false); }, false, 123));
        var applyWiring = Theme.Button("Применить проводку", (_, _) => Run(() =>
        {
            var current = session.Domains.First(d => d.Id == domain.Id);
            var updated = SchemaActions.SetWiring(current.Payload, vehiclePicker.SelectedIndex, wires.CheckedItems.Cast<Choice>().Select(c => c.Value), terminals.Checked);
            session.SetJson(domain.Id, "", updated.ToJsonString());
            AfterChange();
            dialog.Close();
            SetStatus("Проводка изменена в рабочей копии. Исходный сейв ещё не записан.");
        }), true, 225);
        applyWiring.Enabled = wires.Enabled;
        vehiclePicker.SelectedIndexChanged += (_, _) => applyWiring.Enabled = wires.Enabled;
        footer.Controls.Add(applyWiring);
        footer.Controls.Add(Theme.Button("Закрыть", (_, _) => dialog.Close()));
        outer.Controls.Add(footer, 0, 4);
        if (renderPath is null) dialog.ShowDialog(this);
        else
        {
            dialog.Show(this);
            dialog.Refresh();
            using var bitmap = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
            bitmap.Save(renderPath, System.Drawing.Imaging.ImageFormat.Png);
            dialog.Close();
        }
    }

    private void ShowHelp() => MessageBox.Show(this,
        "MY SUMMER REMAKE SAVE MASTER\n\n" +
        "1. Закройте игру. Откройте native-сейв через «Мои сейвы», «Открыть…» или перетащите файл.\n" +
        "2. Выберите раздел, найдите поле, введите значение и нажмите «Применить». Поиск ищет по всему сохранению.\n" +
        "3. В «Настройке авто» — регулировки, жидкости и износ. В «Мастерской» — крепёж и проводка. JSON раздела позволяет редактировать вложенные коллекции.\n" +
        "4. Нажмите «Проверить», затем «Сохранить…». Просмотрите все изменения и подтвердите запись.\n\n" +
        "Ctrl+O открыть · Ctrl+S сохранить · Ctrl+Shift+S копия\nCtrl+Z отменить · Ctrl+Y повторить · Ctrl+F поиск\n\n" +
        "Перед заменой создаётся отдельный бэкап рядом с файлом. Чтобы восстановить его, откройте бэкап, проверьте и сохраните копию в отдельный файл. При закрытой игре можно заменить current.save.json этой копией, предварительно сохранив текущий файл.\n\n" +
        $"Редактор работает с native-форматом ремейка, а не defaultES2File.txt оригинальной MSC. Версии {SaveSession.SupportedDocumentVersions} поддерживаются без миграции. Разделы и поля, которых игра ещё не сохраняет, редактор не придумывает. Проверка определения контента и совместимости всей сцены остаётся за игрой.\n\n" +
        "Никаких сетевых запросов или отправки сохранений.",
        "Как пользоваться", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void EditBalance()
    {
        if (isOpening || session?.CanEdit != true) return;
        Run(() =>
        {
            var economy = session.Domains.FirstOrDefault(d => d.Id == "economy.player");
            if (economy is null) throw new InvalidOperationException("В этом сохранении отсутствует раздел экономики.");
            using var dialog = Dialog("Изменить баланс", 820, 550);
            var layout = DialogLayout(dialog, 100);
            var info = Theme.Label("Деньги игрока\nВведите сумму в финских марках. 1 марка = 100 пенни.\nРедактор добавит согласованную операцию в журнал и сохранит прежнюю историю.", 12);
            info.Dock = DockStyle.Fill;
            layout.Controls.Add(info, 0, 0);
            var balance = new NumericUpDown
            {
                DecimalPlaces = 2, Minimum = 0, Maximum = 9_999_999_999m, Increment = 100,
                ThousandsSeparator = true, Width = 340, Font = new Font("Segoe UI", 18),
                Value = Math.Clamp((economy.Payload["balanceMinorUnits"]?.GetValue<long>() ?? 0) / 100m, 0, 9_999_999_999m)
            };
            layout.Controls.Add(balance, 0, 1);
            var buttons = DialogButtons(layout);
            buttons.Controls.Add(Theme.Button("Применить баланс", (_, _) => Run(() =>
            {
                session.SetBalance(decimal.ToInt64(balance.Value * 100));
                AfterChange();
                dialog.Close();
                SetStatus("Баланс и журнал операций изменены в рабочей копии.");
            }), true, 220));
            buttons.Controls.Add(Theme.Button("Отмена", (_, _) => dialog.Close()));
            dialog.ShowDialog(this);
        });
    }

    private void EditTime()
    {
        if (isOpening || session?.CanEdit != true) return;
        Run(() =>
        {
            var time = session.Domains.FirstOrDefault(d => d.Id == "core.time");
            if (time is null) throw new InvalidOperationException("В этом сохранении отсутствует раздел времени.");
            using var dialog = Dialog("Игровая дата и время", 820, 550);
            var layout = DialogLayout(dialog, 135);
            var info = Theme.Label("Календарь игры\nДата, номер дня и игровые тики меняются согласованно.\nСроки NPC, услуг и других систем остаются прежними: перенос времени может изменить доступность событий. Для небольших правок времени сохраняйте отдельную копию.", 11);
            info.Dock = DockStyle.Fill;
            layout.Controls.Add(info, 0, 0);
            var picker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy  HH:mm:ss", Width = 340, Font = new Font("Segoe UI", 15) };
            var root = time.Payload;
            if (root["year"] is not null && root["month"] is not null && root["day"] is not null)
            {
                var date = new DateTime(root["year"]!.GetValue<int>(), root["month"]!.GetValue<int>(), root["day"]!.GetValue<int>()).AddTicks(checked((root["timeOfDayTicks"]?.GetValue<long>() ?? 0) * 10));
                picker.Value = date;
            }
            layout.Controls.Add(picker, 0, 1);
            var buttons = DialogButtons(layout);
            buttons.Controls.Add(Theme.Button("Применить дату", (_, _) => Run(() =>
            {
                session.SetTime(picker.Value);
                AfterChange();
                dialog.Close();
                SetStatus("Игровой календарь изменён в рабочей копии. Проверьте зависимые события перед сохранением.");
            }), true, 210));
            buttons.Controls.Add(Theme.Button("Отмена", (_, _) => dialog.Close()));
            dialog.ShowDialog(this);
        });
    }

    private static Form Dialog(string title, int width, int height) => new()
    {
        Text = title, Size = new Size(width, height), MinimumSize = new Size(Math.Min(width, 820), 550),
        StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, BackColor = Theme.Background,
        ForeColor = Theme.Ink, Font = new Font("Segoe UI", 10), MinimizeBox = false
    };

    private static TableLayoutPanel DialogLayout(Form dialog, int headerHeight)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, headerHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        dialog.Controls.Add(layout);
        return layout;
    }

    private static FlowLayoutPanel DialogButtons(TableLayoutPanel layout)
    {
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 13, 0, 0), WrapContents = false };
        layout.Controls.Add(buttons, 0, 2);
        return buttons;
    }
}
