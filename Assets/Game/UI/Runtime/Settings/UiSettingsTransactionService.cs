using System;

namespace MSC.UI.Runtime.Settings
{
    public sealed class UiSettingsTransactionService
    {
        private UiSettingsDocument defaultsSnapshot;
        private UiSettingsDocument appliedSnapshot;
        private UiSettingsDocument pendingSnapshot;

        public UiSettingsTransactionService(
            UiSettingsDocument applied,
            UiSettingsDocument defaults = null)
        {
            defaultsSnapshot = (defaults ?? UiSettingsDefaults.Create()).DeepClone();
            appliedSnapshot = (applied ?? throw new ArgumentNullException(nameof(applied))).DeepClone();
            defaultsSnapshot.Validate();
            appliedSnapshot.Validate();
            pendingSnapshot = appliedSnapshot.DeepClone();
        }

        public event Action StateChanged;

        public UiSettingsDocument Defaults => defaultsSnapshot.DeepClone();

        public UiSettingsDocument Applied => appliedSnapshot.DeepClone();

        public UiSettingsDocument Pending => pendingSnapshot.DeepClone();

        public bool HasPendingChanges => !pendingSnapshot.ContentEquals(appliedSnapshot);

        public void ReplacePending(UiSettingsDocument replacement)
        {
            UiSettingsDocument candidate = (replacement ?? throw new ArgumentNullException(nameof(replacement))).DeepClone();
            candidate.Validate();
            pendingSnapshot = candidate;
            RaiseStateChanged();
        }

        public void EditPending(Action<UiSettingsDocument> edit)
        {
            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            UiSettingsDocument candidate = pendingSnapshot.DeepClone();
            edit(candidate);
            candidate.Validate();
            pendingSnapshot = candidate;
            RaiseStateChanged();
        }

        public void ResetPendingToDefaults()
        {
            pendingSnapshot = defaultsSnapshot.DeepClone();
            RaiseStateChanged();
        }

        public void RevertPending()
        {
            pendingSnapshot = appliedSnapshot.DeepClone();
            RaiseStateChanged();
        }

        public UiSettingsDocument ApplyPending()
        {
            pendingSnapshot.Validate();
            appliedSnapshot = pendingSnapshot.DeepClone();
            pendingSnapshot = appliedSnapshot.DeepClone();
            RaiseStateChanged();
            return appliedSnapshot.DeepClone();
        }

        public void ReplaceApplied(UiSettingsDocument replacement)
        {
            UiSettingsDocument candidate = (replacement ?? throw new ArgumentNullException(nameof(replacement))).DeepClone();
            candidate.Validate();
            appliedSnapshot = candidate;
            pendingSnapshot = candidate.DeepClone();
            RaiseStateChanged();
        }

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke();
        }
    }
}
