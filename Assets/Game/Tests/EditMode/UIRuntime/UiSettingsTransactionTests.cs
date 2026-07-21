using System;
using MSC.UI.Runtime.Settings;
using NUnit.Framework;

namespace MSC.Tests.EditMode.UIRuntime
{
    public sealed class UiSettingsTransactionTests
    {
        [Test]
        public void PendingEdits_AreIsolatedUntilApplyAndCanRevert()
        {
            UiSettingsDocument initial = UiSettingsDefaults.Create();
            UiSettingsTransactionService service = new UiSettingsTransactionService(initial);

            service.EditPending(settings => settings.Audio.Master01 = 0.4f);

            Assert.That(service.HasPendingChanges, Is.True);
            Assert.That(service.Pending.Audio.Master01, Is.EqualTo(0.4f));
            Assert.That(service.Applied.Audio.Master01, Is.EqualTo(1f));

            service.RevertPending();

            Assert.That(service.HasPendingChanges, Is.False);
            Assert.That(service.Pending.Audio.Master01, Is.EqualTo(1f));
        }

        [Test]
        public void ApplyPending_ReturnsDetachedAppliedSnapshot()
        {
            UiSettingsTransactionService service = new UiSettingsTransactionService(UiSettingsDefaults.Create());
            service.EditPending(settings =>
            {
                settings.Gameplay.HudMode = UiHudMode.Contextual;
                settings.Controls.VehicleBindingOverridesJson = "[]";
            });

            UiSettingsDocument applied = service.ApplyPending();
            applied.Gameplay.HudMode = UiHudMode.Off;

            Assert.That(service.HasPendingChanges, Is.False);
            Assert.That(service.Applied.Gameplay.HudMode, Is.EqualTo(UiHudMode.Contextual));
            Assert.That(service.Applied.Controls.VehicleBindingOverridesJson, Is.EqualTo("[]"));
        }

        [Test]
        public void InvalidEdit_DoesNotPartiallyMutatePendingSnapshot()
        {
            UiSettingsTransactionService service = new UiSettingsTransactionService(UiSettingsDefaults.Create());
            UiSettingsDocument before = service.Pending;

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                service.EditPending(settings => settings.Controls.GamepadDeadzone = 1.5f));

            Assert.That(service.Pending.ContentEquals(before), Is.True);
            Assert.That(service.HasPendingChanges, Is.False);
        }

        [Test]
        public void ResetPendingToDefaults_UsesDetachedDefaultSnapshot()
        {
            UiSettingsDocument applied = UiSettingsDefaults.Create();
            applied.Graphics.VSync = false;
            UiSettingsTransactionService service = new UiSettingsTransactionService(applied);

            service.ResetPendingToDefaults();
            UiSettingsDocument pending = service.Pending;
            pending.Graphics.VSync = false;

            Assert.That(service.Pending.Graphics.VSync, Is.True);
            Assert.That(service.HasPendingChanges, Is.True);
        }
    }
}
