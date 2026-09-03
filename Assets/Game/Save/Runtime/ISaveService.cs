using System;
using System.Collections.Generic;

namespace MSC.Save
{
    /// <summary>
    /// Reports save-system activity without coupling consumers to storage or DTO implementations.
    /// </summary>
    public interface ISaveService
    {
        bool IsOperationInProgress { get; }
        event EventHandler<SaveOperationEventArgs> OperationStarted;
        event EventHandler<SaveOperationEventArgs> OperationCompleted;
        event EventHandler<SaveOperationEventArgs> OperationFailed;
        event EventHandler<SaveOperationEventArgs> RecoveryPerformed;

        SaveWriteResult Save(SaveRequest request);
        SaveLoadResult Load(string slotId);
        IReadOnlyList<SaveSlotSummary> EnumerateSlots();
    }
}
