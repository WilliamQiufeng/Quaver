using System.Net;
using Microsoft.Xna.Framework;
using Quaver.Server.Client.Events.Download;
using Quaver.Server.Client.Helpers;
using Quaver.Shared.Assets;
using Quaver.Shared.Graphics.Containers;
using Quaver.Shared.Screens.Download;
using Wobble;
using Wobble.Bindables;
using Wobble.Graphics;
using Wobble.Graphics.Sprites;
using Wobble.Graphics.Sprites.Text;
using Wobble.Graphics.UI;
using Wobble.Graphics.UI.Buttons;
using Wobble.Managers;

namespace Quaver.Shared.Graphics.Overlays.Hub.Downloads.Scrolling
{
    public sealed class DrawableDownload : PoolableSprite<MapsetDownload>
    {
        /// <summary>
        /// </summary>
        public override int HEIGHT { get; } = 115;

        /// <summary>
        /// </summary>
        private Sprite ContentContainer { get; set; }

        /// <summary>
        /// </summary>
        private ImageButton Button { get; set; }
        
        private ImageButton PauseButton { get; set; }
        
        private ImageButton RemoveButton { get; set; }
        private ImageButton RetryButton { get; set; }

        /// <summary>
        /// </summary>
        private SpriteTextPlus Name { get; set; }

        /// <summary>
        /// </summary>
        private ProgressBar ProgressBar { get; set; }

        /// <summary>
        /// </summary>
        private SpriteTextPlus ProgressPercentage { get; set; }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="container"></param>
        /// <param name="item"></param>
        /// <param name="index"></param>
        public DrawableDownload(PoolableScrollContainer<MapsetDownload> container, MapsetDownload item, int index)
            : base(container, item, index)
        {
            Size = new ScalableVector2(container.Width, HEIGHT);
            Alpha = 0;

            CreateContentContainer();
            CreateButton();
            CreateSongName();
            CreateProgressBar();
            CreateProgressPercentage();

            UpdateText();

            Item.Progress.ValueChanged += OnDownloadProgressChanged;
            Item.FileDownloader.ValueChanged += (sender, args) =>
            {
                if (args.Value == null) return;
                args.Value.StatusUpdated += OnDownloadStatusUpdated;
            };
            Item.FileDownloader.TriggerChange();
        }

        private void OnDownloadStatusUpdated(object sender, DownloadStatusChangedEventArgs e)
        {
            if (e.Status is FileDownloaderStatus.Downloading or FileDownloaderStatus.Initialized
                or FileDownloaderStatus.Connecting)
            {
                PauseButton.Image = UserInterface.HubDownloadPause;
            }
            else
            {
                PauseButton.Image = UserInterface.HubDownloadResume;
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            Button.Alpha = Button.IsHovered ? 0.35f : 0;
            RetryButton.Alpha = RetryButton.IsHovered ? 0.75f : 1;
            RemoveButton.Alpha = RemoveButton.IsHovered ? 0.75f : 1;
            PauseButton.Alpha = PauseButton.IsHovered ? 0.75f : 1;

            var game = (QuaverGame) GameBase.Game;

            if (Container != null)
                Button.IsClickable = game.OnlineHub.SelectedSection == game.OnlineHub.Sections[OnlineHubSectionType.ActiveDownloads];

            base.Update(gameTime);
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public override void Destroy()
        {
            // ReSharper disable twice DelegateSubtraction
            Item.Progress.ValueChanged -= OnDownloadProgressChanged;

            base.Destroy();
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="item"></param>
        /// <param name="index"></param>
        public override void UpdateContent(MapsetDownload item, int index)
        {
            Item = item;
            Index = index;

            ScheduleUpdate(UpdateText);
        }

        /// <summary>
        /// </summary>
        private void CreateContentContainer()
        {
            ContentContainer = new Sprite()
            {
                Parent = this,
                Alignment = Alignment.MidCenter,
                Size = new ScalableVector2(Width * 0.94f, HEIGHT * 0.85f),
                Image = UserInterface.HubDownloadContainer,
                UsePreviousSpriteBatchOptions = true
            };
        }

        /// <summary>
        /// </summary>
        private void CreateButton()
        {
            Button = new ImageButton(UserInterface.BlankBox)
            {
                Parent = ContentContainer,
                Alignment = Alignment.MidCenter,
                Alpha = 0,
                Size = new ScalableVector2(ContentContainer.Width - 2, ContentContainer.Height - 2)
            };
            PauseButton = new ImageButton(UserInterface.HubDownloadPause)
            {
                Parent = ContentContainer,
                Alignment = Alignment.BotRight,
                Position = new ScalableVector2(-60, 0),
                Size = new ScalableVector2(30, 30)
            };
            PauseButton.Clicked += (sender, args) =>
            {
                if (Item.FileDownloader.Value?.Status is FileDownloaderStatus.Connecting or FileDownloaderStatus.Downloading)
                {
                    Item.FileDownloader.Value?.Pause();
                }
                else
                {
                    Item.FileDownloader.Value?.StartOrResume();
                }
            };
            RetryButton = new ImageButton(UserInterface.HubDownloadRetry)
            {
                Parent = ContentContainer,
                Alignment = Alignment.BotRight,
                Position = new ScalableVector2(-30, 0),
                Size = new ScalableVector2(30, 30)
            };
            RetryButton.Clicked += (sender, args) =>
            {
                Item.FileDownloader.Value?.Cancel();
                Item.FileDownloader.Value?.StartOrResume();
            };
            RemoveButton = new ImageButton(UserInterface.HubDownloadRemove)
            {
                Parent = ContentContainer,
                Alignment = Alignment.BotRight,
                Position = new ScalableVector2(0, 0),
                Size = new ScalableVector2(30, 30)
            };
            RemoveButton.Clicked += (sender, args) =>
            {
                Item.RemoveDownload();
            };
        }

        /// <summary>
        /// </summary>
        private void CreateSongName()
        {
            Name = new SpriteTextPlus(FontManager.GetWobbleFont(Fonts.LatoBlack), "Artist - Title", 22)
            {
                Parent = ContentContainer,
                Alignment = Alignment.TopLeft,
                X = 18,
                Y = 14,
                UsePreviousSpriteBatchOptions = true
            };
        }

        /// <summary>
        /// </summary>
        private void CreateProgressBar()
        {
            ProgressBar = new ProgressBar(new Vector2(Width - 110, 14), 0, 100, 0, Color.Transparent,
                Colors.MainAccent)
            {
                Parent = ContentContainer,
                Alignment = Alignment.TopLeft,
                X = Name.X,
                Y = Name.RelativeRectangle.Bottom + 5,
                UsePreviousSpriteBatchOptions = true,
                ActiveBar =
                {
                    UsePreviousSpriteBatchOptions = true
                },
            };

            ProgressBar.AddBorder(Colors.MainBlue, 2);
        }

        /// <summary>
        /// </summary>
        private void CreateProgressPercentage()
        {
            ProgressPercentage = new SpriteTextPlus(FontManager.GetWobbleFont(Fonts.LatoBlack), "0%", 20)
            {
                Parent = ContentContainer,
                Alignment = Alignment.TopLeft,
                Y = ProgressBar.Y + 1,
                X = ProgressBar.X + ProgressBar.Width + 14,
                UsePreviousSpriteBatchOptions = true
            };
        }

        /// <summary>
        /// </summary>
        private void UpdateText()
        {
            Name.Text = string.IsNullOrEmpty(Item.Title) ? Item.Artist : $"{Item.Artist} - {Item.Title}";
            Name.TruncateWithEllipsis((int) ProgressBar.Width - 10);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDownloadProgressChanged(object sender, BindableValueChangedEventArgs<DownloadProgressEventArgs> e)
        {
            ScheduleUpdate(() =>
            {
                var percent = e.Value.ProgressPercentage;

                if (e.Value.BytesReceived == 0)
                    ProgressBar.Bindable.Value = 0;

                ProgressBar.Bindable.Value = percent;
                ProgressPercentage.Text = $"{percent}%";
            });
        }
    }
}