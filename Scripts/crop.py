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


def crop_max(img):
    if img.shape[2] == 4:
        alpha = img[:, :, 3]
        coords = cv2.findNonZero(alpha)
        if coords is None:
            raise ValueError("No non-transparent pixels found")
        x, y, w, h = cv2.boundingRect(coords)
    else:
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        _, thresh = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        if not contours:
            raise ValueError("No contours found")
        largest = max(contours, key=cv2.contourArea)
        x, y, w, h = cv2.boundingRect(largest)
    return img[y:y+h, x:x+w]

def main():
    if len(sys.argv) < 3:
        print("Usage: crop_max.py input output")
        sys.exit(1)
    img = load_image_unicode(sys.argv[1])
    cropped = crop_max(img)
    save_image_unicode(sys.argv[2], cropped)

if __name__ == "__main__":
    main()
