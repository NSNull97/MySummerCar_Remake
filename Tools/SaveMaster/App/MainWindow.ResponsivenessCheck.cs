using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MySummerRemake.SaveMaster.Core;

namespace MySummerRemake.SaveMaster;

internal sealed partial class MainWindow
{
    /// <summary>
    /// Opt-in developer regression that runs this form's actual message loop.
    /// It never writes a save, drives another application, or uses DrawToBitmap.
    /// Invoke from the shown UI thread so the watchdog covers opening the file.
    /// </summary>
    internal async Task RunResponsivenessCheckAsync(string savePath, string outputDirectory, bool includeWindowStress = false)
    {
        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        string reportPath = Path.Combine(outputDirectory, "responsiveness-check.txt");
        string eventsPath = Path.Combine(outputDirectory, "responsiveness-events.jsonl");
        File.WriteAllText(reportPath, "START real WinForms message-loop responsiveness check; windowStress=" + includeWindowStress + "\n");
        File.WriteAllText(eventsPath, "");
        var logLock = new object();
        using var finished = new ManualResetEventSlim();
        using var process = Process.GetCurrentProcess();
        var elapsed = Stopwatch.StartNew();
        double totalTimeoutSeconds = includeWindowStress ? 75 : 45;
        int heartbeats = 0, gridPaints = 0, gridLayouts = 0, selections = 0, formLayouts = 0, inputLayouts = 0, visibilityChanges = 0;
        long lastHeartbeat = Stopwatch.GetTimestamp();
        long maxHeartbeatGap = 0;
        string phase = "initializing";
        using var heartbeat = new System.Windows.Forms.Timer { Interval = 50 };

        void Log(string message, object? measurements = null)
        {
            lock (logLock)
            {
                File.AppendAllText(reportPath, $"{elapsed.Elapsed.TotalSeconds:F3}s {message}\n");
                File.AppendAllText(eventsPath, JsonSerializer.Serialize(new
                {
                    seconds = elapsed.Elapsed.TotalSeconds,
                    phase = Volatile.Read(ref phase),
                    message,
                    heartbeats = Volatile.Read(ref heartbeats),
                    gridPaints = Volatile.Read(ref gridPaints),
                    gridLayouts = Volatile.Read(ref gridLayouts),
                    selections = Volatile.Read(ref selections),
                    formLayouts = Volatile.Read(ref formLayouts),
                    inputLayouts = Volatile.Read(ref inputLayouts),
                    visibilityChanges = Volatile.Read(ref visibilityChanges),
                    measurements,
                }) + "\n");
            }
        }

        // A UI-thread timeout would itself never run if WM_PAINT/WM_LAYOUT starves
        // the message pump. This background watchdog only terminates this explicit
        // developer-check process, leaving the input save untouched.
        var watchdog = new Thread(() =>
        {
            while (!finished.Wait(250))
            {
                double silentSeconds = (Stopwatch.GetTimestamp() - Volatile.Read(ref lastHeartbeat)) / (double)Stopwatch.Frequency;
                if (silentSeconds < 8 && elapsed.Elapsed.TotalSeconds < totalTimeoutSeconds) continue;
                Log($"FAIL watchdog: UI heartbeat silent {silentSeconds:F2}s; total {elapsed.Elapsed.TotalSeconds:F2}s");
                Environment.Exit(1);
            }
        }) { IsBackground = true, Name = "SaveMaster responsiveness watchdog" };

        PaintEventHandler onGridPaint = (_, _) => Interlocked.Increment(ref gridPaints);
        LayoutEventHandler onGridLayout = (_, _) => Interlocked.Increment(ref gridLayouts);
        EventHandler onSelection = (_, _) => Interlocked.Increment(ref selections);
        LayoutEventHandler onFormLayout = (_, _) => Interlocked.Increment(ref formLayouts);
        LayoutEventHandler onInputLayout = (_, _) => Interlocked.Increment(ref inputLayouts);
        EventHandler onVisibility = (_, _) => Interlocked.Increment(ref visibilityChanges);
        Control? inputParent = valueInput.Parent;
        grid.Paint += onGridPaint;
        grid.Layout += onGridLayout;
        grid.SelectionChanged += onSelection;
        Layout += onFormLayout;
        if (inputParent != null) inputParent.Layout += onInputLayout;
        valueInput.VisibleChanged += onVisibility;
        valueChoice.VisibleChanged += onVisibility;
        heartbeat.Tick += (_, _) =>
        {
            long now = Stopwatch.GetTimestamp();
            long gap = now - Interlocked.Exchange(ref lastHeartbeat, now);
            if (gap > maxHeartbeatGap) maxHeartbeatGap = gap;
            int count = Interlocked.Increment(ref heartbeats);
            if (count % 10 == 0) Log("heartbeat");
        };
        heartbeat.Start();
        watchdog.Start();

        try
        {
            // The launcher deliberately starts background helpers with SW_HIDE.
            // A second, application-owned show must produce real WM_PAINT traffic;
            // testing an invisible Form can hide the very repaint loop we need.
            Hide();
            Show();
            Activate();
            phase = "opening";
            Log("BEGIN open " + savePath);
            if (!await OpenFileAsync(savePath)) throw new InvalidOperationException("Initial asynchronous save open failed.");
            if (session is null) throw new InvalidOperationException("A verified save must be opened before the responsiveness check.");
            string source = session.FilePath;
            byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
            Log($"OPENED version={session.DocumentVersion}; domains={domainSnapshots.Count}; fields={allFields.Count}; rows={grid.RowCount}", new
            {
                source,
                sourceSha256 = Convert.ToHexStringLower(sourceHash),
                selectedField = SelectedField is { } selected ? selected.Domain + selected.Pointer : "",
                windowHandle = Handle.ToInt64(),
                gridHandle = grid.Handle.ToInt64(),
            });
            if (allFields.Count < 10 || grid.RowCount == 0) throw new InvalidOperationException("Opened save has no populated field table.");
            await ObserveAsync("loaded-idle");

            await CheckBlockedLoadAsync();
            await CheckFailedLoadAsync();

            search.Clear();
            changedOnly.Checked = false;
            if (tree.Nodes.Count > 0) tree.SelectedNode = tree.Nodes[0];
            await Task.Delay(350); // Let the actual debounced search handler run.
            SelectForCheck(field => field.Domain == "player.needs" && field.Key == "hunger", field => field.Kind == JsonValueKind.Number);
            await ObserveAsync("numeric-selected");

            SelectForCheck(field => field.Domain == "player.state" && field.Key == "crouching", field => field.Kind is JsonValueKind.True or JsonValueKind.False);
            await ObserveAsync("boolean-selected");
            if (!valueChoice.Visible || valueInput.Visible) throw new InvalidOperationException("Boolean editor did not settle on its dropdown.");

            SelectForCheck(field => field.Domain == "player.needs" && field.Key == "hunger", field => field.Kind == JsonValueKind.Number);
            await ObserveAsync("numeric-reselected");
            if (!valueInput.Visible || valueChoice.Visible) throw new InvalidOperationException("Numeric editor did not settle on its text input.");

            string originalInput = valueInput.Text;
            string pendingInput = originalInput == "37.25" ? "38.25" : "37.25";
            string pendingField = fieldPath.Text;
            phase = "uncommitted-input";
            Log("ACTION type a value without Apply, then reselect the same current cell");
            valueInput.Focus();
            valueInput.Text = pendingInput;
            int sameRow = grid.CurrentCell!.RowIndex;
            grid.ClearSelection();
            grid.CurrentCell = grid.Rows[sameRow].Cells[0];
            grid.Rows[sameRow].Selected = true;
            grid.Focus();
            await ObserveAsync("uncommitted-input");
            if (fieldPath.Text != pendingField || valueInput.Text != pendingInput)
                throw new InvalidOperationException("A same-cell selection or repaint erased the uncommitted value.");
            if (session.IsDirty) throw new InvalidOperationException("Typing without Apply unexpectedly edited the save model.");
            valueInput.Text = originalInput;
            Log("PASS uncommitted input survives same-cell selection and repaint without modifying the model");

            if (grid.RowCount > 20)
            {
                grid.FirstDisplayedScrollingRowIndex = Math.Max(0, grid.RowCount - 15);
                await ObserveAsync("scrolled-near-end");
                grid.FirstDisplayedScrollingRowIndex = 0;
            }

            phase = "search-input";
            Log("ACTION global search: hunger");
            search.Focus();
            search.Text = "hunger";
            await Task.Delay(400); // Do not bypass the debounce by calling RefreshGrid.
            if (!visibleFields.Any(field => field.Domain == "player.needs" && field.Key == "hunger"))
                throw new InvalidOperationException("Debounced global search did not display player hunger.");
            await ObserveAsync("search-results");

            await CheckSlotsAsync();
            if (includeWindowStress) await CheckWindowStressAsync();

            if (session.IsDirty) throw new InvalidOperationException("Selection, scrolling or search unexpectedly modified the save.");
            if (!sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source)))) throw new InvalidOperationException("Responsiveness check changed source bytes.");
            Log("PASS COMPLETE: blocked background I/O, failed opening, visible message loop, field editing, scrolling, search and My Saves remained responsive; source unchanged");
        }
        catch (Exception exception)
        {
            Log("FAIL " + exception);
            Environment.ExitCode = 1;
        }
        finally
        {
            finished.Set();
            watchdog.Join(1000);
            heartbeat.Stop();
            grid.Paint -= onGridPaint;
            grid.Layout -= onGridLayout;
            grid.SelectionChanged -= onSelection;
            Layout -= onFormLayout;
            if (inputParent != null) inputParent.Layout -= onInputLayout;
            valueInput.VisibleChanged -= onVisibility;
            valueChoice.VisibleChanged -= onVisibility;
            session = null; // Developer run only; never present an unsaved-edit prompt on shutdown.
            Close();
        }

        void SelectForCheck(Func<FieldRow, bool> preferred, Func<FieldRow, bool> fallback)
        {
            int index = visibleFields.FindIndex(field => preferred(field));
            if (index < 0) index = visibleFields.FindIndex(field => fallback(field));
            if (index < 0) throw new InvalidOperationException("Fixture lacks the requested numeric or boolean field.");
            phase = "selection-input";
            Log("ACTION select " + visibleFields[index].Domain + visibleFields[index].Pointer);
            grid.CurrentCell = grid.Rows[index].Cells[0];
            grid.Rows[index].Selected = true;
            grid.Focus();
        }

        async Task ObserveAsync(string name, int durationMilliseconds = 2000)
        {
            phase = name;
            Log("BEGIN " + name);
            await Task.Delay(200); // Exclude the deliberate selection/render transition from idle CPU.
            int beatsBefore = heartbeats, paintsBefore = gridPaints, layoutsBefore = inputLayouts, selectionsBefore = selections;
            int gridLayoutsBefore = gridLayouts, formLayoutsBefore = formLayouts;
            TimeSpan cpuBefore = process.TotalProcessorTime;
            long started = Stopwatch.GetTimestamp();
            maxHeartbeatGap = 0;
            grid.Invalidate(); // Queue a real paint; do not synchronously render it.
            await Task.Delay(durationMilliseconds); // Return through the real WinForms pump.
            double wallMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            double cpuMilliseconds = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
            int beatCount = heartbeats - beatsBefore;
            double largestGapMs = maxHeartbeatGap * 1000d / Stopwatch.Frequency;
            int paintCount = gridPaints - paintsBefore, layoutCount = inputLayouts - layoutsBefore, selectionCount = selections - selectionsBefore;
            int gridLayoutCount = gridLayouts - gridLayoutsBefore, formLayoutCount = formLayouts - formLayoutsBefore;
            string currentField = SelectedField is { } current ? current.Domain + current.Pointer : "";
            Log("MEASURE " + name, new
            {
                wallMilliseconds, cpuMilliseconds, beatCount, largestGapMs, paintCount, layoutCount, gridLayoutCount, formLayoutCount, selectionCount,
                currentField, editorField = fieldPath.Text, textInputVisible = valueInput.Visible, choiceInputVisible = valueChoice.Visible,
                durationMilliseconds, windowState = WindowState.ToString(), bounds = new { Left, Top, Width, Height },
            });
            if (paintCount == 0) throw new InvalidOperationException($"No real visible grid paint was processed during {name}; a hidden-window check is insufficient.");
            if (beatCount < Math.Max(8, durationMilliseconds / 250) || largestGapMs > 1200 || wallMilliseconds > durationMilliseconds + 2500)
                throw new InvalidOperationException($"UI message loop stalled during {name}: beats={beatCount}, maxGap={largestGapMs:F0}ms, wall={wallMilliseconds:F0}ms.");
            if (layoutCount > 100 || gridLayoutCount > 100 || formLayoutCount > 100 || selectionCount > 40)
                throw new InvalidOperationException($"Unbounded idle UI event activity during {name}: inputLayouts={layoutCount}, gridLayouts={gridLayoutCount}, formLayouts={formLayoutCount}, selections={selectionCount}.");
            // Relative to one logical core, not overall-machine CPU percentage.
            // A sustained idle repaint loop consumes most of one core even when
            // aggregate CPU looks small on a many-core workstation.
            if (cpuMilliseconds > wallMilliseconds * 0.8)
                throw new InvalidOperationException($"Idle CPU exceeded 80% of one logical core during {name}: cpu={cpuMilliseconds:F0}ms / wall={wallMilliseconds:F0}ms.");
            if (currentField.Length != 0 && fieldPath.Text != currentField)
                throw new InvalidOperationException($"Stale field editor during {name}: CurrentCell points to '{currentField}', editor still shows '{fieldPath.Text}'.");
            Log("PASS " + name);
        }

        async Task CheckWindowStressAsync()
        {
            if (session is null) throw new InvalidOperationException("Window stress requires fully loaded save data.");
            int rowCountBefore = grid.RowCount;
            string selectedBefore = SelectedField is { } selected ? selected.Domain + selected.Pointer : "";
            Rectangle normalBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            phase = "window-resize-input";
            Log("ACTION resize the loaded window");
            WindowState = FormWindowState.Normal;
            Size = new Size(
                normalBounds.Width > MinimumSize.Width ? Math.Max(MinimumSize.Width, normalBounds.Width - 180) : normalBounds.Width + 120,
                normalBounds.Height > MinimumSize.Height ? Math.Max(MinimumSize.Height, normalBounds.Height - 100) : normalBounds.Height + 80);
            await ObserveAsync("window-resized");
            AssertStableGrid();

            phase = "window-maximize-input";
            Log("ACTION maximize the loaded window");
            WindowState = FormWindowState.Maximized;
            await ObserveAsync("window-maximized");
            if (WindowState != FormWindowState.Maximized) throw new InvalidOperationException("The test-owned window did not maximize.");
            AssertStableGrid();

            phase = "window-restore-input";
            Log("ACTION restore the loaded window to its original bounds");
            WindowState = FormWindowState.Normal;
            Bounds = normalBounds;
            await ObserveAsync("window-restored");
            if (WindowState != FormWindowState.Normal) throw new InvalidOperationException("The test-owned window did not restore.");
            AssertStableGrid();

            await ObserveAsync("window-restored-long-idle", 10_000);
            AssertStableGrid();
            Log("PASS resize, maximize, restore and ten-second idle; field table and selection preserved");

            void AssertStableGrid()
            {
                string selectedNow = SelectedField is { } current ? current.Domain + current.Pointer : "";
                if (grid.RowCount != rowCountBefore || selectedNow != selectedBefore)
                    throw new InvalidOperationException("Resizing changed field-table membership or the selected field.");
            }
        }

        async Task CheckSlotsAsync()
        {
            string[] paths = Directory.Exists(NativeSaveDirectory)
                ? Directory.EnumerateDirectories(NativeSaveDirectory).Select(directory => Path.Combine(directory, "current.save.json")).Where(File.Exists).Order().ToArray()
                : [];
            if (paths.Length == 0)
            {
                Log("SKIP My Saves route: no local native slots exist.");
                return;
            }
            int targetIndex = Array.FindIndex(paths, path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(savePath), StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0) targetIndex = 0;
            var originalHashes = paths.ToDictionary(path => path, path => SHA256.HashData(File.ReadAllBytes(path)), StringComparer.OrdinalIgnoreCase);
            phase = "my-saves-dialog";
            Log("ACTION open My Saves and choose " + paths[targetIndex]);
            using var chooser = new System.Windows.Forms.Timer { Interval = 100 };
            DataGridView? slotsGrid = null;
            Form? slotsDialog = null;
            Exception? failure = null;
            int modalPaints = 0, beatsAtShow = 0;
            long shownAt = 0;
            PaintEventHandler onSlotsPaint = (_, _) => modalPaints++;
            chooser.Tick += (_, _) =>
            {
                try
                {
                    slotsDialog ??= OwnedForms.SingleOrDefault(form => form.Text == "Мои сохранения");
                    if (slotsDialog is null) return;
                    if (slotsGrid is null)
                    {
                        slotsGrid = Descendants(slotsDialog).OfType<DataGridView>().Single();
                        slotsGrid.Paint += onSlotsPaint;
                        slotsGrid.Invalidate();
                        shownAt = Stopwatch.GetTimestamp();
                        beatsAtShow = heartbeats;
                        Log("My Saves modal shown", new { rows = slotsGrid.RowCount, visible = slotsDialog.Visible });
                        return;
                    }
                    if ((Stopwatch.GetTimestamp() - shownAt) / (double)Stopwatch.Frequency < 2) return;
                    if (modalPaints == 0 || heartbeats - beatsAtShow < 8) throw new InvalidOperationException("My Saves modal did not paint or pump heartbeat messages.");
                    if (slotsGrid.RowCount <= targetIndex) throw new InvalidOperationException("My Saves did not list the expected slot.");
                    chooser.Stop();
                    slotsGrid.CurrentCell = slotsGrid.Rows[targetIndex].Cells[0];
                    Button choose = Descendants(slotsDialog).OfType<Button>().Single(button => button.Text == "Открыть слот");
                    Log("PASS My Saves modal heartbeat and paint", new { modalPaints, heartbeats = heartbeats - beatsAtShow });
                    choose.PerformClick();
                }
                catch (Exception exception)
                {
                    failure = exception;
                    chooser.Stop();
                    if (slotsDialog != null) slotsDialog.DialogResult = DialogResult.Cancel;
                }
            };
            chooser.Start();
            try { await ShowSlotsAsync(); }
            finally
            {
                chooser.Stop();
                if (slotsGrid != null) slotsGrid.Paint -= onSlotsPaint;
            }
            if (failure != null) throw failure;
            await Task.Delay(300);
            if (session is null || !string.Equals(session.FilePath, paths[targetIndex], StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("My Saves open button did not load its selected file.");
            await ObserveAsync("my-saves-loaded-idle");
            foreach ((string path, byte[] hash) in originalHashes)
                if (!hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)))) throw new InvalidOperationException("My Saves route changed source bytes: " + path);
            Log("PASS My Saves open route; all listed native source hashes unchanged");
        }

        async Task CheckBlockedLoadAsync()
        {
            SaveSession previousSession = session ?? throw new InvalidOperationException("Blocked-load check requires an open session.");
            IReadOnlyList<SaveDomain> previousSnapshots = domainSnapshots;
            List<FieldRow> previousFields = allFields;
            int uiThreadId = Environment.CurrentManagedThreadId;
            int openerThreadId = 0;
            using var gate = new ManualResetEventSlim();
            using var entered = new ManualResetEventSlim();
            phase = "blocked-io-start";
            Log("ACTION begin reload with deliberately blocked worker I/O");
            Task<bool> pending = OpenFileAsync(savePath, path =>
            {
                Interlocked.Exchange(ref openerThreadId, Environment.CurrentManagedThreadId);
                entered.Set();
                gate.Wait();
                return SaveSession.Open(path);
            }, showErrors: false);
            try
            {
                long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 3;
                while (!entered.IsSet && Stopwatch.GetTimestamp() < deadline) await Task.Delay(25);
                if (!entered.IsSet) throw new InvalidOperationException("The delayed opener did not start on a worker.");
                if (Volatile.Read(ref openerThreadId) == uiThreadId) throw new InvalidOperationException("The injected opener ran on the UI thread.");
                if (!isOpening || pending.IsCompleted) throw new InvalidOperationException("Blocked I/O was reported as a completed opening.");
                if (grid.Enabled || tree.Enabled || search.Enabled || changedOnly.Enabled || apply.Enabled || save.Enabled || saveCopy.Enabled)
                    throw new InvalidOperationException("Data editing remained enabled during opening.");
                await ObserveAsync("blocked-background-io");
                if (pending.IsCompleted || !ReferenceEquals(session, previousSession) || !ReferenceEquals(domainSnapshots, previousSnapshots) || !ReferenceEquals(allFields, previousFields))
                    throw new InvalidOperationException("A blocked load replaced or partially rebuilt the previous session before completion.");
                Log("PASS blocked worker I/O leaves prior session intact and the visible UI pumping", new { uiThreadId, openerThreadId });
            }
            finally { gate.Set(); }
            if (!await pending || session is null || ReferenceEquals(session, previousSession))
                throw new InvalidOperationException("Releasing the worker gate did not publish the newly prepared session.");
            if (isOpening || !grid.Enabled || !tree.Enabled || !search.Enabled || !saveCopy.Enabled)
                throw new InvalidOperationException("Successful asynchronous opening did not restore usable controls.");
            await ObserveAsync("released-background-io");
        }

        async Task CheckFailedLoadAsync()
        {
            SaveSession previousSession = session ?? throw new InvalidOperationException("Failure check requires an open session.");
            IReadOnlyList<SaveDomain> previousSnapshots = domainSnapshots;
            List<FieldRow> previousFields = allFields;
            phase = "failed-io-start";
            Log("ACTION inject an IOException in the background opener without a modal error dialog");
            bool succeeded = await OpenFileAsync(savePath,
                _ => throw new IOException("Responsiveness regression: injected read failure."), showErrors: false);
            if (succeeded || !ReferenceEquals(session, previousSession) || !ReferenceEquals(domainSnapshots, previousSnapshots) || !ReferenceEquals(allFields, previousFields))
                throw new InvalidOperationException("Failed opening lost the previous session or published a partial one.");
            if (isOpening || !grid.Enabled || !tree.Enabled || !search.Enabled || !changedOnly.Enabled || !saveCopy.Enabled)
                throw new InvalidOperationException("Failed opening left loading state or disabled controls behind.");
            await ObserveAsync("failed-background-io-recovery");
            Log("PASS failed worker opening retains the previous session and restores usable controls");
        }

        static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (Control descendant in Descendants(child)) yield return descendant;
            }
        }
    }
}
