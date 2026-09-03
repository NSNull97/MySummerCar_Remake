using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Characters
{
    [Serializable]
    public struct CharacterAnimationBinding
    {
        [SerializeField] private CharacterActivityState state;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private bool loop;

        public CharacterAnimationBinding(
            CharacterActivityState configuredState,
            AnimationClip configuredClip,
            bool shouldLoop)
        {
            state = configuredState;
            clip = configuredClip;
            loop = shouldLoop;
        }

        public CharacterActivityState State => state;
        public AnimationClip Clip => clip;
        public bool Loop => loop;
    }

    [Serializable]
    public struct CharacterActionAnimationBinding
    {
        [SerializeField] private string actionId;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private bool loop;

        public CharacterActionAnimationBinding(
            string configuredActionId,
            AnimationClip configuredClip,
            bool shouldLoop)
        {
            actionId = configuredActionId ?? string.Empty;
            clip = configuredClip;
            loop = shouldLoop;
        }

        public string ActionId => actionId ?? string.Empty;
        public AnimationClip Clip => clip;
        public bool Loop => loop;
    }

    /// <summary>
    /// Project-owned wrapper around sanitized character mesh/clip presentation.
    /// It has no gameplay, schedule, dialogue, input or save authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LegacyCharacterPresentationBinding : MonoBehaviour
    {
        [SerializeField] private string bindingId = string.Empty;
        [SerializeField] private string productionReplacementKey = string.Empty;
        [SerializeField] private string geometryReplacementKey = string.Empty;
        [SerializeField] private string materialReplacementKey = string.Empty;
        [SerializeField] private string animationReplacementKey = string.Empty;
        [SerializeField] private Animation legacyAnimation;
        [SerializeField] private CharacterAnimationBinding[] stateBindings =
            Array.Empty<CharacterAnimationBinding>();
        [SerializeField] private CharacterActionAnimationBinding[] actionBindings =
            Array.Empty<CharacterActionAnimationBinding>();

        private CharacterActivityState currentState =
            CharacterActivityState.Hidden;
        private Coroutine actionResumeRoutine;
        private string activeActionId = string.Empty;
        private Transform animationTarget;
        private Vector3 authoredAnimationTargetLocalPosition;
        private Quaternion authoredAnimationTargetLocalRotation;
        private Vector3 authoredAnimationTargetLocalScale;
        private bool animationTargetBaselineCaptured;

        public string BindingId => bindingId;
        public string ProductionReplacementKey => productionReplacementKey;
        public string GeometryReplacementKey => geometryReplacementKey;
        public string MaterialReplacementKey => materialReplacementKey;
        public string AnimationReplacementKey => animationReplacementKey;
        public CharacterActivityState CurrentState => currentState;
        public Animation LegacyAnimation => legacyAnimation;
        public string ActiveActionId => activeActionId;

        public bool HasStateBinding(CharacterActivityState state)
        {
            CharacterAnimationBinding[] configured =
                stateBindings ?? Array.Empty<CharacterAnimationBinding>();
            for (int index = 0; index < configured.Length; index++)
            {
                if (configured[index].State == state &&
                    configured[index].Clip != null)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasActionBinding(string actionId)
        {
            CharacterActionAnimationBinding[] configured =
                actionBindings ?? Array.Empty<CharacterActionAnimationBinding>();
            for (int index = 0; index < configured.Length; index++)
            {
                if (string.Equals(
                        configured[index].ActionId,
                        actionId,
                        StringComparison.Ordinal) &&
                    configured[index].Clip != null)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetActionDuration(
            string actionId,
            out float durationSeconds)
        {
            CharacterActionAnimationBinding[] configured =
                actionBindings ?? Array.Empty<CharacterActionAnimationBinding>();
            for (int index = 0; index < configured.Length; index++)
            {
                if (string.Equals(
                        configured[index].ActionId,
                        actionId,
                        StringComparison.Ordinal) &&
                    configured[index].Clip != null)
                {
                    durationSeconds = Mathf.Max(
                        0.01f,
                        configured[index].Clip.length);
                    return true;
                }
            }

            durationSeconds = 0f;
            return false;
        }

        public bool TryGetActionClip(
            string actionId,
            out AnimationClip clip)
        {
            CharacterActionAnimationBinding[] configured =
                actionBindings ?? Array.Empty<CharacterActionAnimationBinding>();
            for (int index = 0; index < configured.Length; index++)
            {
                if (string.Equals(
                        configured[index].ActionId,
                        actionId,
                        StringComparison.Ordinal) &&
                    configured[index].Clip != null)
                {
                    clip = configured[index].Clip;
                    return true;
                }
            }

            clip = null;
            return false;
        }

        public bool TryValidate(out string failure)
        {
            if (!CharacterStableId.TryValidate(
                    bindingId,
                    "presentation.character.",
                    out failure) ||
                !CharacterStableId.TryValidate(
                    productionReplacementKey,
                    "presentation.character.",
                    out failure) ||
                !CharacterStableId.TryValidate(
                    geometryReplacementKey,
                    "presentation.character.",
                    out failure) ||
                !CharacterStableId.TryValidate(
                    materialReplacementKey,
                    "presentation.character.",
                    out failure) ||
                !CharacterStableId.TryValidate(
                    animationReplacementKey,
                    "presentation.character.",
                    out failure))
            {
                return false;
            }

            if (legacyAnimation == null)
            {
                failure = $"Character binding '{bindingId}' has no Animation component.";
                return false;
            }

            if (legacyAnimation.cullingType !=
                AnimationCullingType.AlwaysAnimate)
            {
                failure =
                    $"Character binding '{bindingId}' must always animate because its sanitized renderer can be outside the Animation transform branch.";
                return false;
            }

            if (GetComponentInChildren<Animator>(true) != null)
            {
                failure =
                    $"Character binding '{bindingId}' must not contain an Animator or donor controller.";
                return false;
            }

            if (GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
            {
                failure = $"Character binding '{bindingId}' has no skinned presentation.";
                return false;
            }

            var states = new HashSet<CharacterActivityState>();
            CharacterAnimationBinding[] configured =
                stateBindings ?? Array.Empty<CharacterAnimationBinding>();
            foreach (CharacterAnimationBinding entry in configured)
            {
                if (!states.Add(entry.State))
                {
                    failure =
                        $"Character binding '{bindingId}' duplicates state '{entry.State}'.";
                    return false;
                }

                if (entry.Clip == null || !entry.Clip.legacy)
                {
                    failure =
                        $"Character binding '{bindingId}' has a missing or non-legacy clip for '{entry.State}'.";
                    return false;
                }

                if (entry.Clip.events != null && entry.Clip.events.Length > 0)
                {
                    failure =
                        $"Character binding '{bindingId}' clip '{entry.Clip.name}' retained animation events.";
                    return false;
                }
            }

            var actions = new HashSet<string>(StringComparer.Ordinal);
            CharacterActionAnimationBinding[] configuredActions =
                actionBindings ?? Array.Empty<CharacterActionAnimationBinding>();
            foreach (CharacterActionAnimationBinding entry in configuredActions)
            {
                if (string.IsNullOrWhiteSpace(entry.ActionId) ||
                    !actions.Add(entry.ActionId))
                {
                    failure =
                        $"Character binding '{bindingId}' has an empty or duplicate action ID.";
                    return false;
                }

                if (entry.Clip == null || !entry.Clip.legacy)
                {
                    failure =
                        $"Character binding '{bindingId}' has a missing or non-legacy action clip for '{entry.ActionId}'.";
                    return false;
                }

                if (entry.Clip.events != null && entry.Clip.events.Length > 0)
                {
                    failure =
                        $"Character binding '{bindingId}' action clip '{entry.Clip.name}' retained animation events.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        public bool ApplyState(CharacterActivityState state)
        {
            if (state == CharacterActivityState.Hidden ||
                state == CharacterActivityState.Disabled)
            {
                StopPresentation();
                gameObject.SetActive(false);
                currentState = state;
                return true;
            }

            bool wasInactive = !gameObject.activeSelf;
            if (wasInactive)
            {
                gameObject.SetActive(true);
            }

            // An unchanged unbound state (for example VehicleSeated on the
            // Teimo bicycle wrapper) must not stop a presentation-only
            // one-shot every time the authoritative clock publishes a tick.
            if (!wasInactive && currentState == state)
            {
                return true;
            }

            CancelActionResume();

            CharacterAnimationBinding[] configured =
                stateBindings ?? Array.Empty<CharacterAnimationBinding>();
            for (int index = 0; index < configured.Length; index++)
            {
                if (configured[index].State != state ||
                    configured[index].Clip == null)
                {
                    continue;
                }

                AnimationClip clip = configured[index].Clip;
                if (legacyAnimation.GetClip(clip.name) == null)
                {
                    legacyAnimation.AddClip(clip, clip.name);
                }

                AnimationState animationState = legacyAnimation[clip.name];
                animationState.wrapMode = configured[index].Loop
                    ? WrapMode.Loop
                    : WrapMode.ClampForever;
                animationState.speed = 1f;
                RestoreAnimationTargetBaseline();
                legacyAnimation.Play(clip.name, PlayMode.StopAll);
                currentState = state;
                return true;
            }

            legacyAnimation.Stop();
            currentState = state;
            return false;
        }

        public bool TryPlayAction(
            string actionId,
            bool resumeStateAfter = true,
            bool restartStateAnimationAfter = true)
        {
            if (legacyAnimation == null || string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            CharacterActionAnimationBinding[] configured =
                actionBindings ?? Array.Empty<CharacterActionAnimationBinding>();
            for (int index = 0; index < configured.Length; index++)
            {
                CharacterActionAnimationBinding entry = configured[index];
                if (!string.Equals(
                        entry.ActionId,
                        actionId,
                        StringComparison.Ordinal) ||
                    entry.Clip == null)
                {
                    continue;
                }

                CancelActionResume();
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                AnimationClip clip = entry.Clip;
                if (legacyAnimation.GetClip(clip.name) == null)
                {
                    legacyAnimation.AddClip(clip, clip.name);
                }

                AnimationState animationState = legacyAnimation[clip.name];
                animationState.wrapMode = entry.Loop
                    ? WrapMode.Loop
                    : WrapMode.Once;
                animationState.speed = 1f;
                RestoreAnimationTargetBaseline();
                if (!legacyAnimation.Play(clip.name, PlayMode.StopAll))
                {
                    return false;
                }

                activeActionId = entry.ActionId;
                if (!entry.Loop && resumeStateAfter)
                {
                    actionResumeRoutine = StartCoroutine(
                        ResumeStateAfter(
                            clip.length,
                            restartStateAnimationAfter));
                }

                return true;
            }

            return false;
        }

        public void StopActionAndResumeState(
            bool restartStateAnimation = true)
        {
            CancelActionResume();
            if (restartStateAnimation)
            {
                CharacterActivityState state = currentState;
                currentState = CharacterActivityState.Hidden;
                ApplyState(state);
                return;
            }

            HoldStateEndPose(currentState);
        }

        public bool TryPlayOneShot(CharacterActivityState state)
        {
            CharacterAnimationBinding[] configured =
                stateBindings ?? Array.Empty<CharacterAnimationBinding>();
            for (int index = 0; index < configured.Length; index++)
            {
                if (configured[index].State != state ||
                    configured[index].Clip == null)
                {
                    continue;
                }

                AnimationClip clip = configured[index].Clip;
                if (legacyAnimation.GetClip(clip.name) == null)
                {
                    legacyAnimation.AddClip(clip, clip.name);
                }

                AnimationState animationState = legacyAnimation[clip.name];
                animationState.wrapMode = WrapMode.Once;
                animationState.speed = 1f;
                RestoreAnimationTargetBaseline();
                return legacyAnimation.Play(clip.name, PlayMode.StopAll);
            }

            return false;
        }

        public void StopPresentation()
        {
            CancelActionResume();
            if (legacyAnimation != null)
            {
                legacyAnimation.Stop();
            }

            RestoreAnimationTargetBaseline();
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredBindingId,
            string configuredReplacementKey,
            Animation configuredAnimation,
            IEnumerable<CharacterAnimationBinding> configuredBindings)
        {
            ConfigureForAuthoring(
                configuredBindingId,
                configuredReplacementKey,
                configuredReplacementKey + ".geometry",
                configuredReplacementKey + ".materials",
                configuredReplacementKey + ".animation",
                configuredAnimation,
                configuredBindings,
                Enumerable.Empty<CharacterActionAnimationBinding>());
        }

        public void ConfigureForAuthoring(
            string configuredBindingId,
            string configuredReplacementKey,
            string configuredGeometryReplacementKey,
            string configuredMaterialReplacementKey,
            string configuredAnimationReplacementKey,
            Animation configuredAnimation,
            IEnumerable<CharacterAnimationBinding> configuredBindings) =>
            ConfigureForAuthoring(
                configuredBindingId,
                configuredReplacementKey,
                configuredGeometryReplacementKey,
                configuredMaterialReplacementKey,
                configuredAnimationReplacementKey,
                configuredAnimation,
                configuredBindings,
                Enumerable.Empty<CharacterActionAnimationBinding>());

        public void ConfigureForAuthoring(
            string configuredBindingId,
            string configuredReplacementKey,
            string configuredGeometryReplacementKey,
            string configuredMaterialReplacementKey,
            string configuredAnimationReplacementKey,
            Animation configuredAnimation,
            IEnumerable<CharacterAnimationBinding> configuredBindings,
            IEnumerable<CharacterActionAnimationBinding> configuredActions)
        {
            bindingId = configuredBindingId ?? string.Empty;
            productionReplacementKey = configuredReplacementKey ?? string.Empty;
            geometryReplacementKey = configuredGeometryReplacementKey ?? string.Empty;
            materialReplacementKey = configuredMaterialReplacementKey ?? string.Empty;
            animationReplacementKey = configuredAnimationReplacementKey ?? string.Empty;
            legacyAnimation = configuredAnimation;
            stateBindings = (configuredBindings ??
                    Enumerable.Empty<CharacterAnimationBinding>())
                .OrderBy(binding => binding.State)
                .ToArray();
            actionBindings = (configuredActions ??
                    Enumerable.Empty<CharacterActionAnimationBinding>())
                .OrderBy(binding => binding.ActionId, StringComparer.Ordinal)
                .ToArray();
            foreach (CharacterAnimationBinding binding in stateBindings)
            {
                if (legacyAnimation != null &&
                    binding.Clip != null &&
                    legacyAnimation.GetClip(binding.Clip.name) == null)
                {
                    legacyAnimation.AddClip(binding.Clip, binding.Clip.name);
                }
            }

            foreach (CharacterActionAnimationBinding binding in actionBindings)
            {
                if (legacyAnimation != null &&
                    binding.Clip != null &&
                    legacyAnimation.GetClip(binding.Clip.name) == null)
                {
                    legacyAnimation.AddClip(binding.Clip, binding.Clip.name);
                }
            }

            if (legacyAnimation != null)
            {
                legacyAnimation.playAutomatically = false;
                legacyAnimation.animatePhysics = false;
                // Sanitized donor slices can keep the renderer as a sibling of
                // the animated skeleton. Renderer-based culling would then see
                // no renderer below this Animation component and freeze every
                // bone while the character remains visible.
                legacyAnimation.cullingType = AnimationCullingType.AlwaysAnimate;
            }
        }
#endif

        private IEnumerator ResumeStateAfter(
            float durationSeconds,
            bool restartStateAnimation)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, durationSeconds));
            actionResumeRoutine = null;
            activeActionId = string.Empty;
            if (restartStateAnimation)
            {
                CharacterActivityState state = currentState;
                currentState = CharacterActivityState.Hidden;
                ApplyState(state);
                yield break;
            }

            HoldStateEndPose(currentState);
        }

        private bool HoldStateEndPose(CharacterActivityState state)
        {
            CharacterAnimationBinding[] configured =
                stateBindings ?? Array.Empty<CharacterAnimationBinding>();
            for (int index = 0; index < configured.Length; index++)
            {
                if (configured[index].State != state ||
                    configured[index].Clip == null)
                {
                    continue;
                }

                AnimationClip clip = configured[index].Clip;
                if (legacyAnimation.GetClip(clip.name) == null)
                {
                    legacyAnimation.AddClip(clip, clip.name);
                }

                AnimationState animationState = legacyAnimation[clip.name];
                animationState.wrapMode = WrapMode.ClampForever;
                animationState.speed = 0f;
                RestoreAnimationTargetBaseline();
                if (!legacyAnimation.Play(clip.name, PlayMode.StopAll))
                {
                    return false;
                }

                animationState.time = clip.length;
                legacyAnimation.Sample();
                RestoreAnimationTargetBaseline();
                return true;
            }

            legacyAnimation.Stop();
            RestoreAnimationTargetBaseline();
            return false;
        }

        private void CancelActionResume()
        {
            if (actionResumeRoutine != null)
            {
                StopCoroutine(actionResumeRoutine);
                actionResumeRoutine = null;
            }

            activeActionId = string.Empty;
        }

        private void Awake() => CaptureAnimationTargetBaseline();

        private void LateUpdate()
        {
            // Donor walking/service clips contain root curves. Project-owned
            // navigation and service choreography already move the wrapper,
            // so allowing those curves to move the inner Animation target a
            // second time sends the visible skeleton through floors and away
            // from its interaction collider. Bones remain animated; only the
            // authored presentation root is pinned after legacy evaluation.
            RestoreAnimationTargetBaseline();
        }

        private void CaptureAnimationTargetBaseline()
        {
            if (animationTargetBaselineCaptured || legacyAnimation == null)
            {
                return;
            }

            animationTarget = legacyAnimation.transform;
            if (animationTarget == transform)
            {
                // Some future wrapper may deliberately animate its owner.
                // Never fight the authoritative world pose in that layout.
                animationTarget = null;
                animationTargetBaselineCaptured = true;
                return;
            }

            authoredAnimationTargetLocalPosition = animationTarget.localPosition;
            authoredAnimationTargetLocalRotation = animationTarget.localRotation;
            authoredAnimationTargetLocalScale = animationTarget.localScale;
            animationTargetBaselineCaptured = true;
        }

        private void RestoreAnimationTargetBaseline()
        {
            CaptureAnimationTargetBaseline();
            if (animationTarget == null)
            {
                return;
            }

            animationTarget.localPosition = authoredAnimationTargetLocalPosition;
            animationTarget.localRotation = authoredAnimationTargetLocalRotation;
            animationTarget.localScale = authoredAnimationTargetLocalScale;
        }

        private void OnDisable()
        {
            StopPresentation();
        }
    }

}
