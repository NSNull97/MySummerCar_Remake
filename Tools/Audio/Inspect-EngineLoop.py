"""Read-only PCM-float WAV spectral evidence; no playback-rate or source writes."""
import argparse
import hashlib
import json
import struct
from pathlib import Path
import numpy as np


def read_float_wave(path):
    raw = Path(path).read_bytes()
    if raw[:4] != b"RIFF" or raw[8:12] != b"WAVE":
        raise ValueError("Expected RIFF WAVE")
    pos, fmt, payload = 12, None, None
    while pos + 8 <= len(raw):
        kind, size = struct.unpack_from("<4sI", raw, pos)
        chunk = raw[pos + 8:pos + 8 + size]
        if len(chunk) != size:
            raise ValueError("Truncated RIFF chunk")
        if kind == b"fmt ":
            fmt = struct.unpack_from("<HHIIHH", chunk)
        if kind == b"data":
            payload = chunk
        pos += 8 + size + (size & 1)
    if fmt is None or payload is None or fmt[0] != 3 or fmt[5] != 32:
        raise ValueError("This evidence probe only supports IEEE float32 WAV")
    samples = np.frombuffer(payload, dtype="<f4").reshape(-1, fmt[1]).astype(np.float64)
    if not np.isfinite(samples).all():
        raise ValueError("Non-finite source samples")
    return raw, fmt[2], samples


def inspect(path):
    raw, rate, stereo = read_float_wave(path)
    mono = stereo.mean(axis=1)
    width = rate
    rows = []
    for start in range(0, len(mono) - width + 1, rate // 2):
        wave = mono[start:start + width]
        spectrum = np.abs(np.fft.rfft(wave * np.hanning(width))) ** 2
        frequency = np.fft.rfftfreq(width, 1 / rate)
        band = (frequency >= 50) & (frequency <= 800)
        energy = spectrum[band]
        freq = frequency[band]
        # Peaks/centroid describe this recording, not inferred mechanical RPM.
        peaks = np.argsort(energy)[-4:][::-1]
        rows.append(dict(second=round(start / rate, 3),
                         rmsDb=round(float(20 * np.log10(np.sqrt(np.mean(wave ** 2)) + 1e-15)), 3),
                         centroidHz=round(float(np.sum(freq * energy) / np.sum(energy)), 3),
                         strongestHz=[float(freq[i]) for i in peaks]))
    return dict(source=Path(path).name, sha256=hashlib.sha256(raw).hexdigest(),
                seconds=len(mono) / rate, sampleRate=rate, channels=stereo.shape[1], windows=rows)


def reviewed_segment_hash(path, start=352800, end=1278900, crossfade=882):
    """Independent byte-hash oracle for the C# importer: 8..29s, 20ms seam."""
    raw, rate, samples = read_float_wave(path)
    channels = samples.shape[1]
    prepared = samples[start + crossfade:end].copy()
    weights = np.arange(1, crossfade + 1, dtype=np.float64)[:, None]
    prepared[-crossfade:] = (prepared[-crossfade:] * (crossfade - weights) +
                            samples[start:start + crossfade] * weights) / crossfade
    payload = prepared.astype('<f4').tobytes()
    header = struct.pack('<4sI4s4sIHHIIHH4sI', b'RIFF', len(payload)+36, b'WAVE', b'fmt ',
                         16, 3, channels, rate, rate*channels*4, channels*4, 32, b'data', len(payload))
    return dict(sourceSha256=hashlib.sha256(raw).hexdigest(),
                preparedSha256=hashlib.sha256(header+payload).hexdigest(),
                startFrame=start, endFrame=end, crossfadeFrames=crossfade,
                frames=len(prepared), seconds=len(prepared)/rate,
                seamMaximumDelta=float(np.abs(prepared[0]-prepared[-1]).max()))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("source")
    args = parser.parse_args()
    print(json.dumps(inspect(args.source), indent=2))
