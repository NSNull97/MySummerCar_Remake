using UnityEngine;

namespace MSC.Characters
{
    /// <summary>
    /// Keeps an isolated legacy presentation layer running across streaming
    /// disable/enable cycles. This component has no NPC state authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LegacyLoopingAnimationLayer : MonoBehaviour
    {
        [SerializeField] private Animation legacyAnimation;
        [SerializeField] private AnimationClip clip;
        [SerializeField, Min(0f)] private float startTimeSeconds;

        public Animation LegacyAnimation => legacyAnimation;
        public AnimationClip Clip => clip;
        public float StartTimeSeconds => startTimeSeconds;

        public bool TryValidate(out string failure)
        {
            if (legacyAnimation == null)
            {
                failure = "Legacy looping animation layer has no Animation component.";
                return false;
            }

            if (clip == null || !clip.legacy || clip.length <= 0f)
            {
                failure = "Legacy looping animation layer has no valid legacy clip.";
                return false;
            }

            if (legacyAnimation.GetClip(clip.name) != clip)
            {
                failure = "Legacy looping animation layer has not registered its clip.";
                return false;
            }

            if (!float.IsFinite(startTimeSeconds) || startTimeSeconds < 0f)
            {
                failure = "Legacy looping animation layer has an invalid start offset.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            RestartLayer();
        }

        private void LateUpdate()
        {
            if (legacyAnimation != null && clip != null &&
                !legacyAnimation.IsPlaying(clip.name))
            {
                RestartLayer();
            }
        }

        private void RestartLayer()
        {
            if (!TryValidate(out _))
            {
                return;
            }

            AnimationState state = legacyAnimation[clip.name];
            state.wrapMode = WrapMode.Loop;
            state.speed = 1f;
            legacyAnimation.Play(clip.name, PlayMode.StopSameLayer);
            state.time = Mathf.Repeat(startTimeSeconds, clip.length);
            legacyAnimation.Sample();
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            Animation configuredAnimation,
            AnimationClip configuredClip,
            float configuredStartTimeSeconds)
        {
            legacyAnimation = configuredAnimation;
            clip = configuredClip;
            startTimeSeconds = Mathf.Max(0f, configuredStartTimeSeconds);
            if (legacyAnimation == null || clip == null)
            {
                return;
            }

            if (legacyAnimation.GetClip(clip.name) == null)
            {
                legacyAnimation.AddClip(clip, clip.name);
            }

            legacyAnimation.clip = clip;
            legacyAnimation.playAutomatically = false;
            legacyAnimation.animatePhysics = false;
            legacyAnimation.cullingType = AnimationCullingType.AlwaysAnimate;
        }
#endif
    }
}
