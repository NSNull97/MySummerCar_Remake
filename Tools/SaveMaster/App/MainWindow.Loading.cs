using System.Diagnostics;
using MySummerRemake.SaveMaster.Core;

namespace MySummerRemake.SaveMaster;

internal sealed partial class MainWindow
{
    private bool isOpening;

    internal async Task<bool> OpenFileAsync(string path, Func<string, SaveSession>? openSession = null, bool showErrors = true)
    {
        if (isOpening || !MayLeave()) return false;
        var trace = new LoadTrace();
        trace.Write("Begin open: " + path);
        searchDelay.Stop();
        isOpening = true;
        SetLoadingControls(true);
        SetStatus("Открываю сохранение… Чтение, проверка и подготовка идут в фоне.");
        var progress = new Progress<string>(message =>
        {
            if (!IsDisposed && !Disposing && isOpening) SetStatus(message);
        });
        try
        {
            var prepared = await Task.Run(() =>
            {
                trace.Write("Worker: read and verify integrity");
                var opened = (openSession ?? SaveSession.Open)(path);
                trace.Write("Worker: integrity verified; preparing domain metadata");
                ((IProgress<string>)progress).Report("Контрольная сумма проверена. Подготавливаю разделы и поиск…");
                var snapshots = opened.Domains;
                // Prime schema checks while the model belongs exclusively to this worker.
                foreach (var domain in snapshots) _ = opened.CanEditDomain(domain.Id);
                var fields = FieldIndex.Build(snapshots);
                trace.Write($"Worker: prepared {snapshots.Count} domains, {fields.Count} fields");
                return (Session: opened, Snapshots: snapshots, Fields: fields);
            });
            if (IsDisposed || Disposing) return false;
            trace.Write("UI: binding prepared snapshot");
            // Publish only a fully prepared session. A read/parse failure preserves the previous one.
            session = prepared.Session;
            domainSnapshots = prepared.Snapshots;
            allFields = prepared.Fields;
            building = true;
            try
            {
                search.Clear();
                changedOnly.Checked = false;
                searchDelay.Stop();
            }
            finally { building = false; }
            RebuildTree();
            trace.Write("UI: tree and grid bound");
            SetStatus(session.CanEdit
                ? $"Открыт v{session.DocumentVersion}. Выберите поле; значение редактируется внизу через «Применить»."
                : $"Открыт v{session.DocumentVersion} только для чтения. Поддерживаются версии {SaveSession.SupportedDocumentVersions}; нужен обновлённый редактор.", !session.CanEdit);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            trace.Write("Failed: " + exception);
            if (!IsDisposed && !Disposing)
            {
                SetStatus("Открытие прервано: " + exception.Message, true);
                if (showErrors) MessageBox.Show(this, exception.Message, "Save Master — файл не открыт", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return false;
        }
        finally
        {
            isOpening = false;
            if (!IsDisposed && !Disposing)
            {
                trace.Write("UI: restoring controls and presenting selected field");
                SetLoadingControls(false);
                presentedField = null;
                SelectField();
                trace.Write("UI: selected field ready; open handler complete");
                BeginInvoke(() => trace.Write("UI: message pump resumed after open"));
            }
        }
    }

    private void SetLoadingControls(bool loading)
    {
        tree.Enabled = grid.Enabled = search.Enabled = changedOnly.Enabled = !loading;
        if (loading) apply.Enabled = valueInput.Enabled = valueChoice.Enabled = false;
        UpdateButtons();
    }

    private sealed class LoadTrace
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly object sync = new();
        private Task previousWrite = Task.CompletedTask;
        private readonly string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySummerRemakeSaveMaster", "Logs", $"open-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid():N}.log");

        internal void Write(string message)
        {
            string entry = $"{clock.Elapsed.TotalMilliseconds:F1}ms [thread {Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}";
            lock (sync)
            {
                // Diagnostics must never block the UI on another filesystem operation.
                previousWrite = previousWrite.ContinueWith(_ =>
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                        File.AppendAllText(filePath, entry);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            }
        }
    }
}
