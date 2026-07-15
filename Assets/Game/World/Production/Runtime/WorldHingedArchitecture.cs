using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.World.Remaster
{
    [DisallowMultipleComponent]
    public sealed class WorldHingedArchitecture : MonoBehaviour, IContextInteractionTarget
    {
        [SerializeField] private Transform pivot;
        [SerializeField] private Vector3 closedLocalEuler;
        [SerializeField] private Vector3 openLocalEuler = new Vector3(0f, 90f, 0f);
        [SerializeField, Min(1f)] private float angularSpeedDegrees = 120f;
        [SerializeField] private string openPrompt = "Открыть";
        [SerializeField] private string closePrompt = "Закрыть";
        [SerializeField, Range(0f, 1f)] private float openNormalized;
        [SerializeField] private bool targetOpen;

        public string InteractionPrompt => targetOpen ? closePrompt : openPrompt;
        public float OpenNormalized => openNormalized;
        public bool TargetOpen => targetOpen;

        private void Awake()
        {
            if (pivot == null)
            {
                pivot = transform;
            }

            ApplyRotation();
        }

        private void Update()
        {
            float target = targetOpen ? 1f : 0f;
            if (Mathf.Approximately(openNormalized, target))
            {
                return;
            }

            float totalAngle = Quaternion.Angle(
                Quaternion.Euler(closedLocalEuler),
                Quaternion.Euler(openLocalEuler));
            float normalizedSpeed = totalAngle <= 0.001f
                ? 1f
                : angularSpeedDegrees / totalAngle;
            openNormalized = Mathf.MoveTowards(openNormalized, target, normalizedSpeed * Time.deltaTime);
            ApplyRotation();
        }

        public bool CanInteract(in InteractionContext context) => enabled && gameObject.activeInHierarchy;

        public void Interact(in InteractionContext context) => SetOpen(!targetOpen, immediate: false);

        public void Configure(
            Transform hingePivot,
            Vector3 closedEuler,
            Vector3 openEuler,
            float speedDegrees,
            string authoredOpenPrompt,
            string authoredClosePrompt)
        {
            pivot = hingePivot != null ? hingePivot : transform;
            closedLocalEuler = closedEuler;
            openLocalEuler = openEuler;
            angularSpeedDegrees = Mathf.Max(1f, speedDegrees);
            openPrompt = authoredOpenPrompt ?? "Открыть";
            closePrompt = authoredClosePrompt ?? "Закрыть";
            openNormalized = 0f;
            targetOpen = false;
            ApplyRotation();
        }

        public void SetOpen(bool value, bool immediate)
        {
            targetOpen = value;
            if (immediate)
            {
                openNormalized = value ? 1f : 0f;
                ApplyRotation();
            }
        }

        private void ApplyRotation()
        {
            if (pivot == null)
            {
                return;
            }

            pivot.localRotation = Quaternion.Slerp(
                Quaternion.Euler(closedLocalEuler),
                Quaternion.Euler(openLocalEuler),
                openNormalized);
        }
    }
}
