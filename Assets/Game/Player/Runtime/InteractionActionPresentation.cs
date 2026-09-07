using System;
using MSC.Interaction.Capabilities;

namespace MSC.Player
{
    public enum InteractionReticleKind
    {
        Dot = 0,
        Pickup = 1,
        Install = 2,
        Remove = 3,
    }

    /// <summary>
    /// Compact visual category for the currently preferred input binding.
    /// The HUD resolves this from the effective Input System path so a rebound
    /// action never keeps displaying the old mouse button.
    /// </summary>
    public enum InteractionBindingGlyphKind
    {
        None = 0,
        Keycap = 1,
        MouseGeneric = 2,
        MouseLeftButton = 3,
        MouseRightButton = 4,
        MouseMiddleButton = 5,
        MouseWheelScroll = 6,
    }

    public enum InteractionActionBinding
    {
        Interact = 0,
        Throw = 1,
        ToolActivate = 2,
        Scroll = 3,
        Wave = 4,
        MiddleFinger = 5,
        Swear = 6,
        DrivingMode = 7,
    }

    public readonly struct InteractionActionHint :
        IEquatable<InteractionActionHint>
    {
        public InteractionActionHint(
            InteractionActionBinding binding,
            string label,
            InteractionScrollDirection? scrollDirection = null)
        {
            Binding = binding;
            Label = label ?? string.Empty;
            ScrollDirection = scrollDirection;
        }

        public InteractionActionBinding Binding { get; }

        public string Label { get; }

        public InteractionScrollDirection? ScrollDirection { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Label);

        public string InputActionName => Binding switch
        {
            InteractionActionBinding.Interact => "Interact",
            InteractionActionBinding.Throw => "Throw",
            InteractionActionBinding.ToolActivate => "ToolActivate",
            InteractionActionBinding.Scroll => "RotateModifier",
            InteractionActionBinding.Wave => "Wave",
            InteractionActionBinding.MiddleFinger => "MiddleFinger",
            InteractionActionBinding.Swear => "Swear",
            InteractionActionBinding.DrivingMode => "DrivingMode",
            _ => string.Empty,
        };

        public string DefaultBindingLabel => Binding switch
        {
            InteractionActionBinding.Interact => "ЛКМ",
            InteractionActionBinding.Throw => "ПКМ",
            InteractionActionBinding.ToolActivate => "F",
            InteractionActionBinding.Scroll => "КОЛЕСО",
            InteractionActionBinding.Wave => "H",
            InteractionActionBinding.MiddleFinger => "M",
            InteractionActionBinding.Swear => "N",
            InteractionActionBinding.DrivingMode => "ENTER",
            _ => string.Empty,
        };

        public bool Equals(InteractionActionHint other) =>
            Binding == other.Binding &&
            ScrollDirection == other.ScrollDirection &&
            string.Equals(Label, other.Label, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is InteractionActionHint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Binding;
                hash = hash * 397 ^ (ScrollDirection?.GetHashCode() ?? 0);
                hash = hash * 397 ^
                    (Label != null ? StringComparer.Ordinal.GetHashCode(Label) : 0);
                return hash;
            }
        }

        public static bool operator ==(
            InteractionActionHint left,
            InteractionActionHint right) => left.Equals(right);

        public static bool operator !=(
            InteractionActionHint left,
            InteractionActionHint right) => !left.Equals(right);
    }

    /// <summary>
    /// Allocation-free presentation snapshot. The reference permits no more
    /// than three simultaneous action plaques, so the storage is deliberately
    /// fixed rather than an open-ended collection.
    /// </summary>
    public readonly struct InteractionActionSnapshot :
        IEquatable<InteractionActionSnapshot>
    {
        public const int MaximumActions = 3;

        public InteractionActionSnapshot(InteractionReticleKind reticle)
            : this(reticle, 0, default, default, default)
        {
        }

        private InteractionActionSnapshot(
            InteractionReticleKind reticle,
            int actionCount,
            InteractionActionHint first,
            InteractionActionHint second,
            InteractionActionHint third)
        {
            Reticle = reticle;
            ActionCount = actionCount;
            First = first;
            Second = second;
            Third = third;
        }

        public static InteractionActionSnapshot Empty =>
            new InteractionActionSnapshot(InteractionReticleKind.Dot);

        public InteractionReticleKind Reticle { get; }

        public int ActionCount { get; }

        public InteractionActionHint First { get; }

        public InteractionActionHint Second { get; }

        public InteractionActionHint Third { get; }

        public InteractionActionHint GetAction(int index) => index switch
        {
            0 when ActionCount > 0 => First,
            1 when ActionCount > 1 => Second,
            2 when ActionCount > 2 => Third,
            _ => default,
        };

        public InteractionActionSnapshot WithReticle(
            InteractionReticleKind reticle) =>
            new InteractionActionSnapshot(
                reticle,
                ActionCount,
                First,
                Second,
                Third);

        public InteractionActionSnapshot Add(InteractionActionHint action)
        {
            if (!action.IsValid || ActionCount >= MaximumActions)
            {
                return this;
            }

            return ActionCount switch
            {
                0 => new InteractionActionSnapshot(
                    Reticle,
                    1,
                    action,
                    default,
                    default),
                1 => new InteractionActionSnapshot(
                    Reticle,
                    2,
                    First,
                    action,
                    default),
                _ => new InteractionActionSnapshot(
                    Reticle,
                    3,
                    First,
                    Second,
                    action),
            };
        }

        public InteractionActionSnapshot WithAlternativeActions() =>
            new InteractionActionSnapshot(Reticle)
                .Add(new InteractionActionHint(
                    InteractionActionBinding.Wave,
                    "ПОМАХАТЬ"))
                .Add(new InteractionActionHint(
                    InteractionActionBinding.MiddleFinger,
                    "ПОКАЗАТЬ ФАК"))
                .Add(new InteractionActionHint(
                    InteractionActionBinding.Swear,
                    "ВЫРУГАТЬСЯ"));

        public bool Equals(InteractionActionSnapshot other) =>
            Reticle == other.Reticle &&
            ActionCount == other.ActionCount &&
            First.Equals(other.First) &&
            Second.Equals(other.Second) &&
            Third.Equals(other.Third);

        public override bool Equals(object obj) =>
            obj is InteractionActionSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Reticle;
                hash = hash * 397 ^ ActionCount;
                hash = hash * 397 ^ First.GetHashCode();
                hash = hash * 397 ^ Second.GetHashCode();
                hash = hash * 397 ^ Third.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(
            InteractionActionSnapshot left,
            InteractionActionSnapshot right) => left.Equals(right);

        public static bool operator !=(
            InteractionActionSnapshot left,
            InteractionActionSnapshot right) => !left.Equals(right);
    }
}
