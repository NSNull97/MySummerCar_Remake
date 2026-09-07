using System;
using System.IO;
using System.Text;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Deterministic, explicitly manifested trim of private float-WAV copies.
    /// Never mutates input bytes. This is not automatic silence removal: every
    /// end frame and optional loop seam was reviewed against the selected file.
    /// </summary>
    public static class UserAudioWavePreparation
    {
        public static byte[] Prepare(byte[] source, int endFrame, int loopCrossfadeFrames, int startFrame = 0)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (endFrame == 0 && loopCrossfadeFrames == 0 && startFrame == 0) return source;
            if (startFrame < 0 || endFrame <= startFrame || loopCrossfadeFrames < 0)
                throw new InvalidDataException("Invalid explicit audio preparation range.");
            using var input = new MemoryStream(source, false);
            using var reader = new BinaryReader(input, Encoding.ASCII, true);
            if (input.Length < 44 || Tag(reader) != "RIFF" || reader.ReadUInt32() != source.Length - 8 || Tag(reader) != "WAVE")
                throw new InvalidDataException("Prepared audio requires a complete little-endian RIFF/WAVE.");
            int rate = 0, channels = 0, dataOffset = -1, dataBytes = 0;
            while (input.Position + 8 <= input.Length)
            {
                string tag = Tag(reader);
                uint size = reader.ReadUInt32();
                long end = input.Position + size;
                if (end > input.Length) throw new InvalidDataException("Truncated WAV chunk.");
                if (tag == "fmt ")
                {
                    if (size < 16 || reader.ReadUInt16() != 3)
                        throw new InvalidDataException("Reviewed preparation accepts IEEE float PCM only.");
                    channels = reader.ReadUInt16();
                    rate = reader.ReadInt32();
                    int byteRate = reader.ReadInt32();
                    int alignment = reader.ReadUInt16();
                    int bits = reader.ReadUInt16();
                    if ((channels != 1 && channels != 2) || rate != 44100 || bits != 32 ||
                        alignment != channels * 4 || byteRate != rate * alignment)
                        throw new InvalidDataException("Unexpected reviewed WAV format.");
                }
                else if (tag == "data")
                {
                    if (dataOffset >= 0) throw new InvalidDataException("Multiple audio data chunks are not supported.");
                    dataOffset = checked((int)input.Position);
                    dataBytes = checked((int)size);
                }
                input.Position = end + (size & 1);
            }
            if (rate == 0 || dataOffset < 0 || dataBytes % (channels * 4) != 0 ||
                endFrame >= dataBytes / (channels * 4) ||
                loopCrossfadeFrames > rate / 50 || loopCrossfadeFrames * 2 >= endFrame - startFrame)
                throw new InvalidDataException("Prepared frame range is outside the reviewed audio.");

            int frames = endFrame - startFrame - loopCrossfadeFrames;
            int bytes = checked(frames * channels * 4);
            using var output = new MemoryStream(bytes + 44);
            using var writer = new BinaryWriter(output, Encoding.ASCII, true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(bytes + 36);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)3); writer.Write((short)channels); writer.Write(rate);
            writer.Write(rate * channels * 4); writer.Write((short)(channels * 4)); writer.Write((short)32);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            for (int frame = 0; frame < frames; frame++)
            {
                // The reviewed seam overlaps the beginning; dropping the consumed
                // prefix makes the next loop sample continue naturally.
                int originalFrame = startFrame + frame + loopCrossfadeFrames;
                int overlapFrame = frame - (frames - loopCrossfadeFrames);
                for (int channel = 0; channel < channels; channel++)
                {
                    float sample = BitConverter.ToSingle(source, dataOffset + (originalFrame * channels + channel) * 4);
                    if (loopCrossfadeFrames > 0 && overlapFrame >= 0)
                    {
                        float startSample = BitConverter.ToSingle(source, dataOffset + ((startFrame + overlapFrame) * channels + channel) * 4);
                        // Integer weights in double precision make the baked
                        // bytes identical under Editor Mono and .NET tooling.
                        sample = (float)(((double)sample * (loopCrossfadeFrames - overlapFrame - 1) +
                            (double)startSample * (overlapFrame + 1)) / loopCrossfadeFrames);
                    }
                    if (float.IsNaN(sample) || float.IsInfinity(sample))
                        throw new InvalidDataException("Non-finite prepared audio sample.");
                    writer.Write(sample);
                }
            }
            return output.ToArray();
        }

        private static string Tag(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
    }
}
