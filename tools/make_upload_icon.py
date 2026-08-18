#!/usr/bin/env python3
"""Convert the uploaded "上传到云端" gradient icon into a multi-size app.ico."""
from PIL import Image
import os

SRC = r"I:\Documents\inscode-desktop\general-workspace\attachments\icons8_extract\icons8-上传到云端-100.png"
OUT = r"F:\1_GitHun\workspace\R2Explorer\Resources\app.ico"

src = Image.open(SRC).convert("RGBA")
print("source size:", src.size, src.mode)

sizes = [256, 128, 64, 48, 32, 24, 16]
src.save(OUT, format="ICO", sizes=[(s, s) for s in sizes])
print("wrote", OUT, os.path.getsize(OUT), "bytes")

# verify
ico = Image.open(OUT)
print("ico size:", ico.size)
