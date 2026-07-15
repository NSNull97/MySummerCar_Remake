using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class VehicleAssemblyPrototypeMarker : MonoBehaviour
    {
        [SerializeField]
        private string milestone = "05";

        [SerializeField]
        private string datasetVersion = "04B.4";

        public string Milestone => milestone;

        public string DatasetVersion => datasetVersion;
    }
}
