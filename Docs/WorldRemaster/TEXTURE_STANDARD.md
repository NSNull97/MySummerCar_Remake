# World Texture Standard

- Имена: `WR_<Asset>_BaseColor`, `_Normal`, `_Mask`, optional `_Height`/`_Detail`.
- Base color хранится как sRGB; normal/mask/height — linear по importer contract.
- Mask packing соответствует HDRP: R metallic, G AO, B detail mask, A smoothness.
- Texel density выбирается по экранному размеру; hero architecture обычно 512–1024 px/m только после profiling, background ниже.
- Текстуры должны тайлиться без видимых швов или использовать trim/decal breakup.
- Donor texture и AI-upscale donor texture не могут быть final production source.

05A не создаёт ложный final-texture claim. Текущий procedural basis достаточен для material/lighting/collision pilot; unique house/garage/road/vegetation authoring записано в backlog.
