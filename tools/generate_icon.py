#!/usr/bin/env python3
"""Generate a 256x256 app icon (ICO with embedded PNG) for R2 Explorer.

Design: orange rounded square (Cloudflare orange gradient) with a white cloud
shape and a small accent spark. Pure-python PNG encoder, no dependencies.

Usage: python3 generate_icon.py <output.ico>
"""
import struct
import sys
import zlib

SIZE = 256


def lerp(a, b, t):
    return a + (b - a) * t


def clamp(v, lo, hi):
    return max(lo, min(hi, v))


def blend(dst, src):
    """dst/src = (r,g,b,a), alpha-composite src over dst."""
    sa = src[3] / 255.0
    if sa <= 0:
        return dst
    if sa >= 1:
        return src
    out_a = src[3] + dst[3] * (1 - sa)
    if out_a <= 0:
        return (0, 0, 0, 0)
    out_r = (src[0] * sa + dst[0] * dst[3] / 255.0 * (1 - sa)) / (out_a / 255.0)
    out_g = (src[1] * sa + dst[1] * dst[3] / 255.0 * (1 - sa)) / (out_a / 255.0)
    out_b = (src[2] * sa + dst[2] * dst[3] / 255.0 * (1 - sa)) / (out_a / 255.0)
    return (int(out_r), int(out_g), int(out_b), int(out_a))


def sd_rounded_rect(px, py, cx, cy, hw, hh, r):
    qx = abs(px - cx) - (hw - r)
    qy = abs(py - cy) - (hh - r)
    ax = max(qx, 0.0)
    ay = max(qy, 0.0)
    return ((ax * ax + ay * ay) ** 0.5) + min(max(qx, qy), 0.0) - r


def sd_circle(px, py, cx, cy, r):
    return ((px - cx) ** 2 + (py - cy) ** 2) ** 0.5 - r


def make_icon():
    pixels = [(0, 0, 0, 0)] * (SIZE * SIZE)

    def put(x, y, color):
        if 0 <= x < SIZE and 0 <= y < SIZE:
            pixels[y * SIZE + x] = blend(pixels[y * SIZE + x], color)

    for y in range(SIZE):
        for x in range(SIZE):
            # Background: rounded square with vertical orange gradient
            d = sd_rounded_rect(x + 0.5, y + 0.5, 128, 128, 110, 110, 56)
            t = y / float(SIZE)
            base = (
                int(lerp(247, 234, t)),
                int(lerp(163, 106, t)),
                int(lerp(60, 22, t)),
                255,
            )
            if d <= 0:
                pixels[y * SIZE + x] = base
            else:
                # Feather the edge slightly
                alpha = clamp(1.0 - d, 0.0, 1.0)
                pixels[y * SIZE + x] = (base[0], base[1], base[2], int(base[3] * alpha))

    # White cloud made of three circles + a base rectangle
    cloud_parts = [
        lambda x, y: sd_circle(x, y, 92, 128, 40),
        lambda x, y: sd_circle(x, y, 140, 98, 48),
        lambda x, y: sd_circle(x, y, 188, 130, 38),
    ]

    def cloud_sdf(x, y):
        d = 1e9
        for f in cloud_parts:
            d = min(d, f(x, y))
        d = min(d, sd_rounded_rect(x, y, 128, 158, 78, 26, 14))
        return d

    for y in range(SIZE):
        for x in range(SIZE):
            d = cloud_sdf(x + 0.5, y + 0.5)
            if d <= 0:
                a = clamp(1.0 + d, 0.0, 1.0)
                put(x, y, (255, 255, 255, int(255 * a)))

    # Accent spark: small orange circle at cloud bottom-right
    for y in range(SIZE):
        for x in range(SIZE):
            d = sd_circle(x + 0.5, y + 0.5, 188, 172, 16)
            if d <= 0:
                a = clamp(1.0 + d, 0.0, 1.0)
                put(x, y, (255, 255, 255, int(255 * a * 0.25)))

    return pixels


def png_chunk(typ, data):
    return (
        struct.pack(">I", len(data))
        + typ
        + data
        + struct.pack(">I", zlib.crc32(typ + data) & 0xFFFFFFFF)
    )


def encode_png(pixels):
    raw = bytearray()
    for y in range(SIZE):
        raw.append(0)  # filter type 0
        for x in range(SIZE):
            raw.extend(pixels[y * SIZE + x])
    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", ihdr)
        + png_chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        + png_chunk(b"IEND", b"")
    )


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "app.ico"
    pixels = make_icon()
    png = encode_png(pixels)
    # ICO header: reserved(2) type(2)=1 count(2)=1
    header = struct.pack("<HHH", 0, 1, 1)
    # Directory entry: width(1, 0=256) height(1, 0=256) colors(1) reserved(1)
    # planes(2) bitcount(2) size(4) offset(4)
    entry = struct.pack("<BBBBHHII", 0, 0, 0, 0, 1, 32, len(png), 22)
    with open(out, "wb") as f:
        f.write(header + entry + png)
    print("wrote", out, len(header) + len(entry) + len(png), "bytes")


if __name__ == "__main__":
    main()
