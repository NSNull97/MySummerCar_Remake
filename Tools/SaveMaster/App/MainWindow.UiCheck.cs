using System.Security.Cryptography;
using MySummerRemake.SaveMaster.Core;

namespace MySummerRemake.SaveMaster;

internal sealed partial class MainWindow
{
    /// <summary>Opt-in developer smoke check using the same controls and handlers as the application.</summary>
    internal void RunUiCheck(string outputDirectory)
    {
        var report = new List<string>();
        Directory.CreateDirectory(outputDirectory);
        try
        {
            if (session is null) throw new InvalidOperationException("UI check requires a verified native save fixture.");
            var source = session.FilePath;
            var sourceHash = SHA256.HashData(File.ReadAllBytes(source));
            if (allFields.Count < 10 || grid.RowCount != allFields.Count) throw new InvalidOperationException("Loaded fields are absent from the grid.");
            report.Add($"PASS open: {session.DocumentVersion}, {domainSnapshots.Count} domains, {allFields.Count} fields");
            SaveRender("01-overview.png");

            search.Text = "hunger";
            RefreshGrid();
            var index = visibleFields.FindIndex(f => f.Domain == "player.needs" && f.Key == "hunger");
            if (index < 0) throw new InvalidOperationException("Global search did not find player hunger.");
            grid.CurrentCell = grid.Rows[index].Cells[0];
            SelectField();
            var original = valueInput.Text;
            valueInput.Text = original == "23.5" ? "24.5" : "23.5";
            apply.PerformClick();
            if (!session.IsDirty || !session.CanUndo || session.Changes.Count != 1) throw new InvalidOperationException("Apply control did not produce exactly one edit.");
            report.Add("PASS search and typed fractional value applied through the editor button");
            SaveRender("02-edit.png");
            undo.PerformClick();
            if (session.IsDirty || !session.CanRedo) throw new InvalidOperationException("Undo did not return the original snapshot.");
            redo.PerformClick();
            if (!session.IsDirty) throw new InvalidOperationException("Redo did not restore the edit.");
            report.Add("PASS undo and redo buttons");
            changedOnly.Checked = true;
            if (grid.RowCount != 1) throw new InvalidOperationException("Changed-only filter lost the edited field.");
            report.Add("PASS changes-only filter");
            ShowWorkshop(Path.Combine(outputDirectory, "03-workshop.png"));
            report.Add("PASS workshop dialog created and rendered with actual vehicle wiring");
            ShowVehicleTuning(outputDirectory);
            report.Add("PASS vehicle tuning tabs, invalid-input rejection, grid edit and atomic undo");
            var result = session.SaveAs(Path.Combine(outputDirectory, "ui-edited.save.json"));
            var reopened = SaveSession.Open(result.Path);
            if (reopened.Validate().Any(i => i.Severity == ValidationSeverity.Error)) throw new InvalidOperationException("Exported UI edit failed validation.");
            if (!sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source)))) throw new InvalidOperationException("UI check source changed.");
            report.Add("PASS edited copy reopens with valid integrity and semantics; source bytes unchanged");
            File.WriteAllLines(Path.Combine(outputDirectory, "ui-check.txt"), report);
        }
        catch (Exception exception)
        {
            report.Add("FAIL " + exception);
            File.WriteAllLines(Path.Combine(outputDirectory, "ui-check.txt"), report);
            Environment.ExitCode = 1;
        }
        finally
        {
            session = null;
            Close();
        }

        void SaveRender(string name)
        {
            Refresh();
            using var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
            bitmap.Save(Path.Combine(outputDirectory, name), System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
