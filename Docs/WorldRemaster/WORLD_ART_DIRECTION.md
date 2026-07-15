# World Art Direction

Цель — узнаваемая финская сельская среда середины 1990-х: практичные постройки, состаренные окрашенные доски, оцинкованный металл, гравий, влажная почва, хвойно-лиственный край леса и спокойное низкое солнце. Ремастер повышает правдоподобие материалов и плотность деталей, но не превращает мир в стерильную современную застройку.

## Визуальные правила

- Реальный масштаб, SI units и читаемые проёмы важнее декоративного exaggeration.
- Цвета земли и строений умеренные; акцент создают свет, погода, локальная краска и gameplay props.
- Dirt/rust/wetness должны быть mask-driven и логичными: низы стен, кромки крыши, колеи, зоны хвата.
- Bloom, chromatic aberration и gameplay depth of field не являются основой читаемости.
- Clear midday, golden evening, overcast/rain и dusk используют одни production materials, а не отдельные baked variants.

## Pilot 05A

`WR_HomeYardPilot.prefab` — production-candidate, не final art. Он фиксирует composition, metres, garage/vehicle clearance, material families, collision, LOD and streaming boundary. Hero modelling, unique labels, trim damage, believable vegetation species mix и финальная texture detail остаются в `WORLD_ART_BACKLOG.csv`.
