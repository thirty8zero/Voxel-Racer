# Mission briefing artwork

`Day.png` and `Night.png` are standalone, opaque landscape backgrounds. They contain no UI or enemy models. Swap the references in `Assets/Resources/MissionBriefingSettings.asset`, or assign a track's **Mission Briefing > Briefing Background Override**. With no override, the track's existing Night Time setting selects the default.

`Models/` contains three-quarter renders of the actual enemy, radar, turret and boss prefabs. The screen uses native text, panels and buttons with these cached images; it does not instantiate enemy gameplay or additional rendering cameras.

After editing an enemy model or adding a new enemy to a mission, run **Tools > Voxel Racer > Mission Briefing > Rebuild Model Previews**. It preserves the selected backgrounds and existing source prefabs/tuning. Use **Select Settings**, **Validate And Render All Levels**, and **Validate Transitions In Play Mode** from the same menu. Start the Play Mode check from MainMenu in Edit Mode. Reports and screen captures go to `Temp/MissionBriefing/`.
