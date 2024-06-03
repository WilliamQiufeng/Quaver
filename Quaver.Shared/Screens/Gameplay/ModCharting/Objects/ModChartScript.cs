using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoonSharp.Interpreter;
using Quaver.API.Enums;
using Quaver.API.Maps;
using Quaver.API.Maps.Structures;
using Quaver.Shared.Assets;
using Quaver.Shared.Config;
using Quaver.Shared.Screens.Gameplay.ModCharting.Objects.Events;
using Quaver.Shared.Screens.Gameplay.ModCharting.Proxy;
using Quaver.Shared.Screens.Gameplay.ModCharting.Timeline;
using Quaver.Shared.Screens.Gameplay.ModCharting.Tween;
using Quaver.Shared.Screens.Gameplay.Rulesets.Keys.HitObjects;
using Quaver.Shared.Skinning;
using Wobble;
using Wobble.Graphics;
using Wobble.Graphics.Animations;
using Wobble.Graphics.Sprites;
using Wobble.Graphics.Sprites.Text;
using Wobble.Logging;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Quaver.Shared.Screens.Gameplay.ModCharting.Objects;

public class ModChartScript
{
    public Script WorkingScript { get; set; }
    protected string FilePath { get; set; }
    protected bool IsResource { get; set; }
    protected string ScriptText { get; set; }

    protected ElementAccessShortcut Shortcut { get; }
    protected ModChartState State { get; set; }


    protected GameplayScreenView GameplayScreenView { get; set; }
    public ModChartTimeline Timeline { get; set; }
    public TweenSetters TweenSetters { get; set; }

    public ModChartConstants ModChartConstants { get; set; }

    public ModChartStage ModChartStage { get; set; }
    public ModChartNotes ModChartNotes { get; set; }

    public ModChartEvents ModChartEvents { get; set; }

    public ModChartStateMachines ModChartStateMachines { get; set; }

    /// <summary>
    ///     Manages continuous segments of updates from storyboard
    /// </summary>
    public SegmentManager SegmentManager { get; set; }

    /// <summary>
    ///     Manages one-shot event firing for storyboard
    /// </summary>
    public TriggerManager TriggerManager { get; set; }

    public ModChartScript(string path, GameplayScreenView screenView)
    {
        FilePath = path;

        GameplayScreenView = screenView;

        Shortcut = new ElementAccessShortcut(screenView, this);

        ModChartEvents = new ModChartEvents(Shortcut);

        TriggerManager = new TriggerManager(new List<ValueVertex<ITriggerPayload>>());
        SegmentManager = new SegmentManager(new());

        Timeline = new ModChartTimeline(Shortcut);
        SegmentManager.SetupEvents(ModChartEvents);
        TriggerManager.SetupEvents(ModChartEvents);

        TweenSetters = new TweenSetters(Shortcut);

        ModChartConstants = new ModChartConstants();

        ModChartStage = new ModChartStage(Shortcut);

        ModChartNotes = new ModChartNotes(Shortcut);


        ModChartStateMachines = new ModChartStateMachines(Shortcut);

        UserData.RegisterAssembly(Assembly.GetCallingAssembly());
        UserData.RegisterAssembly(typeof(SliderVelocityInfo).Assembly);
        UserData.RegisterExtensionType(typeof(EventHelper));
        UserData.RegisterType<Easing>();
        UserData.RegisterType<Alignment>();
        UserData.RegisterType<Judgement>();
        UserData.RegisterType<Direction>();
        UserData.RegisterType<GameMode>();
        UserData.RegisterType<ModChartEventType>();
        UserData.RegisterType<EasingDelegate>();
        UserData.RegisterType<TweenPayload<float>.SetterDelegate>();
        UserData.RegisterType<TweenPayload<Vector2>.SetterDelegate>();
        UserData.RegisterType<TweenPayload<Vector3>.SetterDelegate>();
        UserData.RegisterType<TweenPayload<Vector4>.SetterDelegate>();
        UserData.RegisterProxyType<QuaProxy, Qua>(q => new QuaProxy(q));
        UserData.RegisterProxyType<HitObjectInfoProxy, HitObjectInfo>(hitObjectInfo =>
            new HitObjectInfoProxy(hitObjectInfo));
        UserData.RegisterProxyType<TimingPointInfoProxy, TimingPointInfo>(
            tp => new TimingPointInfoProxy(tp));
        UserData.RegisterProxyType<SpriteProxy, Sprite>(s => new SpriteProxy(s));
        UserData.RegisterProxyType<AnimatableSpriteProxy, AnimatableSprite>(s => new AnimatableSpriteProxy(s));
        UserData.RegisterProxyType<SpriteTextPlusProxy, SpriteTextPlus>(t => new SpriteTextPlusProxy(t));
        UserData.RegisterProxyType<ContainerProxy, Container>(s => new ContainerProxy(s));
        UserData.RegisterProxyType<DrawableProxy, Drawable>(s => new DrawableProxy(s));
        UserData.RegisterProxyType<Texture2DProxy, Texture2D>(t => new Texture2DProxy(t));
        UserData.RegisterProxyType<GameplayHitObjectKeysProxy, GameplayHitObjectKeys>(s =>
            new GameplayHitObjectKeysProxy(s));
        UserData.RegisterProxyType<GameplayHitObjectKeysInfoProxy, GameplayHitObjectKeysInfo>(s =>
            new GameplayHitObjectKeysInfoProxy(s), friendlyName: "GameplayHitObjectKeys");


        RegisterAllVectors();
        RegisterClosures();
        LoadScript();
    }


    public void LoadScript()
    {
        State = new ModChartState();
        WorkingScript = new Script(CoreModules.Preset_HardSandbox);

        WorkingScript.Globals["Timeline"] = Timeline;
        WorkingScript.Globals["State"] = State;
        WorkingScript.Globals["Tween"] = TweenSetters;
        WorkingScript.Globals["EasingWrapper"] = new EasingWrapperFunctions();
        WorkingScript.Globals["Easing"] = typeof(Easing);
        WorkingScript.Globals["Constants"] = ModChartConstants;
        WorkingScript.Globals["Map"] = GameplayScreenView.Screen.Map;
        WorkingScript.Globals["Stage"] = ModChartStage;
        WorkingScript.Globals["Skin"] = SkinManager.Skin;
        WorkingScript.Globals["Notes"] = ModChartNotes;
        WorkingScript.Globals["SM"] = ModChartStateMachines;
        WorkingScript.Globals["Fonts"] = typeof(Fonts);
        WorkingScript.Globals["Events"] = ModChartEvents;
        WorkingScript.Globals["Alignment"] = typeof(Alignment);
        WorkingScript.Globals["Direction"] = typeof(Direction);
        WorkingScript.Globals["EventType"] = typeof(ModChartEventType);
        WorkingScript.Globals["GameMode"] = typeof(GameMode);
        WorkingScript.Globals["Judgement"] = typeof(Judgement);
#pragma warning disable CS8974 // Converting method group to non-delegate type
        WorkingScript.Globals["Segment"] = ModChartTimeline.Segment;
        WorkingScript.Globals["Trigger"] = ModChartTimeline.Trigger;
#pragma warning restore CS8974 // Converting method group to non-delegate type
        WorkingScript.Options.DebugPrint = s => Logger.Debug(s, LogType.Runtime);

        ModChartScriptHelper.TryPerform(() =>
        {
            if (IsResource)
            {
                var buffer = GameBase.Game.Resources.Get(FilePath);
                ScriptText = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
            }
            else
            {
                ScriptText = File.ReadAllText(FilePath);
            }

            // Update state at start
            Update(int.MinValue);
            WorkingScript.DoString(ScriptText, codeFriendlyName: Path.GetFileName(FilePath));
        });
    }

    public void Update(int time)
    {
        State.SongTime = time;
        State.UnixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        State.CurrentTimingPoint = GameplayScreenView.Screen.Map.GetTimingPointAt(State.SongTime);
        State.WindowSize = new Vector2(ConfigManager.WindowWidth.Value, ConfigManager.WindowHeight.Value);

        TriggerManager.Update(time);
        SegmentManager.Update(time);
        ModChartStateMachines.RootMachine.Update();
        ModChartEvents.DeferredEventQueue.Dispatch();
    }

    private void RegisterClosures()
    {
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(ITriggerPayload),
            dynVal =>
            {
                var triggerClosure = dynVal.Function;
                return new LuaCustomTriggerPayload(triggerClosure);
            }
        );
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(ISegmentPayload),
            dynVal =>
            {
                var triggerClosure = dynVal.Function;
                return new LuaCustomSegmentPayload(triggerClosure);
            }
        );
    }

    /// <summary>
    ///     Handles registering the Vector types for the script
    /// </summary>
    private void RegisterAllVectors()
    {
        // Vector 2
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Vector2),
            dynVal =>
            {
                var table = dynVal.Table;
                var x = (float)(double)table[1];
                var y = (float)(double)table[2];
                return new Vector2(x, y);
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector2>(
            (script, vector) =>
            {
                var x = DynValue.NewNumber(vector.X);
                var y = DynValue.NewNumber(vector.Y);
                var dynVal = DynValue.NewTable(script, x, y);
                return dynVal;
            }
        );

        // Scalable Vector 2
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(ScalableVector2),
            dynVal =>
            {
                var table = dynVal.Table;
                var x = (float)(double)table[1];
                var y = (float)(double)table[2];
                var sx = (float)(double)table[3];
                var sy = (float)(double)table[4];
                return new ScalableVector2(x, y, sx, sy);
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<ScalableVector2>(
            (script, vector) =>
            {
                var x = DynValue.NewNumber(vector.X.Value);
                var y = DynValue.NewNumber(vector.Y.Value);
                var sx = DynValue.NewNumber(vector.X.Scale);
                var sy = DynValue.NewNumber(vector.Y.Scale);
                var dynVal = DynValue.NewTable(script, x, y, sx, sy);
                return dynVal;
            }
        );

        // Vector3
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Vector3),
            dynVal =>
            {
                var table = dynVal.Table;
                var x = (float)((double)table[1]);
                var y = (float)((double)table[2]);
                var z = (float)((double)table[3]);
                return new Vector3(x, y, z);
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector3>(
            (script, vector) =>
            {
                var x = DynValue.NewNumber(vector.X);
                var y = DynValue.NewNumber(vector.Y);
                var z = DynValue.NewNumber(vector.Z);
                var dynVal = DynValue.NewTable(script, x, y, z);
                return dynVal;
            }
        );

        // Color
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Color),
            dynVal =>
            {
                var table = dynVal.Table;
                var r = (byte)(double)table[1];
                var g = (byte)(double)table[2];
                var b = (byte)(double)table[3];
                var a = (byte)(double)table[4];
                return new Color(r, g, b, a);
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Color>(
            (script, vector) =>
            {
                var r = DynValue.NewNumber(vector.R);
                var g = DynValue.NewNumber(vector.G);
                var b = DynValue.NewNumber(vector.B);
                var a = DynValue.NewNumber(vector.A);
                var dynVal = DynValue.NewTable(script, r, g, b, a);
                return dynVal;
            }
        );

        // Vector4
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Vector4),
            dynVal =>
            {
                var table = dynVal.Table;
                var x = (float)((double)table[1]);
                var y = (float)((double)table[2]);
                var z = (float)((double)table[3]);
                var w = (float)((double)table[4]);
                return new Vector4(x, y, z, w);
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector4>(
            (script, vector) =>
            {
                var x = DynValue.NewNumber(vector.X);
                var y = DynValue.NewNumber(vector.Y);
                var z = DynValue.NewNumber(vector.Z);
                var w = DynValue.NewNumber(vector.W);
                var dynVal = DynValue.NewTable(script, x, y, z, w);
                return dynVal;
            }
        );
    }
}