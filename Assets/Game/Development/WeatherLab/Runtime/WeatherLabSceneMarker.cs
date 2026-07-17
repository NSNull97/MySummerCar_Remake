using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Development.WeatherLab
{
    public sealed class WeatherLabSceneMarker : MonoBehaviour
    {
        public const string SceneAssetPath =
            "Assets/Game/Development/WeatherLab/Scenes/WeatherLab.unity";

        [SerializeField] private string milestoneId = "07A";
        [SerializeField] private Camera[] cameras = Array.Empty<Camera>();
        [SerializeField] private WeatherLabCaptureAnchor[] captureAnchors =
            Array.Empty<WeatherLabCaptureAnchor>();

        public string MilestoneId => milestoneId ?? string.Empty;

        public IReadOnlyList<Camera> Cameras => cameras;

        public IReadOnlyList<WeatherLabCaptureAnchor> CaptureAnchors => captureAnchors;

        public void ConfigureForAuthoring(
            Camera[] authoredCameras,
            WeatherLabCaptureAnchor[] authoredCaptureAnchors)
        {
            milestoneId = "07A";
            cameras = authoredCameras ?? Array.Empty<Camera>();
            captureAnchors = authoredCaptureAnchors ?? Array.Empty<WeatherLabCaptureAnchor>();
        }

        public bool TryValidate(out string failure)
        {
            if (!string.Equals(milestoneId, "07A", StringComparison.Ordinal))
            {
                failure = "WeatherLab milestone marker must be 07A.";
                return false;
            }

            if (cameras == null || cameras.Length != 3)
            {
                failure = "WeatherLab requires exactly three deterministic cameras.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (captureAnchors == null || captureAnchors.Length < 6)
            {
                failure = "WeatherLab requires at least six capture anchors.";
                return false;
            }

            foreach (WeatherLabCaptureAnchor anchor in captureAnchors)
            {
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.CaptureId) ||
                    !ids.Add(anchor.CaptureId))
                {
                    failure = "WeatherLab capture anchors must be assigned and have unique IDs.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }
}
