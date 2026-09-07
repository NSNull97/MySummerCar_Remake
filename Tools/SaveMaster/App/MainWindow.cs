using System.Text.Json;
using System.Text.Json.Nodes;
using MySummerRemake.SaveMaster.Core;

namespace MySummerRemake.SaveMaster;

internal sealed partial class MainWindow : Form
{
    private SaveSession? session;
    private List<FieldRow> allFields = [];
    private List<FieldRow> visibleFields = [];
    private HashSet<string> changedFields = new(StringComparer.Ordinal);
    private IReadOnlyList<SaveDomain> domainSnapshots = [];
    private readonly TreeView tree = new();
    private readonly DataGridView grid = Theme.Grid();
    private readonly TextBox search = new();
    private readonly CheckBox changedOnly = new() { Text = "Только изменения", AutoSize = true };
    private readonly Label heading = Theme.Label("Откройте сохранение", 20, true);
    private readonly Label subtitle = Theme.Label("Ремейк • native save • локальный редактор", 10);
    private readonly Label status = Theme.Label("Готов к работе. Исходный файл меняется только после просмотра правок и сохранения.");
    private readonly Label fileInfo = Theme.Label("Файл не открыт", 10);
    private readonly Label fieldsInfo = Theme.Label("0 полей", 11, true);
    private readonly Label changesInfo = Theme.Label("0 изменений", 11, true);
    private readonly Label integrityInfo = Theme.Label("SHA-256  —", 11, true);
    private readonly Label fieldName = Theme.Label("Выберите поле в таблице", 12, true);
    private readonly Label fieldHelp = Theme.Label("Значения меняются в рабочей копии. Ctrl+Z отменяет последнее действие.");
    private readonly TextBox fieldPath = new() { ReadOnly = true, BorderStyle = BorderStyle.None };
    private readonly TextBox valueInput = new();
    private readonly ComboBox valueChoice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
    private readonly Button apply;
    private readonly Button save;
    private readonly Button saveCopy;
    private readonly Button undo;
    private readonly Button redo;
    private readonly Button raw;
    private readonly Button actions;
    private readonly Button tuning;
    private readonly System.Windows.Forms.Timer searchDelay = new() { Interval = 200 };
    private bool building;
    private bool presentingField;
    private FieldRow? presentedField;
    private string activeDomain = "";
    private string activePointer = "";

    public MainWindow(string? initialPath)
    {
        Text = "My Summer Remake Save Master  " + Application.ProductVersion.Split('+')[0];
        Font = new Font("Segoe UI", 10);
        BackColor = Theme.Background;
        ForeColor = Theme.Ink;
        MinimumSize = new Size(1050, 700);
        Size = new Size(1370, 880);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        AllowDrop = true;

        apply = Theme.Button("Применить", (_, _) => ApplyValue(), true, 145);
        save = Theme.Button("Сохранить…", (_, _) => SaveReviewed(false), true, 132);
        saveCopy = Theme.Button("Сохранить копию…", (_, _) => SaveReviewed(true), false, 174);
        undo = Theme.Button("↶  Отменить", (_, _) => History(false), false, 124);
        redo = Theme.Button("↷  Повторить", (_, _) => History(true), false, 128);
        raw = Theme.Button("JSON раздела", (_, _) => EditRaw(), false, 146);
        actions = Theme.Button("Мастерская", (_, _) => ShowWorkshop(), false, 140);
        tuning = Theme.Button("Настройка авто", (_, _) => Run(() => ShowVehicleTuning()), false, 162);
        BuildLayout();
        tree.AfterSelect += (_, e) => SelectNode(e.Node);
        tree.BeforeExpand += (_, e) => PopulateLazy(e.Node);
        // SelectionChanged fires before DataGridView updates CurrentCell. Bind
        // after that update so a click never leaves the previous field editable.
        grid.CurrentCellChanged += (_, _) => SelectField();
        grid.CellDoubleClick += (_, _) => { if (apply.Enabled) { valueInput.Focus(); valueInput.SelectAll(); } };
        search.TextChanged += (_, _) => { searchDelay.Stop(); searchDelay.Start(); };
        searchDelay.Tick += (_, _) => { searchDelay.Stop(); RefreshGrid(); };
        changedOnly.CheckedChanged += (_, _) => RefreshGrid();
        KeyDown += HandleShortcut;
        valueInput.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { ApplyValue(); e.SuppressKeyPress = true; } };
        FormClosing += (_, e) => { if (!MayLeave()) e.Cancel = true; };
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            {
                string path = files[0];
                // Return to Explorer's OLE callback before reading or showing dialogs.
                BeginInvoke(async () => await OpenFileAsync(path));
            }
        };
        Shown += async (_, _) => { if (initialPath is not null) await OpenFileAsync(initialPath); };
        UpdateButtons();
        SelectField();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Ink, Margin = Padding.Empty };
        var title = Theme.Label("MY SUMMER REMAKE", 11, true);
        title.ForeColor = Theme.Lime;
        title.SetBounds(24, 12, 600, 24);
        var brand = Theme.Label("Save Master", 25, true);
        brand.ForeColor = Color.White;
        brand.SetBounds(22, 36, 700, 43);
        var badge = Theme.Label("ГАРАЖ СОХРАНЕНИЙ  /  01", 10, true);
        badge.ForeColor = Theme.Lime;
        badge.Dock = DockStyle.Right;
        badge.Width = 280;
        header.Controls.AddRange([title, brand, badge]);
        layout.Controls.Add(header, 0, 0);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 11, 0, 0), WrapContents = false, AutoScroll = true, Margin = Padding.Empty };
        toolbar.Controls.AddRange([
            Theme.Button("Открыть…", async (_, _) => await PickFileAsync(), false, 112),
            Theme.Button("Мои сейвы", async (_, _) => await ShowSlotsAsync(), false, 119),
            save, saveCopy, undo, redo,
            Theme.Button("Проверить", (_, _) => ShowValidation(), false, 117),
            Theme.Button("Помощь", (_, _) => ShowHelp(), false, 95)
        ]);
        layout.Controls.Add(toolbar, 0, 1);

        var overview = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = new Padding(20, 0, 20, 10), BackColor = Theme.Paper };
        overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 49));
        overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17));
        overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17));
        overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17));
        foreach (var label in new[] { fileInfo, fieldsInfo, changesInfo, integrityInfo })
        {
            label.Dock = DockStyle.Fill;
            label.Padding = new Padding(14, 0, 0, 0);
            overview.Controls.Add(label);
        }
        layout.Controls.Add(overview, 0, 2);

        var workspace = new SplitContainer { Dock = DockStyle.Fill, Margin = new Padding(20, 0, 20, 0), BackColor = Theme.Line, SplitterWidth = 5 };
        workspace.Size = new Size(1300, 600);
        workspace.Panel1MinSize = 220;
        workspace.Panel2MinSize = 640;
        workspace.SplitterDistance = 290;
        workspace.Panel1.BackColor = Theme.Paper;
        workspace.Panel2.BackColor = Theme.Paper;
        layout.Controls.Add(workspace, 0, 3);
        var navTitle = Theme.Label("РАЗДЕЛЫ СОХРАНЕНИЯ", 9, true);
        navTitle.Dock = DockStyle.Top;
        navTitle.Height = 42;
        navTitle.Padding = new Padding(13, 0, 0, 0);
        tree.Dock = DockStyle.Fill;
        tree.BorderStyle = BorderStyle.None;
        tree.BackColor = Theme.Paper;
        tree.ForeColor = Theme.Ink;
        tree.ItemHeight = 29;
        tree.HideSelection = false;
        tree.ShowLines = false;
        tree.FullRowSelect = true;
        tree.Indent = 16;
        tree.AccessibleName = "Разделы сохранения";
        tree.Nodes.Add("Откройте .save.json или перетащите файл");
        workspace.Panel1.Controls.Add(tree);
        workspace.Panel1.Controls.Add(navTitle);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(15, 0, 15, 0) };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 79));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 185));
        workspace.Panel2.Controls.Add(content);
        var titles = new Panel { Dock = DockStyle.Fill };
        heading.Dock = DockStyle.Top;
        heading.Height = 47;
        subtitle.Dock = DockStyle.Bottom;
        subtitle.Height = 29;
        subtitle.ForeColor = Theme.Muted;
        titles.Controls.AddRange([heading, subtitle]);
        content.Controls.Add(titles, 0, 0);

        var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        search.PlaceholderText = "Поиск по всему сейву: деньги, hunger, piston, stage…";
        search.Dock = DockStyle.Fill;
        search.AccessibleName = "Поиск по сохранению";
        search.Margin = new Padding(0, 6, 12, 0);
        changedOnly.Anchor = AnchorStyles.Left;
        filters.Controls.Add(search, 0, 0);
        filters.Controls.Add(changedOnly, 1, 0);
        content.Controls.Add(filters, 0, 1);
        var shortcuts = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        shortcuts.Controls.AddRange([
            tuning, actions, raw,
            Theme.Button("Баланс…", (_, _) => EditBalance(), false, 110),
            Theme.Button("Дата…", (_, _) => EditTime(), false, 95),
            Theme.Button("Изменения", (_, _) => ReviewOnly(), false, 132),
            Theme.Button("Сброс поиска", (_, _) => { search.Clear(); changedOnly.Checked = false; }, false, 140)
        ]);
        content.Controls.Add(shortcuts, 0, 2);
        Theme.Column(grid, "ПОЛЕ", "field", 25);
        Theme.Column(grid, "ОБЪЕКТ / ПУТЬ", "owner", 35);
        Theme.Column(grid, "ЗНАЧЕНИЕ", "value", 25);
        Theme.Column(grid, "СОСТОЯНИЕ", "state", 15);
        grid.AccessibleName = "Поля сохранения";
        grid.VirtualMode = true;
        grid.CellValueNeeded += GridCellValueNeeded;
        content.Controls.Add(grid, 0, 3);

        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(0, 8, 0, 8) };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fieldName.Dock = DockStyle.Fill;
        editor.Controls.Add(fieldName, 0, 0);
        editor.SetColumnSpan(fieldName, 2);
        fieldPath.Dock = DockStyle.Fill;
        fieldPath.BackColor = Theme.Paper;
        fieldPath.ForeColor = Theme.Muted;
        fieldPath.Font = new Font("Consolas", 9);
        editor.Controls.Add(fieldPath, 0, 1);
        editor.SetColumnSpan(fieldPath, 2);
        var inputs = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
        valueInput.Dock = DockStyle.Top;
        valueInput.AccessibleName = "Новое значение";
        valueChoice.Dock = DockStyle.Top;
        valueChoice.AccessibleName = "Новое значение из списка";
        inputs.Controls.AddRange([valueInput, valueChoice]);
        editor.Controls.Add(inputs, 0, 2);
        editor.Controls.Add(apply, 1, 2);
        fieldHelp.Dock = DockStyle.Fill;
        fieldHelp.ForeColor = Theme.Muted;
        fieldHelp.TextAlign = ContentAlignment.TopLeft;
        editor.Controls.Add(fieldHelp, 0, 3);
        editor.SetColumnSpan(fieldHelp, 2);
        content.Controls.Add(editor, 0, 4);

        status.Dock = DockStyle.Fill;
        status.Font = new Font("Segoe UI", 9);
        status.Padding = new Padding(20, 0, 20, 0);
        layout.Controls.Add(status, 0, 4);
    }

    private async Task PickFileAsync()
    {
        if (isOpening) return;
        string? selectedPath = null;
        using (var dialog = new OpenFileDialog
        {
            Title = "Открыть сохранение ремейка", Filter = "Сохранения ремейка|*.json;*.bak;*.backup|Все файлы|*.*",
            InitialDirectory = Directory.Exists(NativeSaveDirectory) ? NativeSaveDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        })
        {
            if (dialog.ShowDialog(this) == DialogResult.OK) selectedPath = dialog.FileName;
        }
        if (selectedPath is not null) await OpenFileAsync(selectedPath);
    }

    private void RebuildTree()
    {
        if (session is null) return;
        building = true;
        tree.BeginUpdate();
        tree.Nodes.Clear();
        var all = new TreeNode("Все разделы") { Tag = new NodeTarget("", "") };
        tree.Nodes.Add(all);
        foreach (var domain in domainSnapshots.OrderBy(d => DomainOrder(d.Id)).ThenBy(d => d.Id))
        {
            var root = new TreeNode(SchemaCatalog.DomainTitle(domain.Id)) { Tag = new NodeTarget(domain.Id, "") };
            root.ToolTipText = $"{domain.Id} • схема {domain.SchemaVersion}";
            AddChildren(root, domain.Payload, domain.Id, "");
            tree.Nodes.Add(root);
        }
        tree.ShowNodeToolTips = true;
        tree.EndUpdate();
        building = false;
        tree.SelectedNode = all;
    }

    private static int DomainOrder(string id) => id switch
    {
        "player.state" => 0, "player.needs" => 1, "economy.player" => 2,
        "vehicle.satsuma" => 3, "vehicle.satsuma.key-access" => 4,
        "items.instances" => 5, "world.entities" => 6, "interaction.carry" => 7,
        "home.state" => 8, _ => 9
    };

    private static void AddChildren(TreeNode parent, JsonNode? payload, string domain, string pointer)
    {
        IEnumerable<(string, JsonNode?)> children = payload switch
        {
            JsonObject obj => obj.Select(pair => (pair.Key, pair.Value)),
            JsonArray array => array.Select((value, index) => (index.ToString(), value)),
            _ => []
        };
        foreach (var (key, child) in children)
        {
            if (child is not (JsonObject or JsonArray)) continue;
            var childPointer = pointer + "/" + FieldIndex.Escape(key);
            var label = SchemaCatalog.Describe(domain, childPointer).Label;
            if (child is JsonArray arr) label += $"  ({arr.Count})";
            else if (child is JsonObject obj)
            {
                var name = new[] { "partDefinitionId", "fastenerDefinitionId", "definitionId", "characterDefinitionId", "mountId", "stableEntityId", "vehicleId" }
                    .Select(k => obj[k]?.ToString()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (name is not null) label = name;
            }
            var node = new TreeNode(label) { Tag = new NodeTarget(domain, childPointer), ToolTipText = childPointer };
            if (child is JsonObject o && o.Any(p => p.Value is JsonObject or JsonArray) || child is JsonArray a && a.Any(v => v is JsonObject or JsonArray))
                node.Nodes.Add(new TreeNode("…") { Tag = "lazy" });
            parent.Nodes.Add(node);
        }
    }

    private void PopulateLazy(TreeNode? node)
    {
        if (session is null || node?.Tag is not NodeTarget target || node.Nodes.Count != 1 || node.Nodes[0].Tag as string != "lazy") return;
        node.Nodes.Clear();
        var domain = domainSnapshots.First(d => d.Id == target.Domain);
        AddChildren(node, FieldIndex.At(domain.Payload, target.Pointer), target.Domain, target.Pointer);
    }

    private void SelectNode(TreeNode? node)
    {
        if (building || node?.Tag is not NodeTarget target) return;
        activeDomain = target.Domain;
        activePointer = target.Pointer;
        heading.Text = activeDomain.Length == 0 ? "Все разделы" : node.Text;
        subtitle.Text = activeDomain.Length == 0 ? "Игрок, машина и мир — данные открытого сохранения" : activeDomain + activePointer;
        search.Clear();
        RefreshGrid();
        UpdateButtons();
    }

    private void RefreshGrid(string? reselect = null)
    {
        if (session is null || building) return;
        var query = search.Text.Trim();
        changedFields = session.Changes.Select(c => c.DomainId + c.JsonPointer).ToHashSet(StringComparer.Ordinal);
        visibleFields = allFields.Where(f =>
            (query.Length > 0
                ? f.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase) || SchemaCatalog.Describe(f.Domain, f.Pointer).Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                : (activeDomain.Length == 0 || f.Domain == activeDomain) && (activePointer.Length == 0 || f.Pointer.StartsWith(activePointer + "/", StringComparison.Ordinal))) &&
            (!changedOnly.Checked || IsChanged(f))).ToList();
        building = true;
        grid.RowCount = 0;
        grid.RowCount = visibleFields.Count;
        grid.ClearSelection();
        var selectedIndex = reselect is null ? 0 : visibleFields.FindIndex(f => f.Domain + f.Pointer == reselect);
        if (selectedIndex >= 0 && selectedIndex < visibleFields.Count)
        {
            grid.CurrentCell = grid.Rows[selectedIndex].Cells[0];
            grid.Rows[selectedIndex].Selected = true;
        }
        building = false;
        fieldsInfo.Text = $"{visibleFields.Count:N0} / {allFields.Count:N0} полей";
        SelectField();
    }

    private void GridCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= visibleFields.Count) return;
        var field = visibleFields[e.RowIndex];
        var metadata = SchemaCatalog.Describe(field.Domain, field.Pointer);
        e.Value = e.ColumnIndex switch
        {
            0 => metadata.Label,
            1 => field.Owner.Length > 0 ? field.Owner : field.Domain + field.Pointer[..Math.Max(0, field.Pointer.LastIndexOf('/'))],
            2 => DisplayValue(field, metadata),
            3 => IsChanged(field) ? "Изменено" : session?.CanEditDomain(field.Domain) != true ? "Только чтение" : metadata.IsReadOnly ? "Защищено" : "—",
            _ => ""
        };
    }

    private static string DisplayValue(FieldRow field, FieldMetadata metadata)
    {
        if (field.Kind is JsonValueKind.True or JsonValueKind.False) return field.Value == "true" ? "Да" : "Нет";
        return metadata.Choices?.TryGetValue(field.Value, out var label) == true ? label + $"  [{field.Value}]" : field.Value;
    }

    private bool IsChanged(FieldRow field) => changedFields.Contains(field.Domain + field.Pointer);
    private FieldRow? SelectedField => grid.CurrentCell is { RowIndex: >= 0 } cell && cell.RowIndex < visibleFields.Count ? visibleFields[cell.RowIndex] : null;

    private void SelectField()
    {
        if (building || presentingField || isOpening) return;
        var field = SelectedField;
        if (field is not null && ReferenceEquals(field, presentedField)) return;
        presentingField = true;
        try
        {
            presentedField = field;
            if (field is null)
            {
                apply.Enabled = false;
                valueInput.Enabled = false;
                valueChoice.Visible = false;
                valueInput.Visible = true;
                valueInput.Clear();
                fieldName.Text = "Нет выбранного поля";
                fieldPath.Text = "";
                fieldHelp.Text = "Выберите раздел слева или найдите поле через поиск. Пустые коллекции доступны в JSON раздела.";
                return;
            }
            var metadata = SchemaCatalog.Describe(field.Domain, field.Pointer);
            fieldName.Text = metadata.Label;
            fieldPath.Text = field.Domain + field.Pointer;
            var domain = domainSnapshots.First(d => d.Id == field.Domain);
            var editable = session!.CanEditDomain(domain.Id) && !metadata.IsReadOnly && field.Kind != JsonValueKind.Null;
            valueInput.Text = field.Value;
            valueInput.Enabled = editable;
            valueInput.Visible = true;
            valueChoice.Visible = false;
            valueChoice.Items.Clear();
            IReadOnlyDictionary<string, string>? choices = metadata.Choices;
            if (field.Kind is JsonValueKind.True or JsonValueKind.False) choices = new Dictionary<string, string> { ["false"] = "Нет", ["true"] = "Да" };
            if (choices is not null)
            {
                foreach (var pair in choices) valueChoice.Items.Add(new Choice(pair.Key, pair.Value));
                valueChoice.SelectedItem = valueChoice.Items.Cast<Choice>().FirstOrDefault(c => c.Value == field.Value);
                valueChoice.Enabled = editable;
                valueChoice.Visible = true;
                valueInput.Visible = false;
            }
            apply.Enabled = editable;
            fieldHelp.Text = metadata.Help + (metadata.Min.HasValue || metadata.Max.HasValue ? $"  Диапазон: {metadata.Min?.ToString() ?? "−∞"} … {metadata.Max?.ToString() ?? "+∞"}." : "") +
                (!editable ? "  Только чтение: " + FieldReadOnlyReason(field, metadata) : "  Измените значение выше и нажмите «Применить».");
            if (editable && field.Key == "stage" && field.Pointer.Contains("/assembly/fasteners/", StringComparison.Ordinal) &&
                SchemaVehicleDefinitions.Fasteners.TryGetValue(field.Owner, out var bolt))
                fieldHelp.Text = $"Затяжка 0–{bolt.MaximumStage}; ключ {bolt.SizeMillimeters} мм. Состояние группы крепежа обновится вместе с этим болтом. Вставленный болт и установленная деталь обязательны.";
        }
        finally { presentingField = false; }
    }

    private string FieldReadOnlyReason(FieldRow field, FieldMetadata metadata)
    {
        if (session?.CanEdit != true)
            return $"версия документа v{session?.DocumentVersion} не поддерживается. Поддерживаются {SaveSession.SupportedDocumentVersions}; нужен обновлённый редактор.";
        if (!session.CanEditDomain(field.Domain))
            return $"не поддерживается схема раздела {field.Domain} или его вложенных данных. Нужен обновлённый редактор.";
        if (metadata.IsReadOnly) return "идентификаторы и служебные поля защищены от изменения.";
        return "пустое значение не редактируется как обычное число или текст.";
    }

    private void ApplyValue()
    {
        var field = SelectedField;
        if (isOpening || session is null || field is null || !apply.Enabled || !ReferenceEquals(field, presentedField)) return;
        Run(() =>
        {
            var text = valueChoice.Visible ? (valueChoice.SelectedItem as Choice)?.Value ?? field.Value : valueInput.Text;
            if (field.Kind == JsonValueKind.Number) text = text.Replace(',', '.');
            if (field.Domain == "vehicle.satsuma" && field.Key == "stage" && field.Pointer.Contains("/assembly/fasteners/", StringComparison.Ordinal))
            {
                if (!int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var stage))
                    throw new InvalidDataException("Ступень затяжки должна быть целым числом.");
                var vehicleDomain = domainSnapshots.First(d => d.Id == field.Domain);
                var bolt = FieldIndex.At(vehicleDomain.Payload, field.Pointer[..field.Pointer.LastIndexOf('/')])!;
                var edited = SchemaActions.SetFastenerStage(vehicleDomain.Payload, int.Parse(field.Pointer.Split('/')[2]),
                    bolt["mountId"]!.GetValue<string>(), bolt["fastenerDefinitionId"]!.GetValue<string>(), stage);
                session.SetJson(field.Domain, "", edited.ToJsonString());
            }
            else session.SetValue(field.Domain, field.Pointer, text);
            AfterChange(field.Domain + field.Pointer);
            SetStatus("Правка применена к рабочей копии. Исходный файл ещё не изменён.");
        });
    }

    private void AfterChange(string? selected = null)
    {
        if (session is null) return;
        domainSnapshots = session.Domains;
        allFields = FieldIndex.Build(domainSnapshots);
        RefreshGrid(selected);
        UpdateButtons();
    }

    private void History(bool forward)
    {
        if (session is null || isOpening) return;
        Run(() =>
        {
            if (forward && session.CanRedo) session.Redo();
            if (!forward && session.CanUndo) session.Undo();
            AfterChange(SelectedField is { } field ? field.Domain + field.Pointer : null);
            SetStatus(forward ? "Изменение повторено." : "Изменение отменено.");
        });
    }

    private void UpdateButtons()
    {
        save.Enabled = !isOpening && session?.CanEdit == true && session.IsDirty;
        saveCopy.Enabled = !isOpening && session?.CanEdit == true;
        undo.Enabled = !isOpening && session?.CanUndo == true;
        redo.Enabled = !isOpening && session?.CanRedo == true;
        raw.Enabled = !isOpening && session is not null && activeDomain.Length > 0;
        actions.Enabled = !isOpening && session?.CanEdit == true && domainSnapshots.Any(d => d.Id == "vehicle.satsuma");
        tuning.Enabled = !isOpening && session is not null && domainSnapshots.Any(d => d.Id == "vehicle.satsuma");
        changesInfo.Text = $"{session?.Changes.Count ?? 0:N0} изменений";
        fileInfo.Text = session is null ? "Файл не открыт" : $"v{session.DocumentVersion}  ·  {Path.GetFileName(Path.GetDirectoryName(session.FilePath))} / {Path.GetFileName(session.FilePath)}";
        integrityInfo.Text = session is null ? "SHA-256  —" : session.CanEdit ? "SHA-256  ✓" : "Только чтение";
        Text = (session?.IsDirty == true ? "● " : "") + "My Summer Remake Save Master  " + Application.ProductVersion.Split('+')[0];
    }

    private async void HandleShortcut(object? sender, KeyEventArgs e)
    {
        if (!e.Control || isOpening) return;
        e.SuppressKeyPress = true;
        switch (e.KeyCode)
        {
            case Keys.O: await PickFileAsync(); break;
            case Keys.S: SaveReviewed(e.Shift); break;
            case Keys.Z: History(false); break;
            case Keys.Y: History(true); break;
            case Keys.F: search.Focus(); search.SelectAll(); break;
            default: e.SuppressKeyPress = false; return;
        }
    }

    private bool MayLeave() => session?.IsDirty != true || MessageBox.Show(this,
        "В рабочей копии остались несохранённые правки. Закрыть её и отбросить эти правки?", "Несохранённые изменения",
        MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private void Run(Action action)
    {
        // This wrapper also contains interactive modal dialogs. A wait cursor
        // for their entire lifetime made a ready window look blocked.
        try { action(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SetStatus("Операция прервана: " + ex.Message, true);
            MessageBox.Show(this, ex.Message, "Save Master — операция прервана", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SetStatus(string text, bool warning = false) { status.Text = text; status.ForeColor = warning ? Theme.Warning : Theme.Muted; }
    private sealed record NodeTarget(string Domain, string Pointer);
    private sealed record Choice(string Value, string Label) { public override string ToString() => Label; }
}
