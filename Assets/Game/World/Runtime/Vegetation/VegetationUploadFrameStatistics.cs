namespace MSC.World.Vegetation
{
    /// <summary>Aggregate incremental catalog preparation across all vegetation renderers.</summary>
    public readonly struct VegetationMetadataFrameStatistics
    {
        public int FrameIndex { get; }
        public int PreparationOperations { get; }
        public int ExaminedProfileRecords { get; }
        public double CpuMilliseconds { get; }

        internal VegetationMetadataFrameStatistics(
            int frameIndex,
            int operations,
            int records,
            double milliseconds)
        {
            FrameIndex = frameIndex;
            PreparationOperations = operations;
            ExaminedProfileRecords = records;
            CpuMilliseconds = milliseconds;
        }
    }

    /// <summary>Aggregate automatic upload work across all vegetation renderers and cameras.</summary>
    public readonly struct VegetationUploadFrameStatistics
    {
        public int FrameIndex { get; }
        public int UploadOperations { get; }
        public int UploadedInstances { get; }
        public int CreatedBuffers { get; }
        public double CpuMilliseconds { get; }

        internal VegetationUploadFrameStatistics(int frameIndex, int operations,
            int instances, int buffers, double milliseconds)
        {
            FrameIndex = frameIndex;
            UploadOperations = operations;
            UploadedInstances = instances;
            CreatedBuffers = buffers;
            CpuMilliseconds = milliseconds;
        }
    }

    internal sealed class VegetationGpuUploadBudget
    {
        private int frameIndex = -1;
        private int operations;
        private int instances;
        private int buffers;
        private double milliseconds;

        public VegetationUploadFrameStatistics Statistics =>
            new VegetationUploadFrameStatistics(frameIndex, operations, instances, buffers, milliseconds);

        public bool TryGetInstanceAllowance(int frame, out int allowance)
        {
            if (frameIndex != frame)
            {
                Reset();
                frameIndex = frame;
            }

            allowance = VegetationWorldRenderer.MaximumAutomaticUploadInstancesPerFrame - instances;
            return allowance > 0 &&
                   operations < VegetationWorldRenderer.MaximumAutomaticUploadOperationsPerFrame &&
                   milliseconds < VegetationWorldRenderer.AutomaticUploadBudgetMilliseconds;
        }

        public void Record(int uploadedInstances, int createdBuffers, double elapsedMilliseconds)
        {
            operations++;
            instances += uploadedInstances;
            buffers += createdBuffers;
            milliseconds += elapsedMilliseconds;
        }

        public void Reset()
        {
            frameIndex = -1;
            operations = 0;
            instances = 0;
            buffers = 0;
            milliseconds = 0d;
        }
    }

    internal sealed class VegetationMetadataPreparationBudget
    {
        private int frameIndex = -1;
        private int operations;
        private int records;
        private double milliseconds;

        public VegetationMetadataFrameStatistics Statistics =>
            new VegetationMetadataFrameStatistics(
                frameIndex,
                operations,
                records,
                milliseconds);

        public bool TryGetRecordAllowance(int frame, out int allowance)
        {
            if (frameIndex != frame)
            {
                Reset();
                frameIndex = frame;
            }

            allowance = VegetationWorldRenderer
                .MaximumAutomaticMetadataRecordsPerFrame - records;
            return allowance > 0 &&
                   operations < VegetationWorldRenderer
                       .MaximumAutomaticMetadataOperationsPerFrame &&
                   milliseconds < VegetationWorldRenderer
                       .AutomaticMetadataBudgetMilliseconds;
        }

        public void Record(int examinedRecords, double elapsedMilliseconds)
        {
            operations++;
            records += examinedRecords;
            milliseconds += elapsedMilliseconds;
        }

        public void Reset()
        {
            frameIndex = -1;
            operations = 0;
            records = 0;
            milliseconds = 0d;
        }
    }
}
