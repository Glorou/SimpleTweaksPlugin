using Dalamud.IoC;
using Dalamud.Plugin.Services;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;


namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Auto Character Rendering")]
[TweakDescription("Automatically adjusts the number of characters rendered to try and maintain target FPS.")]
[TweakCategory(TweakCategory.QoL)]
[TweakAutoConfig]
[TweakReleaseVersion("0.1")]

public class CharaRendering : Tweak
{

    public class Configs : TweakConfig
    {
        [TweakConfigOption("Target FPS")] public int fpsTarget = 80;
    }
    [TweakConfig] public Configs Config { get; private set; }

    protected void DrawConfig()
    {
        ImGui.Indent();
        ImGui.InputInt("Target FPS", ref Config.fpsTarget);
    }

    private readonly System.Timers.Timer _timer = new();

    protected override void Enable()
    {
        _timer.Elapsed += Timer_Elapsed;
        _timer.Interval = 10000; // every 10 seconds
        _timer.Start();
    }


    public void Disable()
    {
        _timer.Stop();
    }



    private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        var utils = new Utils();
        var usage = Utils.GetGPUUsage(utils.GetCounters()).Result;
        uint currentSetting;
        Service.GameConfig.TryGet(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, out currentSetting);

        if (currentSetting < 4 && (usage > 80 || utils.GetFPS() < Config.fpsTarget * 0.8f)) // Check if we're already at an extrema, if we are then no point in changing
        {
            SimpleLog.Information($"Going down towards min {currentSetting} {usage} {utils.GetFPS()} {Config.fpsTarget}");
            Service.GameConfig.Set(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, currentSetting + 1);
        } else if (currentSetting > 0 && usage < 80 && utils.GetFPS() > Config.fpsTarget * 0.95f)
        {
            SimpleLog.Information($"Going up towards max {currentSetting} {usage} {utils.GetFPS()} {Config.fpsTarget}");
            Service.GameConfig.Set(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, currentSetting - 1);
        }
        return;
    }





}
public class Utils
{
    public List<PerformanceCounter> GetCounters()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        var names = category.GetInstanceNames();
        var utilization = names
                            .Where(counterName => counterName.EndsWith("engtype_3D"))
                            .SelectMany(counterName => category.GetCounters(counterName))
                            .Where(counter => counter.CounterName.Equals("Utilization Percentage"))
                            .ToList();
        return utilization;
    }

    public static async Task<float> GetGPUUsage(List<PerformanceCounter> gpuCounters)
    {

        if (!Service.Framework.IsInFrameworkUpdateThread)
        {
            gpuCounters.ForEach(x => x.NextValue());

            Thread.Sleep(1000);

            var result = gpuCounters.Sum(x => x.NextValue());

            return result;
        }
        else
        {
            var result = await Task.Run<float>(() =>
            {
                gpuCounters.ForEach(x => x.NextValue());

                Thread.Sleep(1000);

                return gpuCounters.Sum(x => x.NextValue());
            });
            return result;
        }
    }


    public float GetFPS()
    {
        var temp = 1 / Service.Framework.UpdateDelta.TotalSeconds;
        return (float)temp;
    }
}