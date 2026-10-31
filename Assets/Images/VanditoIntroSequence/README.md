# El Vandito introduction layers

Four 1672 x 941 PNG layers reconstructed from El Vandito Boss Introduction.png with the built-in imagegen tool. The background is fully opaque; the yellow insert, van and title have true alpha transparency. These are reconstructed layers rather than a pixel-identical split of the flattened source.

Stack back to front: 01 Dark Background, 02 Yellow Strip, 03 Van Model, 04 Title. Use full-canvas image rects beneath one aspect-fitted 1672 x 941 parent, with centred pivots; do not crop individual images to their visible content.

Resting transforms in Unity UI reference pixels:

| Layer | Local scale | Anchored position |
| --- | --- | --- |
| Dark background | 1 | (0, 0) |
| Yellow strip | 1 | (0, 0) |
| Van model | 0.9 | (70, -10) |
| Title | 0.72 | (-222, 114) |

Scale positions with the shared parent. These transforms restore the source poster's overall placement after extraction. The yellow strip includes its distressed black perimeter and reconstructed scenery/road behind the removed van/title. The dark background is continuous across the entire screen so it can enter first.

Import settings: mipmaps off, NPOT scaling off, Clamp, max size 2048, input alpha with Alpha Is Transparency enabled. Android uses ASTC 4x4 at high quality for the cutout edges.

Assembly preview: Temp/VanditoIntroSequence/AssemblyPreview.png. Generation prompts: Temp/VanditoIntroSequence/Prompts.txt. The live boss introduction uses these layers through Assets/Resources/Bosses/ElVanditoIntroduction.asset; tune entry order, side, delays, hold, exit and layout there.
