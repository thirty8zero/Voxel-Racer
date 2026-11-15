# Mission briefing artwork

`Day.png` and `Night.png` are standalone, opaque landscape backgrounds. They contain no UI or enemy models. Swap the references in `Assets/Resources/MissionBriefingSettings.asset`, or assign a track's **Mission Briefing > Briefing Background Override**. With no override, the track's existing Night Time setting selects the default.

`Portraits/` contains the illustrated card artwork. The Radar Interceptor shares the Black Interceptor body image and overlays a transparent 72-frame atlas baked from the gameplay dish and its pedestal. The pedestal is aligned to the roof centre using the atlas contact point at 50% across and 12.5% up. Placement is normalized to the body image from the lower-left corner. The dish completes one turn in three seconds, matching the current gameplay spin speed. Both layers use the same card mask.

`Models/` contains the baked three-quarter views used when a portrait is missing. The screen uses native UI and cached textures.

After editing an enemy model or adding a new enemy to a mission, run **Tools > Voxel Racer > Mission Briefing > Rebuild Model Previews**. The builder preserves the backgrounds and assigned portraits and refreshes the fallback textures and radar atlas. Use **Rebuild Radar Animation** to refresh the radar card alone. **Select Settings**, **Validate And Render All Levels**, and **Validate Transitions In Play Mode** are in the same menu. Start the Play Mode check from MainMenu in Edit Mode. Reports and screen captures go to `Temp/MissionBriefing/`.
