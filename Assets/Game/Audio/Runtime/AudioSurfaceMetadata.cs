using System;

namespace MSC.Audio
{
    /// <summary>
    /// Project-owned acoustic material identity shared by footsteps, impacts,
    /// and vehicle contact presentation. It is deliberately independent from
    /// renderer material names and vendor switch objects.
    /// </summary>
    public enum AudioSurfaceKind
    {
        Unknown = 0,
        Paved = 1,
        Gravel = 2,
        Dirt = 3,
        Grass = 4,
        Wood = 5,
        Concrete = 6,
        Metal = 7,
        Wet = 8,
    }

    public readonly struct AudioSurfaceMetadata
    {
        public AudioSurfaceMetadata(
            AudioSurfaceKind kind,
            float wetness01 = 0f,
            float roughness01 = 0.5f)
        {
            if (!Enum.IsDefined(typeof(AudioSurfaceKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Kind = kind;
            Wetness01 = AudioMath.Clamp01(wetness01);
            Roughness01 = AudioMath.Clamp01(roughness01);
        }

        public AudioSurfaceKind Kind { get; }
        public float Wetness01 { get; }
        public float Roughness01 { get; }

        public static AudioSurfaceMetadata Unknown => new AudioSurfaceMetadata(
            AudioSurfaceKind.Unknown);
    }

    /// <summary>
    /// Typed surface boundary. Runtime audio must not infer a surface from a
    /// GameObject, renderer material, texture, or hierarchy name.
    /// </summary>
    public interface IAudioSurfaceMetadataProvider
    {
        AudioSurfaceMetadata AudioSurface { get; }
    }

    public static class AudioSurfaceMapping
    {
        public static AudioSwitchId ToFootstepSwitch(in AudioSurfaceMetadata metadata)
        {
            AudioSurfaceKind kind = metadata.Kind;
            if (metadata.Wetness01 >= 0.5f)
            {
                kind = AudioSurfaceKind.Wet;
            }

            switch (kind)
            {
                case AudioSurfaceKind.Paved:
                    return AudioProjectIds.Switches.FootstepSurfacePaved;
                case AudioSurfaceKind.Gravel:
                    return AudioProjectIds.Switches.FootstepSurfaceGravel;
                case AudioSurfaceKind.Dirt:
                    return AudioProjectIds.Switches.FootstepSurfaceDirt;
                case AudioSurfaceKind.Grass:
                    return AudioProjectIds.Switches.FootstepSurfaceGrass;
                case AudioSurfaceKind.Wood:
                    return AudioProjectIds.Switches.FootstepSurfaceWood;
                case AudioSurfaceKind.Concrete:
                    return AudioProjectIds.Switches.FootstepSurfaceConcrete;
                case AudioSurfaceKind.Metal:
                    return AudioProjectIds.Switches.FootstepSurfaceMetal;
                case AudioSurfaceKind.Wet:
                    return AudioProjectIds.Switches.FootstepSurfaceWet;
                case AudioSurfaceKind.Unknown:
                default:
                    return AudioProjectIds.Switches.FootstepSurfaceUnknown;
            }
        }

        public static AudioSurfaceContext ToSurfaceContext(
            in AudioSurfaceMetadata metadata)
        {
            AudioSwitchId surfaceSwitch;
            switch (metadata.Kind)
            {
                case AudioSurfaceKind.Paved:
                case AudioSurfaceKind.Wood:
                case AudioSurfaceKind.Concrete:
                case AudioSurfaceKind.Metal:
                    surfaceSwitch = AudioProjectIds.Switches.SurfacePaved;
                    break;
                case AudioSurfaceKind.Gravel:
                    surfaceSwitch = AudioProjectIds.Switches.SurfaceGravel;
                    break;
                case AudioSurfaceKind.Dirt:
                    surfaceSwitch = AudioProjectIds.Switches.SurfaceDirt;
                    break;
                case AudioSurfaceKind.Grass:
                    surfaceSwitch = AudioProjectIds.Switches.SurfaceGrass;
                    break;
                case AudioSurfaceKind.Wet:
                    surfaceSwitch = AudioProjectIds.Switches.SurfaceMudWet;
                    break;
                case AudioSurfaceKind.Unknown:
                default:
                    surfaceSwitch = AudioProjectIds.Switches.SurfaceUnknown;
                    break;
            }

            return new AudioSurfaceContext(
                surfaceSwitch,
                metadata.Wetness01,
                metadata.Roughness01);
        }
    }
}
