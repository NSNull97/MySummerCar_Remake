namespace MySummerRemake.SaveMaster.Core;

internal static class SafeSaveStorage
{
    internal static byte[] Read(string path)
    {
        ValidateOrdinaryPath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadBounded(stream);
    }

    internal static SaveWriteResult Write(string path, byte[] bytes, string? expectedHash)
    {
        path = Path.GetFullPath(path);
        ValidateOrdinaryPath(path);
        string directory = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Choose an existing destination directory.");
        if (expectedHash is null && File.Exists(path)) throw new SaveConflictException("Save As never overwrites an existing file. Choose a new name.");
        string suffix = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ", System.Globalization.CultureInfo.InvariantCulture) + "." + Guid.NewGuid().ToString("N");
        string temporaryPath = Path.Combine(directory, "." + Path.GetFileName(path) + ".savemaster." + suffix + ".tmp");
        string? backupPath = null;
        string displacedPath = path + ".savemaster-displaced." + suffix + ".bak";
        try
        {
            WriteNew(temporaryPath, bytes);
            _ = NativeSaveCodec.Read(Read(temporaryPath));
            if (expectedHash is null)
            {
                ValidateOrdinaryPath(path);
                File.Move(temporaryPath, path, false);
            }
            else
            {
                // Keep an independent flushed copy BEFORE replacement. File.Replace
                // also captures the displaced file, which detects a rename race that
                // a cooperative file-sharing lock cannot exclude.
                using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                byte[] current = ReadBounded(source);
                EnsureExpected(current, expectedHash);
                backupPath = path + ".savemaster." + suffix + ".bak";
                WriteNew(backupPath, current);
                if (NativeSaveCodec.Hash(Read(backupPath)) != expectedHash) throw new IOException("Backup verification failed; original save was not replaced.");
                ValidateOrdinaryPath(path);
                // The held stream prevents writes to the opened object. Re-open the
                // name for the final hash check because another process can rename it.
                using (var finalRead = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                    EnsureExpected(ReadBounded(finalRead), expectedHash);
                File.Replace(temporaryPath, path, displacedPath, false);
                if (NativeSaveCodec.Hash(Read(displacedPath)) != expectedHash)
                    throw new SaveConflictException("Another process replaced the file during the atomic commit. Its displaced version was preserved at: " + displacedPath + ". The edited file is at: " + path + ". Reopen and inspect both files before continuing.");
                File.Delete(displacedPath);
            }
            return new SaveWriteResult(path, backupPath, NativeSaveCodec.Hash(bytes));
        }
        finally
        {
            // Never remove backups on failure. Only our unique uncommitted temp is disposable.
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void EnsureExpected(byte[] current, string expected)
    {
        if (!string.Equals(NativeSaveCodec.Hash(current), expected, StringComparison.Ordinal))
            throw new SaveConflictException("The save changed on disk since it was opened. No overwrite was performed. Reopen it or export the edited copy with Save As.");
    }

    private static byte[] ReadBounded(FileStream stream)
    {
        if (stream.Length > SaveMasterLimits.MaximumDocumentBytes) throw new InvalidDataException("Save exceeds the 16 MiB document limit.");
        using var output = new MemoryStream((int)stream.Length);
        byte[] buffer = new byte[8192];
        int count;
        while ((count = stream.Read(buffer)) != 0)
        {
            if (output.Length + count > SaveMasterLimits.MaximumDocumentBytes) throw new InvalidDataException("Save grew past the 16 MiB document limit while reading.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(true);
    }

    // Refuse symlink/junction traversal and alternate data streams. This is path
    // hygiene, not a claim to defeat an adversary swapping directory handles.
    internal static void ValidateOrdinaryPath(string path)
    {
        path = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows() && path.AsSpan(Path.GetPathRoot(path)!.Length).Contains(':'))
            throw new IOException("Alternate data streams are not supported.");
        string? current = path;
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Save paths through symbolic links or junctions are refused. Open the physical location instead.");
            current = Path.GetDirectoryName(current);
        }
    }
}
