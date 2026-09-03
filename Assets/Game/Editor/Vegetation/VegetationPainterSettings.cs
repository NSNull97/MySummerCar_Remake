using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    [FilePath(
        "ProjectSettings/MSCVegetationPainterSettings.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class VegetationPainterSettings :
        ScriptableSingleton<VegetationPainterSettings>
    {
        [SerializeField] private int profileIndex;
        [SerializeField, Min(0.25f)] private float radius = 8f;
        [SerializeField, Range(0f, 1f)] private float strength = 0.35f;
        [SerializeField, Range(0f, 1f)] private float hardness = 0.55f;
        [SerializeField, Range(0.05f, 1f)] private float spacing = 0.2f;
        [SerializeField] private VegetationBrushOperation operation =
            VegetationBrushOperation.Paint;

        public int ProfileIndex
        {
            get => profileIndex;
            set => profileIndex = Mathf.Max(0, value);
        }

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0.25f, value);
        }

        public float Strength
        {
            get => strength;
            set => strength = Mathf.Clamp01(value);
        }

        public float Hardness
        {
            get => hardness;
            set => hardness = Mathf.Clamp01(value);
        }

        public float Spacing
        {
            get => spacing;
            set => spacing = Mathf.Clamp(value, 0.05f, 1f);
        }

        public VegetationBrushOperation Operation
        {
            get => operation;
            set => operation = value;
        }

        public void Persist()
        {
            Save(true);
        }
    }
}
