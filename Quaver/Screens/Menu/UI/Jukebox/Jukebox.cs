﻿using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Quaver.Assets;
using Quaver.Audio;
using Quaver.Database.Maps;
using Quaver.Graphics;
using Quaver.Graphics.Notifications;
using Quaver.Helpers;
using Quaver.Screens.Gameplay.UI;
using Quaver.Skinning;
using Wobble.Audio.Tracks;
using Wobble.Graphics;
using Wobble.Graphics.BitmapFonts;
using Wobble.Graphics.Sprites;
using Wobble.Graphics.Transformations;
using Wobble.Graphics.UI;
using Wobble.Graphics.UI.Buttons;
using Wobble.Input;
using Wobble.Logging;

namespace Quaver.Screens.Menu.UI.Jukebox
{
    public class Jukebox : Sprite
    {
        /// <summary>
        ///     The background that contains the text that says "Jukebox"
        /// </summary>
        public Sprite TitleBackground { get; set; }

        /// <summary>
        ///     The text that says "Now Playing"
        /// </summary>
        public SpriteTextBitmap NowPlayingText { get; set; }

        /// <summary>
        ///     Button to select the previous track.
        /// </summary>
        public ImageButton PreviousButton { get; set; }

        /// <summary>
        ///     The button to play the track.
        /// </summary>
        public ImageButton PlayButton { get; set; }

        /// <summary>
        ///     The button to pause/unpause the track.
        /// </summary>
        public ImageButton PauseButton { get; set; }

        /// <summary>
        ///     The button to select the next track.
        /// </summary>
        public ImageButton NextButton { get; set; }

        /// <summary>
        ///     The container that holds the song title.
        /// </summary>
        public ScrollContainer SongTitleContainer { get; set; }

        /// <summary>
        ///     The text that displays the song title.
        /// </summary>
        public SpriteTextBitmap SongTitleText { get; set; }

        /// <summary>
        ///     The song time progress bar.
        /// </summary>
        public ProgressBar SongTimeProgressBar { get; set; }

        /// <summary>
        ///     Selects new random maps to play.
        /// </summary>
        public Random RNG { get; } = new Random();

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public Jukebox()
        {
            Size = new ScalableVector2(614, 40);
            Tint = Color.Black;
            Alpha = 0.55f;

            CreateTitleBackground();
            CreateNowPlayingText();
            CreateSongTitleContainer();
            CreateSongTimeProgressBar();

            // IMPORTANT! Add the contained drawable afterwards, so that it appears on top of the progress bar.
            SongTitleContainer.AddContainedDrawable(SongTitleText);

            CreateControlButtons();
            AddBorder(Color.White, 2);
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            AnimateSongTitleText();
            SetSongTimeProgressBarStatus();
            SelectNextTrackIfFinished();

            base.Update(gameTime);
        }

        /// <summary>
        ///     Selects a random map to be selected. (and for the track to play.)
        /// </summary>
        private void SelectNextTrack()
        {
            if (MapManager.Mapsets.Count == 0)
                return;

            var randomSet = RNG.Next(0, MapManager.Mapsets.Count);
            var randomMap = RNG.Next(0, MapManager.Mapsets[randomSet].Maps.Count);

            MapManager.Selected.Value = MapManager.Mapsets[randomSet].Maps[randomMap];

            try
            {
                AudioEngine.LoadCurrentTrack();
                AudioEngine.Track.Play();
            }
            catch (Exception e)
            {
                Logger.Error($"Track for map: could not be loaded.", LogType.Runtime);
            }

            // Update the song title's text with the new one.
            UpdateSongTitleText();

            // Clear current song title animations.
            SongTitleText.Transformations.Clear();

            Logger.Debug($"Selected random jukebox track: {MapManager.Selected.Value.Artist} - {MapManager.Selected.Value.Title} " +
                         $"[{MapManager.Selected.Value.DifficultyName}] ", LogType.Runtime);
        }

        /// <summary>
        ///     Creates the sprite that serves as a background to the title text.
        /// </summary>
        private void CreateTitleBackground() => TitleBackground = new Sprite
        {
            Parent = this,
            Alignment = Alignment.TopLeft,
            Size = new ScalableVector2(130, Height),
            Tint = Color.Black,
            Alpha = 0.45f
        };

        /// <summary>
        ///     Creates the text that says "Now Playing"
        /// </summary>
        private void CreateNowPlayingText()
        {
            NowPlayingText = new SpriteTextBitmap(BitmapFonts.Exo2SemiBold, "Now Playing", 24, Color.White,
                Alignment.MidLeft, int.MaxValue)
            {
                Parent = TitleBackground,
                Alignment = Alignment.MidCenter,
                SpriteBatchOptions = new SpriteBatchOptions()
                {
                    BlendState = BlendState.NonPremultiplied
                },
                X = 2
            };

            NowPlayingText.Size = new ScalableVector2(NowPlayingText.Width * 0.60f, NowPlayingText.Height * 0.60f);
        }

        /// <summary>
        ///     Creates the container that displays the song title.
        /// </summary>
        private void CreateSongTitleContainer()
        {
            SongTitleContainer = new ScrollContainer(new ScalableVector2(Width - TitleBackground.Width - 100 - Height * 0.50f - 10, Height),
                new ScalableVector2(Width - TitleBackground.Width - 100 - Height * 0.50f - 10, Height))
            {
                Parent = this,
                X = TitleBackground.Width,
                Alpha = 0
            };

            SongTitleText = new SpriteTextBitmap(BitmapFonts.Exo2Medium, " ", 24, Color.White, Alignment.MidCenter, int.MaxValue)
            {
                Y = 2,
                Alignment = Alignment.MidLeft
            };

            UpdateSongTitleText();
            SongTitleContainer.AddContainedDrawable(SongTitleText);
        }

        /// <summary>
        ///     Updates the song title text and realigns/resizes it. it.
        /// </summary>
        private void UpdateSongTitleText()
        {
            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (MapManager.Selected.Value != null)
                SongTitleText.Text = $"{MapManager.Selected.Value.Artist} - {MapManager.Selected.Value.Title}";
            else
                SongTitleText.Text = $"No tracks available to play";

            SongTitleText.Size = new ScalableVector2(SongTitleText.Width * 0.50f, SongTitleText.Height * 0.50f);
            SongTitleText.X = SongTitleText.Width + 200;
        }

        /// <summary>
        ///     Animates the song title text from right to left.
        /// </summary>
        private void AnimateSongTitleText()
        {
            // Only reset the animation if the song title doesn't have an active transformation animation.
            if (SongTitleText.Transformations.Count != 0)
                return;

            SongTitleText.X = SongTitleText.Width + 200;

            SongTitleText.Transformations.Add(new Transformation(TransformationProperty.X, Easing.Linear,
                SongTitleText.X, -SongTitleText.Width, 6000));
        }

        /// <summary>
        ///     Creates the jukebox progress bar.
        /// </summary>
        private void CreateSongTimeProgressBar()
        {
            SongTimeProgressBar = new ProgressBar(new Vector2(SongTitleContainer.Width, SongTitleContainer.Height - 4), 0,
                AudioEngine.Track != null ? AudioEngine.Track.Length : int.MaxValue, 0, Color.Transparent, Colors.MainAccent)
            {
                Alignment = Alignment.MidLeft,
                ActiveBar =
                {
                    Alpha = 0.1f
                }
            };

            // Create the invisible bar to seek through the audio.
            var seekBar = new ImageButton(UserInterface.BlankBox, (o, e) =>
            {
                if (AudioEngine.Track == null || AudioEngine.Track.IsDisposed)
                    return;

                // The percentage of how far the mouse is inside of the progress bar.
                var percentage = (MouseManager.CurrentState.X - SongTimeProgressBar.AbsolutePosition.X) / SongTimeProgressBar.AbsoluteSize.X;

                Logger.Debug($"Jukebox track seeked to: {(int)(percentage * AudioEngine.Track.Length)}ms ({(int)(percentage * 100)}%)", LogType.Runtime);
                AudioEngine.Track.Seek(percentage * AudioEngine.Track.Length);
            })
            {
                Parent = SongTimeProgressBar,
                Size = SongTimeProgressBar.Size,
                Position = SongTimeProgressBar.Position,
                Alignment = SongTimeProgressBar.Alignment,
                Alpha = 0
            };

            SongTitleContainer.AddContainedDrawable(SongTimeProgressBar);
        }

         /// <summary>
        ///     Creates the jukebox buttons
        /// </summary>
        private void CreateControlButtons()
        {
            CreateNextSongButton();
            CreatePauseButton();
            CreatePlayButton();
            CreatePreviousSongButton();
        }

        /// <summary>
        ///     Creates the button to allow the user to choose the next song.
        /// </summary>
        private void CreateNextSongButton()
        {
            NextButton = new ImageButton(FontAwesome.StepForward)
            {
                Parent = this,
                Alignment = Alignment.MidRight,
                Size = new ScalableVector2(Height * 0.50f, Height * 0.50f),
                X = -10
            };

            NextButton.Clicked += (o, e) =>
            {
                SkinManager.Skin.SoundClick.CreateChannel().Play();

                SelectNextTrack();
                NotificationManager.Show(NotificationLevel.Info, "Selecting next track");
            };
        }

        /// <summary>
        ///     Creates the pause control button.
        /// </summary>
        private void CreatePauseButton()
        {
            PauseButton = new ImageButton(FontAwesome.Pause)
            {
                Parent = this,
                Alignment = Alignment.MidRight,
                Size = new ScalableVector2(Height * 0.50f, Height * 0.50f),
                X = NextButton.X - NextButton.Width - 10
            };

            PauseButton.Clicked += (o, e) =>
            {
                SkinManager.Skin.SoundClick.CreateChannel().Play();

                if (AudioEngine.Track == null || AudioEngine.Track.IsDisposed)
                    return;

                string action;

                if (AudioEngine.Track.IsStopped || AudioEngine.Track.IsPaused)
                {
                    AudioEngine.Track.Play();
                    action = "Resumed Track";
                }
                else
                {
                    AudioEngine.Track.Pause();
                    action = "Paused Track";
                }

                NotificationManager.Show(NotificationLevel.Info, action);
            };
        }

        /// <summary>
        ///     Creates the play control button.
        /// </summary>
        private void CreatePlayButton()
        {
            PlayButton = new ImageButton(FontAwesome.Play)
            {
                Parent = this,
                Alignment = Alignment.MidRight,
                Size = new ScalableVector2(Height * 0.50f, Height * 0.50f),
                X = PauseButton.X - PauseButton.Width - 10
            };

            PlayButton.Clicked += (o, e) =>
            {
                SkinManager.Skin.SoundClick.CreateChannel().Play();

                try
                {
                    AudioEngine.LoadCurrentTrack();
                    AudioEngine.Track?.Play();
                }
                catch (Exception)
                {
                    Logger.Error($"Failed to load track", LogType.Runtime);
                }

                NotificationManager.Show(NotificationLevel.Info, "Play");
            };
        }

        /// <summary>
        ///     Creates the previous song control button.
        /// </summary>
        private void CreatePreviousSongButton()
        {
            PreviousButton = new ImageButton(FontAwesome.StepBackward)
            {
                Parent = this,
                Alignment = Alignment.MidRight,
                Size = new ScalableVector2(Height * 0.50f, Height * 0.50f),
                X = PlayButton.X - PlayButton.Width - 10
            };

            PreviousButton.Clicked += (o, e) =>
            {
                SkinManager.Skin.SoundClick.CreateChannel().Play();
                NotificationManager.Show(NotificationLevel.Warning, "Not implemented yet");
            };
        }

        /// <summary>
        ///     Makes sure the song time progress bar is always up to date with the current time.
        /// </summary>
        private void SetSongTimeProgressBarStatus()
        {
            // Set progress bar time.
            try
            {
                if (AudioEngine.Track == null)
                    return;

                SongTimeProgressBar.Bindable.MaxValue = AudioEngine.Track.Length;
                SongTimeProgressBar.Bindable.MinValue = 0;
                SongTimeProgressBar.Bindable.Value = AudioEngine.Track.Position;
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        /// <summary>
        ///     Selects tracks continously whenever the current one is complete.
        ///
        ///     Called in update.
        /// </summary>
        private void SelectNextTrackIfFinished()
        {
            // Start selecting random tracks.
            if (MapManager.Mapsets.Count != 0 && AudioEngine.Track == null || AudioEngine.Track.HasPlayed && AudioEngine.Track.IsStopped)
                SelectNextTrack();
        }
    }
}