﻿using System;
using System.Threading.Tasks;
using Quaver.API.Enums;
using Quaver.API.Maps;
using Quaver.API.Maps.Parsers;
using Quaver.Bindables;
using Quaver.Config;
using Quaver.Database.Maps;
using Quaver.Discord;
using Quaver.Graphics.UI.Notifications;
using Quaver.Helpers;
using Quaver.Logging;
using Quaver.Main;
using Quaver.Modifiers;
using Quaver.States.Edit.UI;
using Quaver.States.Edit.UI.Modes;
using Quaver.States.Edit.UI.Modes.Keys;

namespace Quaver.States.Edit
{
    internal class EditorScreen : IGameState
    {
        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public State CurrentState { get; set; } = State.Edit;

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public bool UpdateReady { get; set; }

        /// <summary>
        ///     The map that is currently being edited.
        /// </summary>
        internal Qua Map { get; }

        /// <summary>
        ///     The last saved version of the map.
        /// </summary>
        private Qua LastSavedMap { get; set; }

        /// <summary>
        ///     The entire user interface for the editor.
        /// </summary>
        private EditorInterface UI { get; }

        /// <summary>
        ///     Handles all the input for the editor.
        /// </summary>
        private EditorInputManager InputManager { get; }

        /// <summary>
        ///     The editor for the current game mode.
        /// </summary>
        internal EditorGameMode EditorGameMode { get; }

        /// <summary>
        ///     The current beat snap, it used a BindedValue<int> here
        ///     because some parts of the editor (such as the beat snap bars) will
        ///     need to know when this value changes so it can update accordingly.
        /// </summary>
        internal BindedInt CurrentBeatSnap { get; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="map"></param>
        public EditorScreen(Qua map)
        {
            ModManager.RemoveAllMods();

            if (GameBase.AudioEngine.IsPlaying)
                GameBase.AudioEngine.Pause();

            // Grab the map and clone it so that we can save the "last saved" one.
            Map = map;
            LastSavedMap = ObjectHelper.DeepClone(Map);

            // Initialize the UI and input manager.
            UI = new EditorInterface(this);
            InputManager = new EditorInputManager(this);

            // Set the current beat snap and default it to 1/4th.
            CurrentBeatSnap = new BindedInt("BeatSnap", 4, 1, 48);

            // Select the editor's game mode based on the map's mode.
            switch (Map.Mode)
            {
                case GameMode.Keys4:
                case GameMode.Keys7:
                    EditorGameMode = new EditorGameModeKeys(this, Map.Mode);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ChangeDiscordPresence();
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public void Initialize()
        {
            EditorGameMode.Initialize(this);
            UI.Initialize(this);
            UpdateReady = true;
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public void UnloadContent()
        {
            EditorGameMode.UnloadContent();
            UI.UnloadContent();
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="dt"></param>
        public void Update(double dt)
        {
            EditorGameMode.Update(dt);
            InputManager.HandleInput(dt);
            UI.Update(dt);
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public void Draw()
        {
            UI.Draw();
        }

        /// <summary>
        ///     Saves the map.
        /// </summary>
        internal void SaveMap()
        {
            var map = GameBase.SelectedMap;
            Map.Save($"{ConfigManager.SongDirectory}/{map.Directory}/{map.Path}");

            LastSavedMap = ObjectHelper.DeepClone(Map);

            MapCache.LoadAndSetMapsets();
            Logger.LogSuccess($"Map has been saved!", LogType.Runtime);
        }

        /// <summary>
        ///     Changes the discord presence to state that we're editing a map.
        /// </summary>
        private void ChangeDiscordPresence()
        {
            DiscordManager.Client.CurrentPresence.Details = Map.ToString();
            DiscordManager.Client.CurrentPresence.State = "Editing Map";
            DiscordManager.Client.SetPresence(DiscordManager.Client.CurrentPresence);
        }

        /// <summary>
        ///     Goes to the editor screen.
        /// </summary>
        internal static void Go()
        {
            var map = GameBase.SelectedMap;

            if (map == null)
            {
                NotificationManager.Show(NotificationLevel.Error, "There is currently no selected map to edit!");
                return;
            }

            Qua qua;

            switch (map.Game)
            {
                case MapGame.Quaver:
                    var path = $"{ConfigManager.SongDirectory}/{map.Directory}/{map.Path}";
                    qua = Qua.Parse(path);
                    break;
                case MapGame.Osu:
                    qua = new OsuBeatmap($"{GameBase.OsuSongsFolder}/{map.Directory}/{map.Path}").ToQua();
                    break;
                case MapGame.Etterna:
                    NotificationManager.Show(NotificationLevel.Error, "Etterna maps aren't eligible to be edited");
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (qua != null)
                GameBase.GameStateManager.ChangeState(new EditorScreen(qua));
            else
                NotificationManager.Show(NotificationLevel.Error, "An error ocurred when trying to edit this map.");
        }
    }
}