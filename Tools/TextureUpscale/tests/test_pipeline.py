from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image


TOOL_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOL_ROOT))

from pipeline_common import CONFIG_PATH, calculate_comparison_metrics, load_config  # noqa: E402
from upscale_textures import alpha_aware_resize, mask_map_resize, normal_map_resize  # noqa: E402


class TexturePipelineTests(unittest.TestCase):
    def test_config_is_json_compatible_yaml(self) -> None:
        config = load_config()
        self.assertEqual(1, config["schemaVersion"])
        with CONFIG_PATH.open("r", encoding="utf-8") as stream:
            self.assertEqual(config, json.load(stream))

    def test_normal_resize_renormalizes_vectors(self) -> None:
        width, height = 32, 24
        x = np.linspace(-0.45, 0.45, width, dtype=np.float32)[None, :]
        y = np.linspace(-0.3, 0.3, height, dtype=np.float32)[:, None]
        xx = np.broadcast_to(x, (height, width))
        yy = np.broadcast_to(y, (height, width))
        zz = np.sqrt(np.maximum(1.0 - xx * xx - yy * yy, 0.0))
        encoded = np.clip(np.rint((np.dstack((xx, yy, zz)) + 1.0) * 127.5), 0, 255).astype(np.uint8)
        source = Image.fromarray(encoded, mode="RGB")
        output = normal_map_resize(source, (128, 96)).convert("RGB")
        vectors = np.asarray(output, dtype=np.float32) / 127.5 - 1.0
        lengths = np.linalg.norm(vectors, axis=2)
        self.assertLess(float(np.abs(lengths - 1.0).mean()), 0.012)
        self.assertFalse(np.isnan(lengths).any())

    def test_mask_resize_preserves_binary_channels(self) -> None:
        source = np.zeros((16, 16, 4), dtype=np.uint8)
        source[:, 8:, 0] = 255
        source[:, :, 1] = np.linspace(20, 220, 16, dtype=np.uint8)[None, :]
        source[:, :, 2] = 128
        source[:8, :, 3] = 255
        output = np.asarray(mask_map_resize(Image.fromarray(source, mode="RGBA"), (64, 64)))
        self.assertTrue(set(np.unique(output[:, :, 0]).tolist()).issubset({0, 255}))
        self.assertTrue(set(np.unique(output[:, :, 3]).tolist()).issubset({0, 255}))
        self.assertGreater(len(np.unique(output[:, :, 1])), 4)
        self.assertTrue(np.all(output[:, :, 2] == 128))

    def test_alpha_aware_resize_preserves_binary_silhouette(self) -> None:
        source = np.zeros((16, 16, 4), dtype=np.uint8)
        source[3:13, 4:12, :3] = (30, 180, 50)
        source[3:13, 4:12, 3] = 255
        output = np.asarray(
            alpha_aware_resize(
                Image.fromarray(source, mode="RGBA"),
                (64, 64),
                binary_alpha=True,
            )
        )
        self.assertTrue(set(np.unique(output[:, :, 3]).tolist()).issubset({0, 255}))
        self.assertAlmostEqual(
            float(np.mean(source[:, :, 3] > 127)),
            float(np.mean(output[:, :, 3] > 127)),
            delta=0.02,
        )

    def test_metrics_detect_identity(self) -> None:
        source = Image.fromarray(np.full((32, 32, 3), 128, dtype=np.uint8), mode="RGB")
        metrics = calculate_comparison_metrics(source, source.copy())
        self.assertGreaterEqual(metrics["ssimBlock8"], 0.9999)
        self.assertGreaterEqual(metrics["psnr"], 90.0)
        self.assertLessEqual(metrics["meanColorDelta"], 1e-6)

    def test_metrics_ignore_rgb_hidden_below_zero_alpha(self) -> None:
        source = np.zeros((16, 16, 4), dtype=np.uint8)
        candidate = np.full((16, 16, 4), 255, dtype=np.uint8)
        source[:, :, 3] = 0
        candidate[:, :, 3] = 0
        metrics = calculate_comparison_metrics(
            Image.fromarray(source, mode="RGBA"),
            Image.fromarray(candidate, mode="RGBA"),
        )
        self.assertGreaterEqual(metrics["ssimBlock8"], 0.9999)
        self.assertLessEqual(metrics["meanColorDelta"], 1e-6)
        self.assertLessEqual(metrics["alphaCoverageDelta"], 1e-6)


if __name__ == "__main__":
    unittest.main()
