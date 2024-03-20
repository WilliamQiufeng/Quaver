using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using Quaver.Shared.Screens.Gameplay.Rulesets.Keys.Playfield;
using Quaver.Shared.Screens.Gameplay.Rulesets.Keys.Storyboard.Scripting;
using Wobble.Graphics;
using Wobble.Graphics.Sprites;

namespace Quaver.Shared.Screens.Gameplay.Rulesets.Keys.Storyboard.Tween;

[MoonSharpUserData]
public class TweenSetters
{
    [MoonSharpVisible(false)] public GameplayScreenView GameplayScreenView { get; set; }

    [MoonSharpVisible(false)] public StoryboardScript Script { get; set; }

    [MoonSharpVisible(false)] public GameplayScreen GameplayScreen => GameplayScreenView.Screen;

    [MoonSharpVisible(false)]
    public GameplayPlayfieldKeys GameplayPlayfieldKeys => (GameplayPlayfieldKeys)GameplayScreen.Ruleset.Playfield;

    [MoonSharpVisible(false)]
    public GameplayPlayfieldKeysStage GameplayPlayfieldKeysStage => GameplayPlayfieldKeys.Stage;


    public TweenPayload.SetterDelegate SpriteX(Sprite sprite)
    {
        return v => sprite.X = v;
    }
    public TweenPayload.SetterDelegate SpriteY(Sprite sprite)
    {
        return v => sprite.Y = v;
    }
    public TweenPayload.SetterDelegate SpriteRotation(Sprite sprite)
    {
        return v => sprite.Rotation = v;
    }
    
    public TweenPayload.SetterDelegate SpriteAlpha(Sprite sprite)
    {
        return v => sprite.Alpha = v;
    }
    
    public TweenPayload.SetterDelegate SpriteSizeX(Sprite sprite)
    {
        return v => sprite.Size = new ScalableVector2(v, sprite.Size.Y.Value);
    }
    public TweenPayload.SetterDelegate SpriteSizeY(Sprite sprite)
    {
        return v => sprite.Size = new ScalableVector2(sprite.Size.X.Value, v);
    }
    
    public TweenPayload.SetterDelegate ReceptorX(int lane)
    {
        return v => GameplayPlayfieldKeysStage.Receptors[lane - 1].X = v;
    }
    public TweenPayload.SetterDelegate ReceptorY(int lane)
    {
        return v => GameplayPlayfieldKeysStage.Receptors[lane - 1].Y = v;
    }
    
}