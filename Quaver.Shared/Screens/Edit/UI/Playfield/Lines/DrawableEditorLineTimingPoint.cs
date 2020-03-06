using System;
using Microsoft.Xna.Framework;
using Quaver.API.Maps.Structures;
using Quaver.Shared.Helpers;

namespace Quaver.Shared.Screens.Edit.UI.Playfield.Lines
{
    public class DrawableEditorLineTimingPoint : DrawableEditorLine
    {
        private TimingPointInfo TimingPoint { get; }

        public DrawableEditorLineTimingPoint(EditorPlayfield playfield, TimingPointInfo timingPoint) : base(playfield)
        {
            TimingPoint = timingPoint;
        }

        public override Color GetColor() => ColorHelper.HexToColor("#FE5656");

        public override string GetValue() => "";

        public override int GetTime() => (int) Math.Round(TimingPoint.StartTime, MidpointRounding.AwayFromZero);
    }
}