using Microsoft.Xna.Framework;
using Quaver.API.Enums;
using Quaver.Shared.Helpers;
using Quaver.Shared.Localization;

namespace Quaver.Shared.Modifiers.Mods
{
    public class ModMirror : IGameplayModifier
    {
        public string Name { get; set; } = Translations.ModMirror_Name;

        public ModIdentifier ModIdentifier { get; set; } = ModIdentifier.Mirror;

        public ModType Type { get; set; } = ModType.Special;

        public string Description { get; set; } = Translations.ModMirror_Description;

        public bool Ranked() => true;

        public bool AllowedInMultiplayer { get; set; } = true;

        public bool OnlyMultiplayerHostCanCanChange { get; set; }

        public bool ChangesMapObjects { get; set; }

        public ModIdentifier[] IncompatibleMods { get; set; } = { };

        public Color ModColor { get; } = ColorHelper.HexToColor($"#5F868F");

        public void InitializeMod() { }
    }
}