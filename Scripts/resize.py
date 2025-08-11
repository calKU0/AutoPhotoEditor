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


def resize_max_width(img, max_width=900):
    h, w = img.shape[:2]
    if w <= max_width:
        return img
    scale = max_width / w
    new_w = max_width
    new_h = int(h * scale)
    return cv2.resize(img, (new_w, new_h), interpolation=cv2.INTER_LANCZOS4)

def main():
    if len(sys.argv) < 3:
        print("Usage: resize_max_width.py input output [max_width]")
        sys.exit(1)
    img = load_image_unicode(sys.argv[1])
    max_width = int(sys.argv[3]) if len(sys.argv) > 3 else 900
    resized = resize_max_width(img, max_width)
    save_image_unicode(sys.argv[2], resized)

if __name__ == "__main__":
    main()
