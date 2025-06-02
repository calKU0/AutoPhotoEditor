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
    ext = os.path.splitext(path)[1]
    result, encoded = cv2.imencode(ext, image)
    if not result:
        raise IOError(f"Failed to encode image for saving: {path}")
    encoded.tofile(path)

def crop_to_object(image_path, output_path, watermark_path=None, opacity=0.3):
    print("Received args:", sys.argv)

    # Load image with alpha channel using Unicode-safe method
    img = load_image_unicode(image_path)
    if img.shape[2] < 4:
        raise ValueError("Image does not have an alpha channel")

    alpha = img[:, :, 3]  # Extract alpha channel

    # Find bounding box of non-transparent pixels
    coords = cv2.findNonZero(alpha)
    if coords is None:
        raise ValueError("No non-transparent pixels found")

    x, y, w, h = cv2.boundingRect(coords)

    # Crop the image tightly
    cropped = img[y:y+h, x:x+w]

    # Resize canvas to 900px on longer side (with transparent padding, NOT image resize)
    h_cropped, w_cropped = cropped.shape[:2]
    if h_cropped > w_cropped:
        target_h = 900
        target_w = int(w_cropped * (900 / h_cropped))
    else:
        target_w = 900
        target_h = int(h_cropped * (900 / w_cropped))

    # Pad to target_w x target_h
    pad_top = (target_h - h_cropped) // 2 if target_h > h_cropped else 0
    pad_bottom = target_h - h_cropped - pad_top if target_h > h_cropped else 0
    pad_left = (target_w - w_cropped) // 2 if target_w > w_cropped else 0
    pad_right = target_w - w_cropped - pad_left if target_w > w_cropped else 0

    padded = cv2.copyMakeBorder(
        cropped,
        top=pad_top, bottom=pad_bottom,
        left=pad_left, right=pad_right,
        borderType=cv2.BORDER_CONSTANT,
        value=[0, 0, 0, 0]  # Transparent background
    )

    # Apply watermark AFTER padding, no resizing
    if watermark_path:
        watermark = load_image_unicode(watermark_path)
        if watermark.shape[2] < 4:
            raise ValueError("Watermark must be a PNG with alpha channel")

        wm_h, wm_w = watermark.shape[:2]
        padded_h, padded_w = padded.shape[:2]

        # Center watermark coordinates (may be negative if watermark larger than padded)
        x_offset = (padded_w - wm_w) // 2
        y_offset = (padded_h - wm_h) // 2

        # Calculate start and end coordinates in padded image
        x_start = max(x_offset, 0)
        y_start = max(y_offset, 0)
        x_end = min(x_offset + wm_w, padded_w)
        y_end = min(y_offset + wm_h, padded_h)

        # Calculate corresponding start coordinates in watermark image
        wm_x_start = max(-x_offset, 0)
        wm_y_start = max(-y_offset, 0)
        wm_x_end = wm_x_start + (x_end - x_start)
        wm_y_end = wm_y_start + (y_end - y_start)

        # Extract the ROI and corresponding part of the watermark
        roi = padded[y_start:y_end, x_start:x_end]
        wm_bgr = watermark[wm_y_start:wm_y_end, wm_x_start:wm_x_end, :3]
        wm_alpha = (watermark[wm_y_start:wm_y_end, wm_x_start:wm_x_end, 3] / 255.0) * opacity

        # Blend watermark with roi
        for c in range(3):
            roi[:, :, c] = (wm_alpha * wm_bgr[:, :, c] + (1 - wm_alpha) * roi[:, :, c]).astype(np.uint8)

        padded[y_start:y_end, x_start:x_end] = roi


    # Save final image with alpha channel using Unicode-safe method
    save_image_unicode(output_path, padded)
    print(f"Saved output to: {output_path}")

def main():
    print("Received args:", sys.argv)
    if len(sys.argv) < 3:
        print("Usage: script.py input output [watermark opacity]")
        return

    image_path = sys.argv[1]
    output_path = sys.argv[2]
    watermark = sys.argv[3] if len(sys.argv) > 3 else None
    opacity = float(sys.argv[4]) if len(sys.argv) > 4 else 0.3

    crop_to_object(image_path, output_path, watermark, opacity)

if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print("Exception:", str(e))
        sys.exit(1)
