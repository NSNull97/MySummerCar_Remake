using UnityEngine;

namespace MSC.Development.WeatherLab
{
    public sealed class WeatherLabCaptureAnchor : MonoBehaviour
    {
        [SerializeField] private string captureId = string.Empty;
        [SerializeField] private Camera suggestedCamera;

        public string CaptureId => captureId ?? string.Empty;

        public Camera SuggestedCamera => suggestedCamera;

        public void ConfigureForAuthoring(string stableCaptureId, Camera camera)
        {
            captureId = stableCaptureId ?? string.Empty;
            suggestedCamera = camera;
        }
    }
}
