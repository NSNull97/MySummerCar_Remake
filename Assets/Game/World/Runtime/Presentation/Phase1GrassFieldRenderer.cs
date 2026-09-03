using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.World.Presentation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Phase1GrassFieldRenderer : MonoBehaviour
    {
        private const int MaximumInstancesPerDraw = 1023;

        [SerializeField] private Phase1GrassFieldData fieldData;
        [SerializeField] private Mesh patchMesh;
        [SerializeField] private Material patchMaterial;
        [SerializeField, Min(10f)] private float gameDrawDistance = 115f;
        [SerializeField, Min(10f)] private float sceneDrawDistance = 180f;

        private readonly Matrix4x4[] matrices =
            new Matrix4x4[MaximumInstancesPerDraw];

        public int InstanceCount =>
            fieldData != null && fieldData.LocalPositions != null
                ? fieldData.LocalPositions.Length
                : 0;

#if UNITY_EDITOR
        public void ConfigureGeneratedField(
            Phase1GrassFieldData data,
            Mesh mesh,
            Material material,
            float runtimeDistance,
            float editorDistance)
        {
            fieldData = data;
            patchMesh = mesh;
            patchMaterial = material;
            gameDrawDistance = runtimeDistance;
            sceneDrawDistance = editorDistance;
        }
#endif

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering +=
                OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -=
                OnBeginCameraRendering;
        }

        private void OnBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            if (!isActiveAndEnabled ||
                camera == null ||
                fieldData == null ||
                patchMesh == null ||
                patchMaterial == null ||
                !patchMaterial.enableInstancing)
            {
                return;
            }
            if (camera.cameraType != CameraType.Game &&
                camera.cameraType != CameraType.SceneView)
            {
                return;
            }

            float drawDistance =
                camera.cameraType == CameraType.SceneView
                    ? sceneDrawDistance
                    : gameDrawDistance;
            Bounds worldBounds = TransformBounds(
                fieldData.LocalBounds,
                transform.localToWorldMatrix);
            Vector3 cameraPoint = camera.transform.position;
            cameraPoint.y = worldBounds.center.y;
            if (worldBounds.SqrDistance(cameraPoint) >
                drawDistance * drawDistance)
            {
                return;
            }

            Vector3[] positions = fieldData.LocalPositions;
            float maximumDistanceSquared =
                drawDistance * drawDistance;
            int matrixCount = 0;
            for (int index = 0; index < positions.Length; index++)
            {
                Vector3 worldPosition =
                    transform.TransformPoint(positions[index]);
                float deltaX =
                    worldPosition.x - camera.transform.position.x;
                float deltaZ =
                    worldPosition.z - camera.transform.position.z;
                if (deltaX * deltaX + deltaZ * deltaZ >
                    maximumDistanceSquared)
                {
                    continue;
                }

                uint random = Hash(index, worldPosition);
                float yaw = (random & 0xffffu) / 65535f * 360f;
                float scale = Mathf.Lerp(
                    0.82f,
                    1.18f,
                    ((random >> 16) & 0xffffu) / 65535f);
                matrices[matrixCount++] = Matrix4x4.TRS(
                    worldPosition,
                    Quaternion.Euler(0f, yaw, 0f),
                    Vector3.one * scale);
                if (matrixCount == matrices.Length)
                {
                    RenderBatch(
                        camera,
                        worldBounds,
                        matrixCount);
                    matrixCount = 0;
                }
            }

            if (matrixCount > 0)
            {
                RenderBatch(
                    camera,
                    worldBounds,
                    matrixCount);
            }
        }

        private void RenderBatch(
            Camera camera,
            Bounds worldBounds,
            int count)
        {
            var renderParams = new RenderParams(patchMaterial)
            {
                camera = camera,
                layer = gameObject.layer,
                worldBounds = worldBounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                motionVectorMode =
                    MotionVectorGenerationMode.ForceNoMotion
            };
            Graphics.RenderMeshInstanced(
                renderParams,
                patchMesh,
                0,
                matrices,
                count);
        }

        private static Bounds TransformBounds(
            Bounds localBounds,
            Matrix4x4 matrix)
        {
            Vector3 center =
                matrix.MultiplyPoint3x4(localBounds.center);
            Vector3 extents = localBounds.extents;
            Vector3 axisX =
                matrix.MultiplyVector(
                    new Vector3(extents.x, 0f, 0f));
            Vector3 axisY =
                matrix.MultiplyVector(
                    new Vector3(0f, extents.y, 0f));
            Vector3 axisZ =
                matrix.MultiplyVector(
                    new Vector3(0f, 0f, extents.z));
            extents = new Vector3(
                Mathf.Abs(axisX.x) +
                Mathf.Abs(axisY.x) +
                Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) +
                Mathf.Abs(axisY.y) +
                Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) +
                Mathf.Abs(axisY.z) +
                Mathf.Abs(axisZ.z));
            return new Bounds(center, extents * 2f);
        }

        private static uint Hash(
            int index,
            Vector3 position)
        {
            unchecked
            {
                uint hash = (uint)index * 747796405u + 2891336453u;
                hash ^= (uint)Mathf.RoundToInt(position.x * 10f) *
                        277803737u;
                hash ^= (uint)Mathf.RoundToInt(position.z * 10f) *
                        2246822519u;
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return hash;
            }
        }
    }
}
