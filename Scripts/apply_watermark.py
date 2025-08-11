import cv2
import numpy as np
import sys
import os

def load_image_unicode(path):
    stream = np.fromfile(path, dtype=np.uint8)
    image = cv2.imdecode(stream, cv2.IMREAD_UNCHANGED)
    if image is None:
        raise ValueError(f"Failed to decode image from {path}")
    return image

def save_image_unicode(path, image):
    ext = os.path.splitext(path)[1].lower()

    # If saving JPG and image has alpha, flatten on white background
    if ext in ['.jpg', '.jpeg'] and image.shape[2] == 4:
        alpha = image[:, :, 3] / 255.0
        background = np.ones_like(image[:, :, :3], dtype=np.uint8) * 255  # white
        for c in range(3):
            background[:, :, c] = (image[:, :, c] * alpha + background[:, :, c] * (1 - alpha)).astype(np.uint8)
        image = background

    params = []
    if ext in ['.jpg', '.jpeg']:
        params = [cv2.IMWRITE_JPEG_QUALITY, 100]
    elif ext == '.png':
        params = [cv2.IMWRITE_PNG_COMPRESSION, 3]

    result, encoded = cv2.imencode(ext, image, params)
    if not result:
        raise IOError(f"Failed to encode image for saving: {path}")
    encoded.tofile(path)


def apply_watermark(image, watermark_path, opacity=0.3):
    watermark = load_image_unicode(watermark_path)
    if watermark.shape[2] < 4:
        raise ValueError("Watermark must have alpha channel")

    wm_h, wm_w = watermark.shape[:2]
    img_h, img_w = image.shape[:2]

    # Center position
    x_offset = (img_w - wm_w) // 2
    y_offset = (img_h - wm_h) // 2

    # Clamp ROI to image boundaries
    x_start = max(x_offset, 0)
    y_start = max(y_offset, 0)
    x_end = min(x_offset + wm_w, img_w)
    y_end = min(y_offset + wm_h, img_h)

    # Corresponding watermark crop
    wm_x_start = max(-x_offset, 0)
    wm_y_start = max(-y_offset, 0)
    wm_x_end = wm_x_start + (x_end - x_start)
    wm_y_end = wm_y_start + (y_end - y_start)

    roi = image[y_start:y_end, x_start:x_end]
    wm_bgr = watermark[wm_y_start:wm_y_end, wm_x_start:wm_x_end, :3]
    wm_alpha = (watermark[wm_y_start:wm_y_end, wm_x_start:wm_x_end, 3] / 255.0) * opacity

    # Blend pixel-by-pixel
    for c in range(3):
        roi[:, :, c] = (wm_alpha * wm_bgr[:, :, c] + (1 - wm_alpha) * roi[:, :, c]).astype(np.uint8)

    image[y_start:y_end, x_start:x_end] = roi
    return image


def main():
    if len(sys.argv) < 4:
        print("Usage: apply_watermark.py input output watermark_path [opacity]")
        sys.exit(1)
    img = load_image_unicode(sys.argv[1])
    watermark_path = sys.argv[3]
    opacity = float(sys.argv[4]) if len(sys.argv) > 4 else 0.3
    result = apply_watermark(img, watermark_path, opacity)
    save_image_unicode(sys.argv[2], result)

if __name__ == "__main__":
    main()
