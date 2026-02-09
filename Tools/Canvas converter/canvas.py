import tkinter as tk
from tkinter import filedialog, messagebox
from PIL import Image, ImageTk, ImageDraw, ImageFont
from math import sqrt
import requests
from io import BytesIO

def get_color_code_from_rgba(r, g, b, a = 255):
    """Converts RGBA values into a PaintingCode string format."""
    return f"{r / 255:.2f}|{g / 255:.2f}|{b / 255:.2f}|{a / 255:.2f}"


def get_rgba_from_color_code(color_code):
    """Converts a PaintingCode string format back into RGBA values."""
    try:
        components = list(map(float, color_code.split('|')))
        if len(components) == 4:
            r, g, b, a = components
            return int(r * 255), int(g * 255), int(b * 255), int(a * 255)
    except ValueError:
        pass
    return 255, 255, 255, 255  # Default to white if invalid



def open_file():
    file_path = filedialog.askopenfilename(filetypes=[("Image files", "*.jpg *.jpeg *.png *.bmp")])
    if file_path:
        load_image(file_path)


def open_fileold():
    """Open an image file and display it."""
    file_path = filedialog.askopenfilename(
        filetypes=[("Image files", "*.png *.jpg *.jpeg *.bmp *.gif")]
    )
    if not file_path:
        return

    try:
        image = Image.open(file_path).convert("RGB")
        img_display = ImageTk.PhotoImage(image.resize((200, 200)))
        original_img_label.config(image=img_display)
        original_img_label.image = img_display  # Keep a reference to prevent garbage collection
        app.image = image
    except Exception as e:
        messagebox.showerror("Error", f"Failed to open image: {e}")

# Function to fetch and display image from a URL
def fetch_image():
    url = url_entry.get().strip()
    try:
        response = requests.get(url, timeout=10)
        response.raise_for_status()  # Raise an error if the request fails
        image = Image.open(BytesIO(response.content))
        load_image(image)
    except Exception as e:
        tk.messagebox.showerror("Error", f"Could not fetch image: {e}")

# Function to load an image into the label
def load_image(image):
    global original_img
    if isinstance(image, str):  # If image is a file path
        image = Image.open(image)
    original_img = image.resize((150, 150))  # Resize for display
    img_tk = ImageTk.PhotoImage(original_img)
    original_img_label.config(image=img_tk)
    original_img_label.image = img_tk
    app.image = image

def convert_image_to_code(image, height, width):
    """Convert an image into PaintingCode format using segments and add dimensions prefix."""
    image = image.resize((width, height), Image.Resampling.NEAREST)  # Resize the image
    pixels = image.load()
    code = []

    for y in range(height):
        row_code = []
        for x in range(width):
            pixel = pixels[x, y]

            if isinstance(pixel, int):  # Grayscale
                r = g = b = pixel
                a = 255
            elif len(pixel) == 3:  # RGB
                r, g, b = pixel
                a = 255
            elif len(pixel) == 4:  # RGBA
                r, g, b, a = pixel
            else:
                raise ValueError("Unexpected pixel format. Supported formats are Grayscale, RGB, and RGBA.")

            segment = get_color_code_from_rgba(r, g, b, a)
            row_code.append(segment)

        code.append(";".join(row_code))

    # Combine everything and add dimensions prefix
    flat_code = ";\n".join(code)
    full_code = f"{width}x{height};{flat_code}"
    
    return full_code, code

def generate_image_from_code(code, height, width):
    """Generate an image from PaintingCode using segments, removing size prefix if present."""
    # Check if code starts with dimensions like 32x32;
    if ";" in code:
        first_part = code.split(";", 1)[0]
        if "x" in first_part:
            try:
                int(first_part.split("x")[0])
                int(first_part.split("x")[1])
                # Strip the prefix
                code = code[len(first_part) + 1:]
            except ValueError:
                pass  # Not a valid size prefix; ignore

    rows = code.strip().split(";\n")  # Split the PaintingCode into rows
    if len(rows) != height:
        raise ValueError("Code dimensions do not match specified height.")

    # Create a blank image
    image = Image.new("RGBA", (width, height))
    pixels = image.load()

    for y, row in enumerate(rows):
        segments = row.split(";")  # Split each row into segments
        if len(segments) != width:
            raise ValueError(f"Row {y} does not match specified width.")
        for x, segment in enumerate(segments):
            r, g, b, a = get_rgba_from_color_code(segment)
            pixels[x, y] = (r, g, b, a)

    # Scale up for better visibility
    return image.resize((width * 10, height * 10), Image.Resampling.NEAREST)



def preview_generated_image():
    """Preview the generated image from PaintingCode."""
    code = output_text.get(1.0, tk.END).strip()
    if not code:
        messagebox.showerror("Error", "Please generate or input PaintingCode first.")
        return

    try:
        height = int(height_entry.get())
        width = int(width_entry.get())
    except ValueError:
        messagebox.showerror("Error", "Height and Width must be integers.")
        return

    image = generate_image_from_code(code, height, width)
    if image:
        img_preview = ImageTk.PhotoImage(image)
        preview_img_label.config(image=img_preview)
        preview_img_label.image = img_preview  # Keep reference to prevent garbage collection


def generate_code():
    """Generate PaintingCode from the uploaded image."""
    if not hasattr(app, 'image'):
        messagebox.showerror("Error", "Please upload an image first.")
        return

    try:
        height = int(height_entry.get())
        width = int(width_entry.get())
    except ValueError:
        messagebox.showerror("Error", "Height and Width must be integers.")
        return

    code, _ = convert_image_to_code(app.image, height, width)
    output_text.delete(1.0, tk.END)
    output_text.insert(tk.END, code)
# Create the main application window
app = tk.Tk()
app.title("PaintingCode Converter with Preview")

# Define a main container for layout adjustment
main_frame = tk.Frame(app)
main_frame.pack(padx=10, pady=10)

# Left section for input controls
left_frame = tk.Frame(main_frame)
left_frame.pack(side=tk.LEFT, padx=10, pady=10)

# Right section for image preview
right_frame = tk.Frame(main_frame)
right_frame.pack(side=tk.RIGHT, padx=10, pady=10)

# File input section
file_frame = tk.Frame(left_frame)
file_frame.pack(pady=10)
file_button = tk.Button(file_frame, text="Upload Image", command=open_file)
file_button.pack(side=tk.LEFT, padx=5)

url_entry = tk.Entry(file_frame, width=40)
url_entry.pack(side=tk.LEFT, padx=5)
fetch_button = tk.Button(file_frame, text="Fetch Image", command=fetch_image)
fetch_button.pack(side=tk.LEFT, padx=5)

# Input for height and width
size_frame = tk.Frame(left_frame)
size_frame.pack(pady=10)
tk.Label(size_frame, text="Height:").pack(side=tk.LEFT, padx=5)
height_entry = tk.Entry(size_frame, width=5)
height_entry.pack(side=tk.LEFT, padx=5)
height_entry.insert(0, "16")
tk.Label(size_frame, text="Width:").pack(side=tk.LEFT, padx=5)
width_entry = tk.Entry(size_frame, width=5)
width_entry.pack(side=tk.LEFT, padx=5)
width_entry.insert(0, "16")

# Generate and preview buttons
button_frame = tk.Frame(left_frame)
button_frame.pack(pady=10)
generate_button = tk.Button(button_frame, text="Generate PaintingCode", command=generate_code)
generate_button.pack(side=tk.LEFT, padx=5)
preview_button = tk.Button(button_frame, text="Preview with PaintingCode", command=preview_generated_image)
preview_button.pack(side=tk.LEFT, padx=5)

# Output area
output_frame = tk.Frame(left_frame)
output_frame.pack(pady=10)
output_label = tk.Label(output_frame, text="Generated PaintingCode:")
output_label.pack()
output_text = tk.Text(output_frame, wrap=tk.WORD, width=50, height=10)
output_text.pack()

# Original image preview on the right
original_img_label = tk.Label(right_frame, text="Uploaded Image", relief=tk.RAISED)
original_img_label.pack(pady=10)

# Generated image preview below the original image
preview_img_label = tk.Label(right_frame, text="Generated Preview", relief=tk.RAISED)
preview_img_label.pack(pady=10)

# Message label for previews or status
status_label = tk.Label(app, text="", fg="green")
status_label.pack(pady=5)

# Run the application
app.mainloop()
