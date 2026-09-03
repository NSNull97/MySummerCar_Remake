using UnityEngine;

namespace MSC.Presentation.AntiAliasing
{
    [CreateAssetMenu(
        fileName = "AntiAliasingSettings",
        menuName = "MSC/Presentation/Anti-Aliasing Settings")]
    public sealed class AntiAliasingSettings : ScriptableObject
    {
        public const string ResourcesPath = "AntiAliasing/AntiAliasingSettings";

        [Header("Startup")]
        [SerializeField] private AntiAliasingPreset defaultPreset = AntiAliasingPreset.High;
        [SerializeField] private bool persistPlayerSelection = true;

        [Header("Preset tuning")]
        [SerializeField] private AntiAliasingProfile low = AntiAliasingProfile.Create(
            AntiAliasingMode.Fxaa, 0f, 0.35f, 0.2f, 0.25f, 0.1f, false, 0.72f, 0.8f);
        [SerializeField] private AntiAliasingProfile medium = AntiAliasingProfile.Create(
            AntiAliasingMode.Smaa, 0f, 0.4f, 0.25f, 0.35f, 0.12f, false, 0.72f, 0.8f);
        [SerializeField] private AntiAliasingProfile high = AntiAliasingProfile.Create(
            AntiAliasingMode.Taa, 0.24f, 0.05f, 0.95f, 0.55f, 0f, true, 0.6f, 0.7f);
        [SerializeField] private AntiAliasingProfile ultra = AntiAliasingProfile.Create(
            AntiAliasingMode.Taa, 0.3f, 0.1f, 1f, 0.68f, 0.05f, true, 0.64f, 0.75f);

        [Header("Camera discontinuity detection")]
        [SerializeField, Min(0.1f)] private float teleportDistanceMeters = 8f;
        [SerializeField, Range(5f, 180f)] private float cameraCutAngleDegrees = 70f;
        [SerializeField, Range(0.1f, 45f)] private float fieldOfViewJumpDegrees = 5f;

        [Header("Camera policy")]
        [SerializeField] private AntiAliasingMode auxiliaryCameraMode = AntiAliasingMode.Smaa;
        [SerializeField] private bool transparentMotionVectors = true;
        [SerializeField] private bool showDevelopmentOverlay = true;

        public AntiAliasingPreset DefaultPreset => defaultPreset;
        public bool PersistPlayerSelection => persistPlayerSelection;
        public float TeleportDistanceMeters => teleportDistanceMeters;
        public float CameraCutAngleDegrees => cameraCutAngleDegrees;
        public float FieldOfViewJumpDegrees => fieldOfViewJumpDegrees;
        public AntiAliasingMode AuxiliaryCameraMode => auxiliaryCameraMode;
        public bool TransparentMotionVectors => transparentMotionVectors;
        public bool ShowDevelopmentOverlay => showDevelopmentOverlay;

        public AntiAliasingProfile GetProfile(AntiAliasingPreset preset)
        {
            switch (preset)
            {
                case AntiAliasingPreset.Low:
                    return low;
                case AntiAliasingPreset.Medium:
                    return medium;
                case AntiAliasingPreset.Ultra:
                    return ultra;
                default:
                    return high;
            }
        }

        internal static AntiAliasingSettings CreateRuntimeDefaults()
        {
            AntiAliasingSettings settings = CreateInstance<AntiAliasingSettings>();
            settings.name = "AntiAliasingSettings (Runtime Defaults)";
            settings.hideFlags = HideFlags.HideAndDontSave;
            return settings;
        }

        private void OnValidate()
        {
            teleportDistanceMeters = Mathf.Max(0.1f, teleportDistanceMeters);
            cameraCutAngleDegrees = Mathf.Clamp(cameraCutAngleDegrees, 5f, 180f);
            fieldOfViewJumpDegrees = Mathf.Clamp(fieldOfViewJumpDegrees, 0.1f, 45f);
        }
    }
}
