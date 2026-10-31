import os
import sys
from PIL import Image, ImageFilter
import numpy as np
from collections import deque

def process_logo(input_path, output_png, output_ico):
    print(f"Loading {input_path}...")
    img = Image.open(input_path).convert('RGB')
    w, h = img.size
    arr = np.array(img).astype(float)

    # 1. Background color detection from the 4 corners
    corners = [arr[0, 0], arr[0, -1], arr[-1, 0], arr[-1, -1]]
    bg_color = np.mean(corners, axis=0)
    print(f"Detected background color: {bg_color}")

    # Distance map from background color
    dist = np.sqrt(np.sum((arr - bg_color) ** 2, axis=2))

    # 2. Flood fill from image borders to only select exterior background
    # Threshold for background:
    bg_threshold = 28.0  # Pixels within this distance from bg_color are candidates
    is_candidate = (dist < bg_threshold)

    visited = np.zeros((h, w), dtype=bool)
    queue = deque()

    # Seed all border pixels that match candidate background
    for x in range(w):
        if is_candidate[0, x]:
            queue.append((0, x))
            visited[0, x] = True
        if is_candidate[h - 1, x]:
            queue.append((h - 1, x))
            visited[h - 1, x] = True

    for y in range(h):
        if is_candidate[y, 0] and not visited[y, 0]:
            queue.append((y, 0))
            visited[y, 0] = True
        if is_candidate[y, w - 1] and not visited[y, w - 1]:
            queue.append((y, w - 1))
            visited[y, w - 1] = True

    # BFS Flood fill
    while queue:
        cy, cx = queue.popleft()
        for dy, dx in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
            ny, nx = cy + dy, cx + dx
            if 0 <= ny < h and 0 <= nx < w:
                if not visited[ny, nx] and is_candidate[ny, nx]:
                    visited[ny, nx] = True
                    queue.append((ny, nx))

    # 3. Create smooth alpha channel
    # Outer background (visited == True) has alpha = 0
    alpha = np.ones((h, w), dtype=np.uint8) * 255
    alpha[visited] = 0

    # Smooth the alpha mask edge using Pillow Gaussian blur for anti-aliasing
    alpha_img = Image.fromarray(alpha, mode='L')
    
    # Bounding box of the logo (non-transparent area)
    bbox = alpha_img.getbbox()
    print(f"Logo bounding box: {bbox}")

    # Crop to bounding box with a 5% margin
    if bbox:
        bx1, by1, bx2, by2 = bbox
        bw = bx2 - bx1
        bh = by2 - by1
        pad = int(max(bw, bh) * 0.06)
        
        # Make square
        max_dim = max(bw, bh) + pad * 2
        cx, cy = (bx1 + bx2) // 2, (by1 + by2) // 2
        
        crop_x1 = max(0, cx - max_dim // 2)
        crop_y1 = max(0, cy - max_dim // 2)
        crop_x2 = min(w, crop_x1 + max_dim)
        crop_y2 = min(h, crop_y1 + max_dim)
    else:
        crop_x1, crop_y1, crop_x2, crop_y2 = 0, 0, w, h

    # Reconstruct RGBA
    rgba_arr = np.dstack((np.array(img), alpha))
    final_img = Image.fromarray(rgba_arr, mode='RGBA')
    
    # Soft edge feathering on the alpha channel
    # Extract alpha, smooth slightly, and reapply
    a = final_img.split()[3]
    a_blurred = a.filter(ImageFilter.GaussianBlur(radius=0.7))
    final_img.putalpha(a_blurred)

    # Crop to square logo area
    cropped = final_img.crop((crop_x1, crop_y1, crop_x2, crop_y2))
    
    # Resize to master 512x512 with high quality Lanczos filter
    master_512 = cropped.resize((512, 512), Image.Resampling.LANCZOS)
    
    os.makedirs(os.path.dirname(output_png), exist_ok=True)
    master_512.save(output_png, format='PNG')
    print(f"Saved master PNG: {output_png} (512x512)")

    # Generate multi-resolution .ico
    icon_sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    master_512.save(output_ico, format='ICO', sizes=icon_sizes)
    print(f"Saved multi-resolution ICO: {output_ico} with sizes {icon_sizes}")

if __name__ == '__main__':
    in_img = sys.argv[1]
    out_png = sys.argv[2]
    out_ico = sys.argv[3]
    process_logo(in_img, out_png, out_ico)
