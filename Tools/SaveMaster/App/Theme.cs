namespace MySummerRemake.SaveMaster;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(243, 244, 238);
    public static readonly Color Paper = Color.FromArgb(253, 253, 249);
    public static readonly Color Ink = Color.FromArgb(31, 47, 39);
    public static readonly Color Muted = Color.FromArgb(99, 112, 103);
    public static readonly Color Green = Color.FromArgb(44, 100, 64);
    public static readonly Color Lime = Color.FromArgb(207, 231, 123);
    public static readonly Color Line = Color.FromArgb(220, 226, 215);
    public static readonly Color Warning = Color.FromArgb(155, 93, 24);

    public static Button Button(string text, EventHandler action, bool primary = false, int width = 120)
    {
        var button = new Button
        {
            Text = text, Width = width, Height = 36, FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Green : Paper, ForeColor = primary ? Color.White : Ink,
            Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand, AutoEllipsis = true,
            AccessibleName = text, UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ? Green : Line;
        button.Click += action;
        return button;
    }

    public static Label Label(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, AutoSize = false, ForeColor = Ink,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
    };

    public static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, BackgroundColor = Paper, BorderStyle = BorderStyle.None,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
        AutoGenerateColumns = false, RowHeadersVisible = false, ReadOnly = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        EnableHeadersVisualStyles = false, GridColor = Line, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        ColumnHeadersHeight = 38, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Background, ForeColor = Muted, Font = new Font("Segoe UI", 9, FontStyle.Bold), Padding = new Padding(8, 0, 0, 0)
        },
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Paper, ForeColor = Ink, SelectionBackColor = Color.FromArgb(223, 236, 210),
            SelectionForeColor = Ink, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 10)
        },
        RowTemplate = { Height = 34 },
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 243) }
    };

    public static void Column(DataGridView grid, string header, string name, int weight) =>
        grid.Columns.Add(new DataGridViewTextBoxColumn
        { HeaderText = header, Name = name, FillWeight = weight, MinimumWidth = 65, SortMode = DataGridViewColumnSortMode.NotSortable });
}
