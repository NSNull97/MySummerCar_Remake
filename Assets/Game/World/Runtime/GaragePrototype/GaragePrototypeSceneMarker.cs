using UnityEngine;

namespace MSC.World.GaragePrototype
{
    /// <summary>
    /// Declares the authored scope and measurable dimensions of the Milestone 3 scene.
    /// It contains no gameplay state and can be removed when the prototype becomes a world cell.
    /// </summary>
    public sealed class GaragePrototypeSceneMarker : MonoBehaviour
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField, Min(100f)] private float reconstructedRoadLengthMeters = 180f;
        [SerializeField] private bool productionContentOnly = true;
        [SerializeField] private string dimensionalAnchor =
            "garage_shed_roof Mesh PathID 2186; mapped donor-local bounds";

        public int SchemaVersion => schemaVersion;
        public float ReconstructedRoadLengthMeters => reconstructedRoadLengthMeters;
        public bool ProductionContentOnly => productionContentOnly;
        public string DimensionalAnchor => dimensionalAnchor;
    }
}
