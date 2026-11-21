using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu_database_reader.BinaryFiles;
using osu.Shared;
using Quaver.API.Enums;
using Quaver.API.Helpers;
using Quaver.Shared.Config;
using Quaver.Shared.Graphics.Notifications;
using Wobble;
using Wobble.Logging;
using GameMode = osu.Shared.GameMode;

namespace Quaver.Shared.Database.Maps.OsuStable;

public static class OsuStableDatabaseCache
{
    /// <summary>
    ///     Reads the osu!.db file defined in config and loads all of those maps into the cache.
    /// </summary>
    public static List<OtherGameMap> LoadOsuBeatmapDatabase()
    {
        try
        {
            var db = OsuDb.Read(ConfigManager.OsuDbPath.Value);

            var osuFolder = Path.GetDirectoryName(ConfigManager.OsuDbPath.Value);

            // Default songs path
            MapManager.OsuSongsFolder = osuFolder + "/Songs/";

            // Read the config file and set the appropriate path
            var configFile = $"{osuFolder}/osu!.{Environment.UserName}.cfg";

            if (File.Exists(configFile))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(configFile))
                    {
                        if (!line.StartsWith("BeatmapDirectory"))
                            continue;

                        var dir = line.Split("=")[1].Trim();

                        MapManager.OsuSongsFolder = dir == "Songs" ? $"{osuFolder}/{dir}/" : dir + "/";
                        break;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, LogType.Runtime);
                }
            }

            // Find all osu!mania maps and order them by their difficulty value.
            var osuBeatmaps = db.Beatmaps.Where(x => x.GameMode == GameMode.Mania && (1 <= x.CircleSize && x.CircleSize <= 10)).ToList();
            osuBeatmaps = osuBeatmaps.OrderBy(x => x.DiffStarRatingMania.ContainsKey(Mods.None) ? x.DiffStarRatingMania[Mods.None] : 0).ToList();

            var osuToQuaverMaps = new List<OtherGameMap>();

            foreach (var map in osuBeatmaps)
            {
                var newMap = new OtherGameMap()
                {
                    Md5Checksum = map.BeatmapChecksum,
                    Directory = map.FolderName.Trim(),
                    Path = map.BeatmapFileName,
                    Artist = map.Artist,
                    Title = map.Title,
                    MapSetId = -1,
                    MapId = -1,
                    DifficultyName = map.Version,
                    RankedStatus = RankedStatus.NotSubmitted,
                    Creator = map.Creator,
                    AudioPath = map.AudioFileName,
                    AudioPreviewTime = map.AudioPreviewTime,
                    Description = $"",
                    Source = map.SongSource,
                    Tags = map.SongTags,
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    Mode = ModeHelper.FromKeyCount((int)map.CircleSize),
                    SongLength = map.TotalTime,
                    Game = MapGame.Osu,
                    OriginalGame = OtherGameMapDatabaseGame.Osu,
                    BackgroundPath = "",
                    RegularNoteCount = map.CountHitCircles,
                    LongNoteCount = map.CountSliders,
                    LocalOffset = map.OffsetLocal,
                    OnlineOffset = map.OffsetOnline,
                    HasScratchKey = map.CircleSize == 8
                };

                // Get the BPM of the osu! maps
                if (map.TimingPoints != null)
                {
                    try
                    {
                        newMap.Bpm = Math.Round(60000 / map.TimingPoints.Find(x => x.MsPerQuarter > 0).MsPerQuarter, 0);
                    }
                    catch (Exception e)
                    {
                        newMap.Bpm = 0;
                    }
                }

                osuToQuaverMaps.Add(newMap);
            }

            return osuToQuaverMaps;
        }
        catch (Exception e)
        {
            Logger.Error(e, LogType.Runtime);

            var game = GameBase.Game as QuaverGame;
            var screen = game?.CurrentScreen;

            if (screen != null)
                NotificationManager.Show(NotificationLevel.Error, "Failed to load maps from other games. Is your db path correct in config?");

            return new List<OtherGameMap>();
        }
    }
}