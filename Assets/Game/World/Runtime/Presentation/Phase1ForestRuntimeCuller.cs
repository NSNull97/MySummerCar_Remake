using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.World.Presentation
{
    /// <summary>
    /// Keeps the globally owned Phase 1 forest compatible with the existing
    /// world scenes while activating only the spatial groups surrounding the
    /// current game camera. Scene View authoring remains uncullled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Phase1ForestRuntimeCuller : MonoBehaviour
    {
        [Serializable]
        private sealed class CellBinding
        {
            [SerializeField] private GameObject root;
            [SerializeField] private Bounds worldBounds;

            public GameObject Root => root;
            public Bounds WorldBounds => worldBounds;

            public CellBinding(GameObject cellRoot, Bounds bounds)
            {
                root = cellRoot;
                worldBounds = bounds;
            }
        }

        [SerializeField] private CellBinding[] cells =
            Array.Empty<CellBinding>();
        [SerializeField, Min(64f)] private float visibleDistanceMeters = 620f;
        [SerializeField, Min(0f)] private float hysteresisMeters = 160f;
        [SerializeField, Min(0.05f)] private float evaluationIntervalSeconds =
            0.2f;

        private float nextEvaluationTime;
        private Vector3 lastCameraPosition;
        private bool hasEvaluation;

        public int CellCount => cells != null ? cells.Length : 0;
        public float VisibleDistanceMeters => visibleDistanceMeters;

#if UNITY_EDITOR
        public void ConfigureGeneratedCells(
            GameObject[] cellRoots,
            Bounds[] cellBounds,
            float visibilityDistance,
            float visibilityHysteresis)
        {
            if (cellRoots == null ||
                cellBounds == null ||
                cellRoots.Length != cellBounds.Length)
            {
                throw new ArgumentException(
                    "Forest cell roots and bounds must have equal lengths.");
            }

            cells = new CellBinding[cellRoots.Length];
            for (int index = 0; index < cellRoots.Length; index++)
            {
                if (cellRoots[index] == null)
                {
                    throw new ArgumentException(
                        $"Forest cell root {index} is null.");
                }

                cells[index] =
                    new CellBinding(cellRoots[index], cellBounds[index]);
            }

            visibleDistanceMeters = Mathf.Max(64f, visibilityDistance);
            hysteresisMeters = Mathf.Max(0f, visibilityHysteresis);
        }
#endif

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SetAllCellsActive(false);
            nextEvaluationTime = 0f;
            hasEvaluation = false;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                RenderPipelineManager.beginCameraRendering +=
                    HandleBeginCameraRendering;
            }
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -=
                HandleBeginCameraRendering;
        }

        private void HandleBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            if (!Application.isPlaying ||
                camera == null ||
                camera.cameraType != CameraType.Game)
            {
                return;
            }

            Vector3 cameraPosition = camera.transform.position;
            bool movedFarEnough =
                !hasEvaluation ||
                (cameraPosition - lastCameraPosition).sqrMagnitude >= 64f;
            if (!movedFarEnough &&
                Time.unscaledTime < nextEvaluationTime)
            {
                return;
            }

            EvaluateCells(cameraPosition);
            lastCameraPosition = cameraPosition;
            hasEvaluation = true;
            nextEvaluationTime =
                Time.unscaledTime + evaluationIntervalSeconds;
        }

        private void EvaluateCells(Vector3 cameraPosition)
        {
            if (cells == null)
            {
                return;
            }

            for (int index = 0; index < cells.Length; index++)
            {
                CellBinding cell = cells[index];
                if (cell == null || cell.Root == null)
                {
                    continue;
                }

                Bounds bounds = cell.WorldBounds;
                cameraPosition.y = bounds.center.y;
                float distance = cell.Root.activeSelf
                    ? visibleDistanceMeters + hysteresisMeters
                    : visibleDistanceMeters;
                bool shouldBeActive =
                    bounds.SqrDistance(cameraPosition) <=
                    distance * distance;
                if (cell.Root.activeSelf != shouldBeActive)
                {
                    cell.Root.SetActive(shouldBeActive);
                }
            }
        }

        private void SetAllCellsActive(bool active)
        {
            if (cells == null)
            {
                return;
            }

            for (int index = 0; index < cells.Length; index++)
            {
                GameObject root = cells[index]?.Root;
                if (root != null && root.activeSelf != active)
                {
                    root.SetActive(active);
                }
            }
        }
    }
}
