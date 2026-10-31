import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

def create_banner():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    icon_path = os.path.join(root, "docs", "assets", "app_icon.png")
    out_path = os.path.join(root, "docs", "assets", "banner.png")

    W, H = 1200, 630
    banner = Image.new("RGBA", (W, H), (11, 15, 25, 255))
    draw = ImageDraw.Draw(banner)

    # 1. Subtle Radial Ambient Lighting
    light = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ldraw = ImageDraw.Draw(light)
    ldraw.ellipse([50, -100, 650, 500], fill=(0, 162, 255, 38))
    ldraw.ellipse([700, 200, 1300, 750], fill=(48, 209, 88, 30))
    light = light.filter(ImageFilter.GaussianBlur(radius=80))
    banner = Image.alpha_composite(banner, light)
    draw = ImageDraw.Draw(banner)

    # 2. Grid Pattern Overlay
    grid = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(grid)
    for x in range(0, W, 40):
        gdraw.line([(x, 0), (x, H)], fill=(255, 255, 255, 6))
    for y in range(0, H, 40):
        gdraw.line([(0, y), (W, y)], fill=(255, 255, 255, 6))
    banner = Image.alpha_composite(banner, grid)
    draw = ImageDraw.Draw(banner)

    # 3. Load App Icon & Paste with Shadow
    if os.path.exists(icon_path):
        icon = Image.open(icon_path).convert("RGBA").resize((320, 320), Image.Resampling.LANCZOS)
        
        # Icon shadow
        ishadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        isdraw = ImageDraw.Draw(ishadow)
        isdraw.rounded_rectangle([95, 165, 95 + 320, 165 + 320], radius=65, fill=(0, 0, 0, 180))
        ishadow = ishadow.filter(ImageFilter.GaussianBlur(radius=30))
        banner = Image.alpha_composite(banner, ishadow)
        
        banner.paste(icon, (90, 155), icon)
        draw = ImageDraw.Draw(banner)

    # 4. Text Typography
    # Try system fonts or default
    try:
        font_title = ImageFont.truetype("segoeui.ttf", 68)
        font_sub = ImageFont.truetype("segoeui.ttf", 26)
        font_badge = ImageFont.truetype("segoeuib.ttf", 16)
        font_pill = ImageFont.truetype("segoeuib.ttf", 15)
    except:
        font_title = ImageFont.load_default()
        font_sub = ImageFont.load_default()
        font_badge = ImageFont.load_default()
        font_pill = ImageFont.load_default()

    # Title & Subtitle
    tx = 470
    ty = 160
    
    # Version Tag
    draw.rounded_rectangle([tx, ty - 10, tx + 140, ty + 24], radius=6, fill=(32, 40, 60, 240), outline=(76, 194, 255, 120), width=1)
    draw.text((tx + 12, ty - 6), "VERSION 1.1.0", font=font_badge, fill=(76, 194, 255, 255))

    draw.text((tx, ty + 38), "OpenWindow", font=font_title, fill=(255, 255, 255, 255))
    draw.text((tx, ty + 125), "The Ultimate Windows Multitasking Suite", font=font_sub, fill=(180, 195, 220, 255))

    # Feature Pills
    pills = [
        ("📌 Always-on-Top", (255, 59, 48)),
        ("🖼️ 60fps Live PiP", (255, 214, 10)),
        ("🪟 Smart Opacity", (76, 194, 255)),
        ("🖱️ Click-Through", (48, 209, 88)),
        ("🌙 GPU Dark Mode", (175, 82, 222)),
        ("🛡️ Shell Immunity", (52, 199, 89))
    ]

    px = tx
    py = ty + 190
    for text, color in pills[:3]:
        bbox = draw.textbbox((px, py), text, font=font_pill)
        w = bbox[2] - bbox[0] + 28
        draw.rounded_rectangle([px, py, px + w, py + 36], radius=18, fill=(22, 28, 45, 220), outline=color + (140,), width=1)
        draw.text((px + 14, py + 8), text, font=font_pill, fill=(240, 245, 255, 255))
        px += w + 14

    px = tx
    py += 52
    for text, color in pills[3:]:
        bbox = draw.textbbox((px, py), text, font=font_pill)
        w = bbox[2] - bbox[0] + 28
        draw.rounded_rectangle([px, py, px + w, py + 36], radius=18, fill=(22, 28, 45, 220), outline=color + (140,), width=1)
        draw.text((px + 14, py + 8), text, font=font_pill, fill=(240, 245, 255, 255))
        px += w + 14

    # Bottom platform badge
    by = H - 65
    draw.text((tx, by), "Standalone Desktop Native App • Windows 11 & 10 (x64 / x86) • Zero .NET Required", font=font_badge, fill=(110, 125, 150, 255))

    banner.save(out_path, "PNG")
    print(f"Generated banner: {out_path}")

if __name__ == "__main__":
    create_banner()
