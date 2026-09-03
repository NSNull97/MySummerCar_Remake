namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Optional capability for actors whose posture is constrained while they
    /// occupy a low interior. The volume owns overlap detection; the actor owns
    /// the exact posture policy and safe restoration.
    /// </summary>
    public interface IRestrictedInteriorPostureParticipant
    {
        void EnterRestrictedInteriorPosture();

        void ExitRestrictedInteriorPosture();
    }
}
