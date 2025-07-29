import sys
from rembg import remove
from rembg.session_factory import new_session
from PIL import Image
import io

def remove_background(input_path: str, output_path: str):
    # You can change model_name to: u2net, u2netp, isnet-general, etc.
    model_name = "isnet-general"  # or "u2net", "u2netp", etc.

    session = new_session(model_name=model_name)
    print(f"Using session: {session}")

    with open(input_path, "rb") as input_file:
        input_data = input_file.read()

    output_data = remove(
        input_data,
        session=session,
        alpha_matting=True,
        alpha_matting_erode_size=5,
        alpha_matting_foreground_threshold=240,
        alpha_matting_background_threshold=10
    )

    image = Image.open(io.BytesIO(output_data)).convert("RGBA")
    image.save(output_path)
    print(f"Saved output to: {output_path}")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python remove_bg.py <input_path> <output_path>")
        sys.exit(1)

    input_path = sys.argv[1]
    output_path = sys.argv[2]

    try:
        remove_background(input_path, output_path)
    except Exception as e:
        print("Error:", e)
        sys.exit(1)
