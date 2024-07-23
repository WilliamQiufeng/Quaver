using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Timers;
using MoonSharp.Interpreter;
using Quaver.API.Enums;
using Quaver.API.Maps;
using Quaver.API.Maps.Structures;
using Quaver.Shared.Assets;
using Quaver.Shared.Config;
using Quaver.Shared.Graphics.Notifications;
using Quaver.Shared.Screens.Gameplay.ModCharting.Objects.Events;
using Quaver.Shared.Screens.Gameplay.ModCharting.Proxy;
using Quaver.Shared.Screens.Gameplay.ModCharting.StateMachine;
using Quaver.Shared.Screens.Gameplay.ModCharting.Timeline;
using Quaver.Shared.Screens.Gameplay.ModCharting.Tween;
using Quaver.Shared.Screens.Gameplay.Rulesets.Keys.HitObjects;
using Quaver.Shared.Screens.Gameplay.Rulesets.Keys.Playfield;
using Quaver.Shared.Skinning;
using Wobble;
using Wobble.Graphics;
using Wobble.Graphics.Animations;
using Wobble.Graphics.Shaders;
using Wobble.Graphics.Sprites;
using Wobble.Graphics.Sprites.Text;
using Wobble.Logging;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;
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

    public ModChartNew ModChartNew { get; set; }

    public ModChartLayers ModChartLayers { get; set; }

    public ModChartAudio ModChartAudio { get; set; }

    public ModChartUtils ModChartUtils { get; set; }

    public ModChartInternal Internal { get; set; }

    /// <summary>
    ///     Manages continuous segments of updates from storyboard
    /// </summary>
    public SegmentManager SegmentManager { get; set; }

    /// <summary>
    ///     Manages one-shot event firing for storyboard
    /// </summary>
    public TriggerManager TriggerManager { get; set; }

    /// <summary>
    ///     Program stops executing
    /// </summary>
    public bool Halted { get; private set; }

    /// <summary>
    ///     The interval between updates
    /// </summary>
    private TimeSpan updateInterval = TimeSpan.FromMilliseconds(16);

    /// <summary>
    ///     Clock ticking updates
    /// </summary>
    private ContinuousClock clock;

    public ModChartScript(string path, GameplayScreenView screenView)
    {
        ModChartScriptHelper.ResetCounters();

        FilePath = path;

        GameplayScreenView = screenView;

        Shortcut = new ElementAccessShortcut(screenView, this);

        Internal = new ModChartInternal(Shortcut);

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

        ModChartNew = new ModChartNew(Shortcut);

        ModChartLayers = new ModChartLayers(Shortcut);

        ModChartAudio = new ModChartAudio(Shortcut);

        State = new ModChartState(Shortcut);

        ModChartUtils = new ModChartUtils(Shortcut);

        ModChartUtils.InitializeMeasures();

        UserData.RegisterType<ModChartVector>(new ModChartVectorDescriptor(typeof(ModChartVector),
            InteropAccessMode.Default, "Vector"));
        UserData.RegisterExtensionType(typeof(EventHelper));
        UserData.RegisterType<EasingDelegate>();
        UserData.RegisterType<LerpDelegate<float>>();
        UserData.RegisterType<LerpDelegate<Vector2>>();
        UserData.RegisterType<LerpDelegate<XnaVector2>>();
        UserData.RegisterType<LerpDelegate<Vector3>>();
        UserData.RegisterType<LerpDelegate<Vector4>>();
        UserData.RegisterProxyType<QuaProxy, Qua>(q => new QuaProxy(q));
        UserData.RegisterProxyType<HitObjectInfoProxy, HitObjectInfo>(hitObjectInfo =>
            new HitObjectInfoProxy(hitObjectInfo));
        UserData.RegisterProxyType<TimingPointInfoProxy, TimingPointInfo>(
            tp => new TimingPointInfoProxy(tp));
        UserData.RegisterProxyType<SpriteProxy, Sprite>(s => new SpriteProxy(s));
        UserData.RegisterProxyType<AnimatableSpriteProxy, AnimatableSprite>(s => new AnimatableSpriteProxy(s));
        UserData.RegisterProxyType<SpriteTextPlusProxy, SpriteTextPlus>(t => new SpriteTextPlusProxy(t));
        UserData.RegisterProxyType<ContainerProxy, Container>(s => new ContainerProxy(s));
        UserData.RegisterProxyType<LaneContainerProxy, GameplayPlayfieldLane>(s => new LaneContainerProxy(s));
        UserData.RegisterProxyType<DrawableProxy, Drawable>(s => new DrawableProxy(s));
        UserData.RegisterProxyType<ShaderProxy, Shader>(s => new ShaderProxy(s));
        UserData.RegisterProxyType<SpriteBatchOptionsProxy, SpriteBatchOptions>(s => new SpriteBatchOptionsProxy(s));
        UserData.RegisterProxyType<Texture2DProxy, Texture2D>(t => new Texture2DProxy(t));
        UserData.RegisterProxyType<LayerProxy, Layer>(s => new LayerProxy(s));
        UserData.RegisterProxyType<GameplayHitObjectKeysProxy, GameplayHitObjectKeys>(s =>
            new GameplayHitObjectKeysProxy(s));
        UserData.RegisterProxyType<GameplayHitObjectKeysInfoProxy, GameplayHitObjectKeysInfo>(s =>
            new GameplayHitObjectKeysInfoProxy(s), friendlyName: "GameplayHitObjectKeys");


        RegisterAllVectors();
        RegisterClosures();
        RegisterEasingType();
        RegisterKeyframe<float>();
        RegisterKeyframe<Vector2>();
        RegisterKeyframe<ModChartVector>();
        RegisterKeyframe<ScalableVector2>();
        RegisterKeyframe<Color>();
        RegisterKeyframe<XnaVector2>();
        RegisterKeyframe<Vector3>();
        RegisterKeyframe<Vector4>();
        RegisterLayer();
        RegisterBeat();

        UserData.RegisterAssembly(Assembly.GetCallingAssembly());
        UserData.RegisterAssembly(typeof(SliderVelocityInfo).Assembly);

        LoadScript();
    }


    public void LoadScript()
    {
        WorkingScript = new Script(CoreModules.Preset_HardSandbox);

        RegisterEnum<Easing>("Easing");
        RegisterEnum<Color>("Color");
        RegisterEnum<Alignment>("Alignment");
        RegisterEnum<Judgement>("Judgement");
        RegisterEnum<Direction>("Direction");
        RegisterEnum<GameMode>("GameMode");
        RegisterEnum<ModChartEventType>("EventType");
        WorkingScript.Globals["New"] = ModChartNew;
        WorkingScript.Globals["Timeline"] = Timeline;
        WorkingScript.Globals["State"] = State;
        WorkingScript.Globals["Tween"] = TweenSetters;
        WorkingScript.Globals["EasingWrapper"] = new EasingWrapperFunctions();
        WorkingScript.Globals["Constants"] = ModChartConstants;
        WorkingScript.Globals["Map"] = GameplayScreenView.Screen.Map;
        WorkingScript.Globals["Stage"] = ModChartStage;
        WorkingScript.Globals["Skin"] = SkinManager.Skin;
        WorkingScript.Globals["Notes"] = ModChartNotes;
        WorkingScript.Globals["SM"] = ModChartStateMachines;
        WorkingScript.Globals["Layers"] = ModChartLayers;
        WorkingScript.Globals["Audio"] = ModChartAudio;
        WorkingScript.Globals["Fonts"] = typeof(Fonts);
        WorkingScript.Globals["Events"] = ModChartEvents;
        WorkingScript.Globals["Vector"] = typeof(ModChartVector);
        WorkingScript.Globals["beat"] = CallbackFunction.FromDelegate(WorkingScript, ModChartUtils.Beat);
        WorkingScript.Globals["setUpdateInterval"] = CallbackFunction.FromDelegate(WorkingScript, SetUpdateInterval);

        WorkingScript.Options.DebugPrint = s => Logger.Debug(s, LogType.Runtime);

        ModChartScriptHelper.TryPerform(() =>
        {
            clock = new ContinuousClock(updateInterval);
            clock.Tick += Tick;
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
            Tick(null, EventArgs.Empty);
            clock.Start();
            WorkingScript.DoString(ScriptText, codeFriendlyName: Path.GetFileName(FilePath));
        });
    }

    private void RegisterEnum<T>(string globalVariableName) => RegisterEnum(typeof(T), globalVariableName);

    private void RegisterEnum(Type enumType, string globalVariableName)
    {
        UserData.RegisterType(enumType);
        WorkingScript.Globals[globalVariableName] = enumType;
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.String, enumType,
            dynVal => Enum.TryParse(enumType, dynVal.String, true, out var result)
                ? result
                : throw new ScriptRuntimeException($"Failed to parse '{dynVal.String}' as {globalVariableName}"));
    }

    public void Update(GameTime gameTime) => clock.Update(gameTime);

    private void SetUpdateInterval(double milliseconds)
    {
        updateInterval = TimeSpan.FromMilliseconds(milliseconds);
        clock.Interval = updateInterval;
    }

    public void Tick(object sender, EventArgs e)
    {
        if (Halted)
            return;

        if (ModChartScriptHelper.CounterExceeded)
        {
            Halted = true;
            clock.Stop();
            NotificationManager.Show(NotificationLevel.Error,
                $"Script stopped executing because there are {ModChartScriptHelper.ErrorCount} errors and" +
                $" {ModChartScriptHelper.TimeLimitExceedCount} calls that exceed" +
                $" {ModChartScriptHelper.MaxInstructionsPerCall} instructions per call!",
                forceShow: true);
            return;
        }

        var time = Shortcut.GameplayScreen.Timing.Time;

        State.SongTime = time;
        State.UnixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        State.CurrentTimingPoint = GameplayScreenView.Screen.Map.GetTimingPointAt(State.SongTime);
        State.WindowSize = new Vector2(ConfigManager.WindowWidth.Value, ConfigManager.WindowHeight.Value);

        TriggerManager.Update((int)time);
        SegmentManager.Update((int)time);
        ModChartStateMachines.RootMachine.Update();

        ModChartEvents.DeferredEventQueue.Dispatch();
    }

    private void RegisterLayer()
    {
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.String, typeof(Layer),
            dynVal => ModChartLayers[dynVal.String]);
    }

    private void RegisterBeat()
    {
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(int),
            dynVal =>
            {
                var tuple = dynVal.Table;
                return tuple.Length switch
                {
                    1 => (int)ModChartUtils.MeasureToTime((float)tuple.Get(1).Number),
                    2 => (int)ModChartUtils.BeatToTime((int)tuple.Get(1).Number, (float)tuple.Get(2).Number),
                    3 => (int)ModChartUtils.BeatToTime((int)tuple.Get(1).Number,
                        (int)tuple.Get(2).Number + (float)tuple.Get(3).Number),
                    4 => (int)ModChartUtils.BeatToTime((int)tuple.Get(1).Number, (float)(tuple.Get(2).Number +
                        tuple.Get(3).Number / tuple.Get(4).Number)),
                    _ => throw new ScriptRuntimeException($"Cannot convert tuple {dynVal} to time")
                };
            });
    }

    private void RegisterEasingType()
    {
        // Implicitly converts Easing to EasingWrapperFunction, so you can directly pass Easing in Timeline.Tween
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(EasingDelegate),
            dynVal =>
            {
                return dynVal.UserData.Object switch
                {
                    Easing easing => EasingWrapperFunctions.From(easing),
                    _ => throw new ScriptRuntimeException(
                        $"Cannot convert {dynVal.UserData.Descriptor.Name} to easing wrapper function")
                };
            }
        );
        // Implicitly converts string to EasingWrapperFunction, so you can directly pass string in Timeline.Tween
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.String, typeof(EasingDelegate),
            dynVal => EasingWrapperFunctions.From(Enum.TryParse(typeof(Easing), dynVal.String, true, out var result)
                ? (Easing)result!
                : throw new ScriptRuntimeException($"Failed to parse '{dynVal.String}' as Easing")));

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(EasingDelegate),
            dynVal => new EasingDelegate(p => dynVal.Function?.SafeCall(p)?.ToObject<float>() ?? 0));
    }

    private void RegisterKeyframe<T>()
    {
        // Constructs keyframes with tables {time, value, easingFunction}.
        // If in the form {time, value}, easingFunction defaults to Linear.
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Keyframe<T>),
            dynVal =>
            {
                var table = dynVal.Table;
                var time = (double)table[1];
                var value = table.Get(2).ToObject<T>();
                var easingFunction =
                    table.RawGet(3)?.ToObject<EasingDelegate>() ?? EasingWrapperFunctions.Linear;
                return new Keyframe<T>(time, value, easingFunction);
            }
        );
    }

    private void RegisterClosures()
    {
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(ITriggerPayload),
            dynVal =>
            {
                var triggerClosure = dynVal.Function;
                return new CustomTriggerPayload(v => { triggerClosure?.SafeCall(v); });
            }
        );
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function,
            typeof(Action<ValueVertex<ITriggerPayload>>),
            dynVal =>
            {
                var closure = dynVal.Function;
                return (ValueVertex<ITriggerPayload> v) => { closure?.SafeCall(v); };
            }
        );
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(ISegmentPayload),
            dynVal =>
            {
                var closure = dynVal.Function;
                return new CustomSegmentPayload((progress, segment) => closure?.SafeCall(progress, segment));
            }
        );
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function,
            typeof(CustomSegmentPayload.SegmentUpdater),
            dynVal =>
            {
                var closure = dynVal.Function;
                return new CustomSegmentPayload.SegmentUpdater((progress, segment) =>
                    closure?.SafeCall(progress, segment));
            }
        );
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function,
            typeof(Action<CustomStateMachineState>),
            dynVal =>
            {
                var closure = dynVal.Function;
                return (CustomStateMachineState state) => { closure?.SafeCall(state); };
            }
        );
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(ITriggerPayload),
            dynVal =>
            {
                return dynVal.UserData.Object switch
                {
                    ulong eventType =>
                        new CustomTriggerPayload(v => ModChartEvents.Enqueue((ModChartEventType)eventType, v)),
                    ITriggerPayload triggerPayload => triggerPayload,
                    _ => throw new ScriptRuntimeException(
                        $"Cannot convert {dynVal.UserData.Descriptor.Name} to trigger payload")
                };
            }
        );

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData,
            typeof(Action<CustomStateMachineState>),
            dynVal =>
            {
                return dynVal.UserData.Object switch
                {
                    ulong eventType =>
                        (CustomStateMachineState state) => ModChartEvents.Enqueue((ModChartEventType)eventType, state),
                    _ => throw new ScriptRuntimeException(
                        $"Cannot convert {dynVal.UserData.Descriptor.Name} to state machine callback")
                };
            }
        );
    }

    /// <summary>
    ///     Handles registering the Vector types for the script
    /// </summary>
    private void RegisterAllVectors()
    {
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(ModChartVector),
            v => new ModChartVector(v.Table.Values.Select(element => element.Number).ToArray()));

        // Vector 2
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Vector2),
            TableToVector2
        );

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(XnaVector2),
            TableToXnaVector2
        );

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(Vector2),
            UserDataToVector2
        );

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(XnaVector2),
            UserDataToXnaVector2
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector2>(
            (script, vector) => UserData.Create(new ModChartVector(vector.X, vector.Y)));

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<XnaVector2>(
            (script, vector) => UserData.Create(new ModChartVector(vector.X, vector.Y)));

        // Scalable Vector 2
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(ScalableVector2),
            TableToScalableVector2
        );

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(ScalableVector2),
            UserDataToScalableVector2
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<ScalableVector2>(
            (script, vector) => UserData.Create(
                new ModChartVector(vector.X.Value, vector.Y.Value)));

        // Vector3
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Vector3),
            TableToVector3
        );
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(Vector3),
            UserDataToVector3
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector3>(
            (script, vector) => UserData.Create(new ModChartVector(vector.X, vector.Y, vector.Z)));

        // Color
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Color),
            TableToColor
        );
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(Color),
            UserDataToColor
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Color>(
            (script, vector) => UserData.Create(new ModChartVector(vector.R, vector.G, vector.B, vector.A)));

        // Vector4
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Vector4),
            TableToVector4
        );
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(Vector4),
            UserDataToVector4
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector4>(
            (script, vector) => UserData.Create(new ModChartVector(vector.X, vector.Y, vector.Z, vector.W)));

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Padding),
            TableToPadding
        );
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(Padding),
            UserDataToPadding
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Padding>(
            (script, vector) => UserData.Create(new ModChartVector(vector.Left, vector.Right, vector.Up, vector.Down)));
    }

    private object TableToPadding(DynValue dynVal)
    {
        var table = dynVal.Table;
        var x = (int)((double)table[1]);
        var y = (int)((double)table[2]);
        var z = (int)((double)table[3]);
        var w = (int)((double)table[4]);
        return new Padding(x, y, z, w);
    }

    private object TableToVector4(DynValue dynVal)
    {
        var table = dynVal.Table;
        var x = (float)((double)table[1]);
        var y = (float)((double)table[2]);
        var z = (float)((double)table[3]);
        var w = (float)((double)table[4]);
        return new Vector4(x, y, z, w);
    }

    private object TableToColor(DynValue dynVal)
    {
        var table = dynVal.Table;
        var r = (byte)(double)table[1];
        var g = (byte)(double)table[2];
        var b = (byte)(double)table[3];
        var a = (byte)(double)table[4];
        return new Color(r, g, b, a);
    }

    private object TableToVector3(DynValue dynVal)
    {
        var table = dynVal.Table;
        var x = (float)((double)table[1]);
        var y = (float)((double)table[2]);
        var z = (float)((double)table[3]);
        return new Vector3(x, y, z);
    }

    private object TableToScalableVector2(DynValue dynVal)
    {
        var table = dynVal.Table;
        var x = (float)(double)table[1];
        var y = (float)(double)table[2];
        if (table.Length != 4) return new ScalableVector2(x, y);
        var sx = (float)(double)table[3];
        var sy = (float)(double)table[4];
        return new ScalableVector2(x, y, sx, sy);
    }

    private object TableToXnaVector2(DynValue dynVal)
    {
        var table = dynVal.Table;
        var x = (float)(double)table[1];
        var y = (float)(double)table[2];
        return new XnaVector2(x, y);
    }

    private object TableToVector2(DynValue dynVal)
    {
        var table = dynVal.Table;
        var x = (float)(double)table[1];
        var y = (float)(double)table[2];
        return new Vector2(x, y);
    }


    private object UserDataToPadding(DynValue dynVal)
    {
        var table = dynVal.ToObject<ModChartVector>();
        var x = (int)table[1];
        var y = (int)table[2];
        var z = (int)table[3];
        var w = (int)table[4];
        return new Padding(x, y, z, w);
    }

    private object UserDataToVector4(DynValue dynVal)
    {
        var table = dynVal.ToObject<ModChartVector>();
        return table.ToVector4();
    }

    private object UserDataToColor(DynValue dynVal)
    {
        var table = dynVal.ToObject<ModChartVector>();
        return table.ToColor();
    }

    private object UserDataToVector3(DynValue dynVal)
    {
        var table = dynVal.ToObject<ModChartVector>();
        return table.ToVector3();
    }

    private object UserDataToScalableVector2(DynValue dynVal)
    {
        var table = dynVal.ToObject<ModChartVector>();
        var x = (float)table[1];
        var y = (float)table[2];
        if (table.Count != 4) return new ScalableVector2(x, y);
        var sx = (float)table[3];
        var sy = (float)table[4];
        return new ScalableVector2(x, y, sx, sy);
    }

    private object UserDataToXnaVector2(DynValue dynVal)
    {
        var table = dynVal.ToObject<ModChartVector>();
        return table.ToXnaVector2();
    }

    private object UserDataToVector2(DynValue dynVal)
    {
        var table = dynVal.ToObject<ModChartVector>();
        return table.ToVector2();
    }
}