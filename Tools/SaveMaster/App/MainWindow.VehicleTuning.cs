using System.Globalization;
using System.Text.Json.Nodes;
using MySummerRemake.SaveMaster.Core;

namespace MySummerRemake.SaveMaster;

internal sealed partial class MainWindow
{
    private void ShowVehicleTuning(string? checkDirectory = null)
    {
        if (isOpening || session is null) return;
        SaveDomain? domain = session.Domains.FirstOrDefault(d => d.Id == "vehicle.satsuma");
        if (domain?.Payload["vehicles"] is not JsonArray vehicles || vehicles.Count == 0)
        {
            SetStatus("В этом сохранении нет машин для настройки.");
            return;
        }
        var payloads = session.Domains.ToDictionary(d => d.Id, d => d.Payload, StringComparer.Ordinal);
        var pending = new Dictionary<(string Domain, string Pointer), string>();
        IReadOnlyList<VehicleTuningField> fields = [];
        bool populating = false;
        using var dialog = Dialog("Настройка авто · Satsuma", 1220, 820);
        using var changedFont = new Font(dialog.Font, FontStyle.Bold);
        dialog.MinimumSize = new Size(1030, 720);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 6 };
        foreach (int height in new[] { 76, 43 }) outer.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (int height in new[] { 110, 43, 48 }) outer.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        dialog.Controls.Add(outer);
        var intro = Theme.Label("Регулировки и обслуживание\nМеняйте ячейки «Значение». Все выбранные правки применяются вместе; Ctrl+Z в главном окне отменит их одним шагом.", 11);
        intro.Dock = DockStyle.Fill;
        outer.Controls.Add(intro, 0, 0);
        var picker = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        for (int i = 0; i < vehicles.Count; i++) picker.Items.Add($"Satsuma {i + 1} · {vehicles[i]?["stableVehicleId"]}");
        outer.Controls.Add(picker, 0, 1);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var grids = new Dictionary<string, DataGridView>();
        var help = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
            BackColor = Theme.Background, ForeColor = Theme.Ink, ScrollBars = ScrollBars.Vertical, Margin = new Padding(3, 9, 3, 0) };
        var feedback = Theme.Label("Выберите параметр — здесь появится пояснение.", 10);
        feedback.Dock = DockStyle.Fill;
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        Button? useSuggested = null;
        Button? applyTuning = null;

        bool Editable(VehicleTuningField field) => field.IsEditable && session.CanEditDomain(field.DomainId);
        DataGridView? ActiveGrid() => tabs.SelectedTab?.Controls.OfType<DataGridView>().FirstOrDefault();
        void Detail()
        {
            if (ActiveGrid()?.CurrentRow?.Tag is not VehicleTuningField field) { help.Text = "В этой категории нет сохранённых полей."; if (useSuggested is not null) useSuggested.Enabled = false; return; }
            string range = field.IsBoolean ? "Да / нет" : $"{field.Min?.ToString("G", CultureInfo.InvariantCulture) ?? "—"} … {field.Max?.ToString("G", CultureInfo.InvariantCulture) ?? "без верхнего предела"} {field.Unit}";
            help.Text = field.Owner + " · " + field.Label + "\r\n" + field.Help + "\r\n" +
                (Editable(field) ? "Допустимо: " + range : "Только просмотр: " + (field.IsEditable ? "версия раздела не поддерживается." : "значение зависит от состояния игры."));
            if (useSuggested is not null) useSuggested.Enabled = Editable(field) && field.RecommendedValue.HasValue;
        }
        void UpdatePending()
        {
            feedback.ForeColor = Theme.Muted;
            feedback.Text = pending.Count == 0 ? "Новых правок нет. Недостающие состояния появятся после сохранения в актуальной игре." : $"Подготовлено правок: {pending.Count}. Файл будет записан отдельно через «Сохранить…».";
            if (applyTuning is not null) applyTuning.Enabled = pending.Count > 0;
        }
        foreach (string category in new[] { "Регулировки", "Жидкости", "Состояние", "Симуляция" })
        {
            var page = new TabPage(category) { BackColor = Theme.Paper, Padding = new Padding(3) };
            DataGridView table = Theme.Grid();
            table.ReadOnly = false;
            table.EditMode = DataGridViewEditMode.EditOnEnter;
            table.SelectionMode = DataGridViewSelectionMode.CellSelect;
            Theme.Column(table, "ДЕТАЛЬ / УЗЕЛ", "owner", 29);
            Theme.Column(table, "ПАРАМЕТР", "label", 28);
            Theme.Column(table, "ЗНАЧЕНИЕ", "value", 14);
            Theme.Column(table, "ЕД.", "unit", 9);
            Theme.Column(table, "ПОДСКАЗКА", "suggested", 20);
            foreach (DataGridViewColumn column in table.Columns) column.ReadOnly = column.Name != "value";
            table.CellFormatting += (_, e) =>
            {
                // Format only the displayed text; preserve the exact saved token
                // unless the user edits the cell.
                if (e.ColumnIndex == 2 && e.Value is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number))
                { e.Value = number.ToString("G7", CultureInfo.InvariantCulture); e.FormattingApplied = true; }
            };
            table.CurrentCellChanged += (_, _) => { if (!populating) Detail(); };
            table.CurrentCellDirtyStateChanged += (_, _) => { if (table.IsCurrentCellDirty && table.CurrentCell is DataGridViewCheckBoxCell) table.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            table.CellValueChanged += (_, e) =>
            {
                if (populating || e.RowIndex < 0 || e.ColumnIndex != 2 || table.Rows[e.RowIndex].Tag is not VehicleTuningField field || !Editable(field)) return;
                object? value = table.Rows[e.RowIndex].Cells[2].Value;
                string text = field.IsBoolean ? (value is true ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                string original = SaveSessionValue(payloads[field.DomainId], field.JsonPointer);
                var key = (field.DomainId, field.JsonPointer);
                if (text == original) pending.Remove(key); else pending[key] = text;
                table.Rows[e.RowIndex].Cells[2].Style.Font = pending.ContainsKey(key) ? changedFont : table.Font;
                UpdatePending();
            };
            table.DataError += (_, e) => { e.ThrowException = false; feedback.ForeColor = Theme.Warning; feedback.Text = "Не удалось прочитать значение. Проверьте выделенную ячейку."; };
            page.Controls.Add(table);
            tabs.TabPages.Add(page);
            grids.Add(category, table);
        }
        outer.Controls.Add(tabs, 0, 2);
        outer.Controls.Add(help, 0, 3);
        outer.Controls.Add(feedback, 0, 4);
        outer.Controls.Add(footer, 0, 5);
        useSuggested = Theme.Button("Взять подсказку", (_, _) =>
        {
            if (ActiveGrid()?.CurrentRow is DataGridViewRow row && row.Tag is VehicleTuningField field && field.RecommendedValue is double value && Editable(field))
            { ActiveGrid()!.EndEdit(); row.Cells[2].Value = value.ToString("G", CultureInfo.InvariantCulture); }
        }, false, 180);
        applyTuning = Theme.Button("Применить настройки", (_, _) =>
        {
            try
            {
                foreach (DataGridView table in grids.Values) if (!table.EndEdit()) throw new InvalidDataException("Завершите ввод значения.");
                session.ApplyVehicleTuning(pending.Select(pair => new SaveEdit(pair.Key.Domain, pair.Key.Pointer, pair.Value)).ToArray());
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or NotSupportedException or System.Text.Json.JsonException or FormatException)
            { feedback.ForeColor = Theme.Warning; feedback.Text = error.Message; help.Text = error.Message; }
        }, true, 245);
        footer.Controls.AddRange([useSuggested, applyTuning, Theme.Button("Отмена", (_, _) => dialog.Close(), false, 130)]);
        void Populate()
        {
            populating = true;
            try
            {
                fields = VehicleTuningCatalog.Build(domain.Payload, picker.SelectedIndex, payloads.GetValueOrDefault("items.instances"));
                foreach (var pair in grids)
                {
                    DataGridView table = pair.Value;
                    table.Rows.Clear();
                    foreach (VehicleTuningField field in fields.Where(f => f.Category == pair.Key))
                    {
                        string value = pending.GetValueOrDefault((field.DomainId, field.JsonPointer), SaveSessionValue(payloads[field.DomainId], field.JsonPointer));
                        string suggestion = !Editable(field) ? "Просмотр" : field.RecommendedValue?.ToString("G", CultureInfo.InvariantCulture) ?? "См. пояснение";
                        int index = table.Rows.Add(field.Owner, field.Label, value, field.Unit, suggestion);
                        DataGridViewRow row = table.Rows[index];
                        row.Tag = field;
                        if (field.IsBoolean) row.Cells[2] = new DataGridViewCheckBoxCell { Value = value == "true" };
                        row.Cells[2].ReadOnly = !Editable(field);
                        row.Cells[2].Style.BackColor = Editable(field) ? Color.FromArgb(237, 246, 217) : Theme.Background;
                        foreach (DataGridViewCell cell in row.Cells) cell.ToolTipText = field.Help;
                    }
                }
            }
            finally { populating = false; }
            UpdatePending(); Detail();
        }
        tabs.SelectedIndexChanged += (_, _) => Detail();
        picker.SelectedIndexChanged += (_, _) => Populate();
        picker.SelectedIndex = 0;
        if (checkDirectory is null)
        {
            if (dialog.ShowDialog(this) == DialogResult.OK)
            { AfterChange(); SetStatus("Настройки авто применены в рабочей копии. Проверьте изменения и сохраните файл."); }
        }
        else
        {
            dialog.Show(this);
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                tabs.SelectedIndex = i; dialog.Refresh();
                using var bitmap = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                bitmap.Save(Path.Combine(checkDirectory, $"tuning-{i + 1}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            DataGridView? editableGrid = grids.Values.FirstOrDefault(table => table.Rows.Cast<DataGridViewRow>().Any(row => row.Tag is VehicleTuningField { RecommendedValue: not null } f && Editable(f)));
            if (editableGrid is not null)
            {
                DataGridViewRow row = editableGrid.Rows.Cast<DataGridViewRow>().First(row => row.Tag is VehicleTuningField { RecommendedValue: not null } f && Editable(f));
                VehicleTuningField field = (VehicleTuningField)row.Tag!;
                tabs.SelectedTab = (TabPage)editableGrid.Parent!;
                editableGrid.CurrentCell = row.Cells[2];
                editableGrid.EndEdit(); row.Cells[2].Value = "invalid";
                var before = session.Changes.ToArray();
                applyTuning.PerformClick();
                if (dialog.IsDisposed || !before.SequenceEqual(session.Changes)) throw new InvalidOperationException("Invalid tuning UI edit was not rejected atomically.");
                row.Cells[2].Value = (field.RecommendedValue!.Value == field.Min ? field.Max ?? field.RecommendedValue.Value : field.Min ?? 0).ToString("G", CultureInfo.InvariantCulture);
                editableGrid.EndEdit();
                applyTuning.PerformClick();
                if (dialog.DialogResult != DialogResult.OK || !session.CanUndo) throw new InvalidOperationException("Tuning UI apply did not commit.");
                SaveSession tunedExport = SaveSession.Open(session.FilePath);
                tunedExport.ApplyVehicleTuning(pending.Select(pair => new SaveEdit(pair.Key.Domain, pair.Key.Pointer, pair.Value)).ToArray());
                SaveWriteResult exported = tunedExport.SaveAs(Path.Combine(checkDirectory, "tuning-ui-edited.save.json"));
                if (SaveSession.Open(exported.Path).Validate().Any(issue => issue.Severity == ValidationSeverity.Error)) throw new InvalidOperationException("Tuning UI export did not reopen cleanly.");
                session.Undo();
                if (!before.SequenceEqual(session.Changes)) throw new InvalidOperationException("Tuning UI undo did not restore earlier changes.");
                File.WriteAllText(Path.Combine(checkDirectory, "tuning-ui-check.txt"), $"PASS {fields.Count} fields; four tabs rendered; invalid input rejected without closing; valid grid edit applied; one undo restores previous state.");
            }
            else File.WriteAllText(Path.Combine(checkDirectory, "tuning-ui-check.txt"), "PASS panel rendered; fixture has no editable suggested field.");
            dialog.Close();
        }
    }

    private static string SaveSessionValue(JsonNode root, string pointer)
    {
        JsonNode? current = root;
        foreach (string token in pointer.Split('/').Skip(1))
        {
            string key = token.Replace("~1", "/").Replace("~0", "~");
            current = current is JsonArray array ? array[int.Parse(key, CultureInfo.InvariantCulture)] : current?[key];
        }
        return current?.ToJsonString() ?? "";
    }
}
