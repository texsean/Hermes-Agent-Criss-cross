"""
Image Change Detector
Uses OpenCV for pixel-level comparison between baseline and current images.

Strategy: Pixel differencing with configurable threshold.
For v1.1: upgrade to feature-vector comparison (ORB/SIFT) for lighting-invariant detection.
"""

import cv2
import numpy as np
import logging

logger = logging.getLogger("inventory_buddy.change_detector")


class ChangeDetector:
    """
    Compares two images and returns a change score + region list.

    Config:
        resize_to: (640, 480) — standardize dimensions for comparison
        pixel_threshold: 30 — pixel diff must exceed this to count as "changed"
        min_changed_pixels: 500 — minimum changed pixels to trigger "changed"
        blur_kernel: (5, 5) — Gaussian blur to reduce noise

    Returns:
        dict with keys: changed (bool), change_score (float 0-1),
                        changed_pixel_count (int), regions (list of dicts)
    """

    def __init__(
        self,
        resize_to: tuple = (640, 480),
        pixel_threshold: int = 30,
        min_changed_pixels: int = 500,
        blur_kernel: tuple = (5, 5),
    ):
        self.resize_to = resize_to
        self.pixel_threshold = pixel_threshold
        self.min_changed_pixels = min_changed_pixels
        self.blur_kernel = blur_kernel

    def compare(self, baseline_path: str, current_path: str) -> dict:
        """
        Compare current image against baseline.
        Returns change detection result.
        """
        baseline = cv2.imread(baseline_path)
        current = cv2.imread(current_path)

        if baseline is None:
            raise FileNotFoundError(f"Baseline image not found: {baseline_path}")
        if current is None:
            raise FileNotFoundError(f"Current image not found: {current_path}")

        # Resize both to standard dimensions
        baseline = cv2.resize(baseline, self.resize_to)
        current = cv2.resize(current, self.resize_to)

        # Gaussian blur to reduce noise (camera sensor noise, slight lighting shifts)
        baseline = cv2.GaussianBlur(baseline, self.blur_kernel, 0)
        current = cv2.GaussianBlur(current, self.blur_kernel, 0)

        # Compute absolute difference
        diff = cv2.absdiff(baseline, current)

        # Convert to grayscale for thresholding
        diff_gray = cv2.cvtColor(diff, cv2.COLOR_BGR2GRAY)

        # Threshold: pixels above pixel_threshold are "changed"
        _, thresh = cv2.threshold(diff_gray, self.pixel_threshold, 255, cv2.THRESH_BINARY)

        changed_pixel_count = np.count_nonzero(thresh)
        total_pixels = self.resize_to[0] * self.resize_to[1]
        change_score = changed_pixel_count / total_pixels

        changed = changed_pixel_count >= self.min_changed_pixels

        # Find changed regions (connected components / bounding boxes)
        regions = self._find_changed_regions(thresh)

        result = {
            "changed": changed,
            "change_score": round(change_score, 4),
            "changed_pixel_count": int(changed_pixel_count),
            "total_pixels": total_pixels,
            "regions": regions,
        }

        logger.debug(
            f"Change detection: changed={changed}, "
            f"score={change_score:.4f}, "
            f"pixels={changed_pixel_count}/{total_pixels}, "
            f"regions={len(regions)}"
        )

        return result

    def _find_changed_regions(self, thresh_image: np.ndarray) -> list[dict]:
        """
        Find connected components in the threshold image.
        Returns a list of bounding boxes for changed regions.
        """
        contours, _ = cv2.findContours(thresh_image, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        regions = []
        for contour in contours:
            # Filter out tiny noise regions
            area = cv2.contourArea(contour)
            if area < 100:  # minimum area in pixels
                continue

            x, y, w, h = cv2.boundingRect(contour)
            regions.append({
                "x": int(x),
                "y": int(y),
                "width": int(w),
                "height": int(h),
                "area": int(area),
            })

        # Sort by area descending (largest changes first)
        regions.sort(key=lambda r: r["area"], reverse=True)

        return regions[:20]  # cap at 20 regions to avoid overwhelming downstream


# Convenience function for manual baseline comparison
def compare_images(baseline_path: str, current_path: str) -> dict:
    """Quick comparison without instantiating the class."""
    detector = ChangeDetector()
    return detector.compare(baseline_path, current_path)
