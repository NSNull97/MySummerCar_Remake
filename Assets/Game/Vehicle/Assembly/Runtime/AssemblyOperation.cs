namespace MSC.Vehicle.Assembly
{
    public enum AssemblyOperation
    {
        Install = 0,
        InsertFastener = 1,
        TightenFastener = 2,
        LoosenFastener = 3,
        Remove = 4,
        Restore = 5,
        RemoveFastener = 6
    }

    public enum AssemblyFailureReason
    {
        None = 0,
        InvalidPart = 1,
        InvalidMount = 2,
        AlreadyInstalled = 3,
        MountOccupied = 4,
        Incompatible = 5,
        OutsidePositionTolerance = 6,
        OutsideAngularTolerance = 7,
        MissingPrerequisite = 8,
        Obstructed = 9,
        InvalidFastener = 10,
        InvalidTool = 11,
        FastenerAtLimit = 12,
        FastenerSecured = 13,
        RemovalBlocked = 14,
        InvalidSaveData = 15,
        DuplicateState = 16
    }

    public readonly struct AssemblyOperationResult
    {
        private AssemblyOperationResult(
            bool succeeded,
            AssemblyOperation operation,
            AssemblyFailureReason failureReason,
            string message)
        {
            Succeeded = succeeded;
            Operation = operation;
            FailureReason = failureReason;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }

        public AssemblyOperation Operation { get; }

        public AssemblyFailureReason FailureReason { get; }

        public string Message { get; }

        public static AssemblyOperationResult Success(AssemblyOperation operation, string message)
        {
            return new AssemblyOperationResult(true, operation, AssemblyFailureReason.None, message);
        }

        public static AssemblyOperationResult Failure(
            AssemblyOperation operation,
            AssemblyFailureReason reason,
            string message)
        {
            return new AssemblyOperationResult(false, operation, reason, message);
        }
    }
}
