# Catalog Texture Upscale Inputs

The canonical read-only source directory is:

```text
E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\raw\world\milestone-04a1\assetripper-unity-project\ExportedProject\Assets\Texture2D
```

Do not overwrite these files. Put reviewed upscale candidates under:

```text
E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\upscaled\catalogs
```

Preserve the source filenames so the replacement review remains mechanical.

The currently generated runtime copies can be inspected here:

```text
E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\LegacyImport\RuntimeBaseline\Generated\Items\Source\Texture2D
```

Do not edit generated copies directly: the item-presentation rebuild replaces
them from the reviewed staging input.

## Home parts catalog

```text
kansi.png
page_audio.png
page_body.png
page_order.png
page_performance.png
page_performance2.png
page_poulstery.png
page_racing 1.png
page_racing 2.png
page_wheels.png
```

Useful related small graphics:

```text
magazine_template.png
postorder.png
amistech_logo.png
x.png
```

`x.png` is the original transparent blue selection mark. Keep its alpha and
canvas untouched; unlike the catalog pages, it should normally remain at its
native resolution to avoid a blurred or oversized mark.

The handwritten order rows use the donor bitmap-font closure rather than an
ordinary UI font:

```text
Font/RAGE.asset
Material/Font Material_0.mat
Texture2D/Font Texture_0.texture2D
```

These three files are hash-gated dependencies of one font and must be reviewed
or replaced together. Do not upscale `Font Texture_0.texture2D` independently:
its glyph rectangles are serialized in `RAGE.asset`.

## Fleetari brochure

```text
repairshop_01.png
repairshop_02.png
repairshop_03.png
repairshop_04.png
repairshop_05.png
repairshop_06.png
repairshop_07.png
repairshop_08.png
```

## Other currently visible small item textures

```text
prop_flashlight.png
prop_flashlight_n.png
```

Recommended output is lossless PNG at 4x resolution. Preserve alpha, do not
crop, rotate, pad, recolor or sharpen text into different glyphs. The runtime
uses normalized click regions, so the pixel canvas and composition must remain
exactly aligned with the source.
