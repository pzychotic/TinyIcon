---
layout: default
title: TinyIcon
---

# TinyIcon

[![GitHub License](https://badgen.net/github/license/pzychotic/TinyIcon)](https://github.com/pzychotic/TinyIcon/blob/main/LICENSE)
[![GitHub Release](https://badgen.net/github/release/pzychotic/TinyIcon/stable)](https://github.com/pzychotic/TinyIcon/releases/latest)
[![CI](https://github.com/pzychotic/TinyIcon/actions/workflows/ci.yml/badge.svg)](https://github.com/pzychotic/TinyIcon/actions/workflows/ci.yml)

A tiny windows icon file (.ico) creator.

Define your wanted resolutions, import image, save. Done!

## Features

- Multi-resolution icons: pick the sub-images your `.ico` should contain from 8×8 up to 256×256
- Colour depth: 24-bit and/or 32-bit entries, selectable independently of each other
- Import a source image (`.png`, `.bmp`, `.jpg`, `.gif`, `.tiff`) that gets downscaled into every sub-image
- Non-square sources are fitted and centred with transparent padding, so the aspect ratio is kept
- Full alpha transparency for 32-bit entries; 24-bit entries use a 1-bit mask
- Automatic encoding per sub-image: PNG for 256×256 32-bit entries, classic DIB/BMP for the rest
- Detail view with zoom (Ctrl +/-/\*, or mouse wheel), pan (right mouse drag, right click recentres)
- Keyboard shortcuts for the whole workflow: Ctrl+N (new icon), Ctrl+I (import image), Ctrl+S (save icon)
- Remembers your window placement and the last resolution selection between runs

## Dependencies

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [Microsoft.Xaml.Behaviors.Wpf](https://github.com/microsoft/XamlBehaviorsWpf)

## References

- Icons created from [Fluent System Icons](https://github.com/microsoft/fluentui-system-icons)

---

[Screenshots](Screenshots) · [Changelog](Changelog) · [GitHub Repository](https://github.com/pzychotic/TinyIcon) · [Releases](https://github.com/pzychotic/TinyIcon/releases)
