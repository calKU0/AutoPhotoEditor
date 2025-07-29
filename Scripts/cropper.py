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

    if ext in ['.jpg', '.jpeg']:
        # Flatten transparent PNG (4 channels) on white before saving JPG
        if image.shape[2] == 4:
            alpha = image[:, :, 3] / 255.0
            # Create white background
            background = np.ones_like(image[:, :, :3], dtype=np.uint8) * 255
            # Composite foreground on white background
            for c in range(3):
                background[:, :, c] = (image[:, :, c] * alpha + background[:, :, c] * (1 - alpha)).astype(np.uint8)
            image = background

    result, encoded = cv2.imencode(ext, image)
    if not result:
        raise IOError(f"Failed to encode image for saving: {path}")
    encoded.tofile(path)

def apply_watermark(image, watermark_path, opacity=0.3):
    watermark = load_image_unicode(watermark_path)
    if watermark.shape[2] < 4:
        raise ValueError("Watermark must be PNG with alpha channel")

    wm_h, wm_w = watermark.shape[:2]
    img_h, img_w = image.shape[:2]

    x_offset = (img_w - wm_w) // 2
    y_offset = (img_h - wm_h) // 2

    x_start = max(x_offset, 0)
    y_start = max(y_offset, 0)
    x_end = min(x_offset + wm_w, img_w)
    y_end = min(y_offset + wm_h, img_h)

    wm_x_start = max(-x_offset, 0)
    wm_y_start = max(-y_offset, 0)
    wm_x_end = wm_x_start + (x_end - x_start)
    wm_y_end = wm_y_start + (y_end - y_start)

    roi = image[y_start:y_end, x_start:x_end]
    wm_bgr = watermark[wm_y_start:wm_y_end, wm_x_start:wm_x_end, :3]
    wm_alpha = (watermark[wm_y_start:wm_y_end, wm_x_start:wm_x_end, 3] / 255.0) * opacity

    for c in range(3):
        roi[:, :, c] = (wm_alpha * wm_bgr[:, :, c] + (1 - wm_alpha) * roi[:, :, c]).astype(np.uint8)

    image[y_start:y_end, x_start:x_end] = roi
    return image

def crop_image(img):
    if img.shape[2] == 4:
        # Crop based on alpha channel
        alpha = img[:, :, 3]
        coords = cv2.findNonZero(alpha)
        if coords is None:
            raise ValueError("No non-transparent pixels found")
        x, y, w, h = cv2.boundingRect(coords)
    else:
        # Crop based on largest contour from grayscale image
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        _, thresh = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
        kernel = np.ones((5, 5), np.uint8)
        cleaned = cv2.morphologyEx(thresh, cv2.MORPH_OPEN, kernel)
        contours, _ = cv2.findContours(cleaned, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        if not contours:
            raise ValueError("No contours found")
        largest = max(contours, key=cv2.contourArea)
        x, y, w, h = cv2.boundingRect(largest)

    cropped = img[y:y + h, x:x + w]
    h_cropped, w_cropped = cropped.shape[:2]
    max_dim = max(h_cropped, w_cropped)

    if max_dim > 900:
        # Scale down so largest side = 900
        scale = 900 / max_dim
        new_w = int(w_cropped * scale)
        new_h = int(h_cropped * scale)
        resized = cv2.resize(cropped, (new_w, new_h), interpolation=cv2.INTER_AREA)
        return resized
    else:
        # Pad the larger side to 900, keep aspect ratio, no resizing
        pad_top, pad_bottom, pad_left, pad_right = 0, 0, 0, 0

        if h_cropped >= w_cropped:
            # height is larger or equal, pad height side
            pad_total = 900 - h_cropped
            pad_top = pad_total // 2
            pad_bottom = pad_total - pad_top
        else:
            # width is larger, pad width side
            pad_total = 900 - w_cropped
            pad_left = pad_total // 2
            pad_right = pad_total - pad_left

        # Determine border color based on channels
        if cropped.shape[2] == 4:
            border_color = [0, 0, 0, 0]  # transparent padding for 4 channel
        else:
            border_color = [255, 255, 255]  # white padding for 3 channel

        padded = cv2.copyMakeBorder(
            cropped, pad_top, pad_bottom, pad_left, pad_right,
            cv2.BORDER_CONSTANT, value=border_color
        )
        return padded



def process_image(image_path, output_path, watermark_path=None, opacity=0.3, do_crop=False):
    img = load_image_unicode(image_path)

    if do_crop:
        img = crop_image(img)

    if watermark_path:
        img = apply_watermark(img, watermark_path, opacity)

    save_image_unicode(output_path, img)

def main():
    print("Args:", sys.argv)
    if len(sys.argv) < 4:
        print("Usage: script.py input output watermark_path|NONE crop_flag[0/1] opacity")
        return

    image_path = sys.argv[1]
    output_path = sys.argv[2]
    watermark = sys.argv[3] if sys.argv[3] != "NONE" else None
    do_crop = bool(int(sys.argv[4])) if len(sys.argv) > 4 else False
    opacity = float(sys.argv[5]) if len(sys.argv) > 5 else 0.3

    process_image(image_path, output_path, watermark, opacity, do_crop)

if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print("Exception:", str(e))
        sys.exit(1)
