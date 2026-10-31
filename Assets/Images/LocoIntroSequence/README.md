# El Bicho Loco introduction layers

Four PNG layers prepared with the built-in imagegen tool from the v3 poster: one fully opaque full-screen dark blue background, plus three transparent foreground layers. Use a 1672 x 941 reference rect. The background, blue strip and title are 1672 x 941; the generated car cutout is 1671 x 941 and stretches to the same full rect. These are reconstructed layers rather than a pixel-identical split of the flattened source.

Back-to-front order: 01 Dark Background, 02 Blue Strip, 03 Car Model, 04 Title. Keep every image on a full-size rect under one aspect-fitted parent; do not individually crop or fit the visible content.

Final positions in a 1672 x 941 Unity UI reference rect: background, strip and title at (0, 0); car at (-20, -50). Scale those offsets with the parent. This places the car beneath the title without hiding either headlight. Use these as the resting positions when animating the separate layers.

The dark background fills the entire screen with continuous opaque navy texture so it can enter first. The blue strip owns the distressed black edging around its diagonal perimeter and stays transparent outside it; its scenery/road are reconstructed behind the removed car and title. The car layer includes the headlight beams, a centred roof beacon and a stepped voxel windscreen without red stripes.

Unity import: input alpha, Alpha Is Transparency enabled, mipmaps off, NPOT scaling off, Clamp, maximum size 2048. The live boss introduction uses these layers through Assets/Resources/Bosses/ElBichoLocoIntroduction.asset; tune entry order, side, delays, hold, exit and layout there.

Assembly preview: Temp/BichoLoco/LayerAssemblyPreview.png. Generation prompts: Temp/BichoLoco/LayerPrompts.txt.
