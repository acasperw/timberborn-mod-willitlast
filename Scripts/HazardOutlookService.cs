using System;
using Timberborn.GameCycleSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.HazardousWeatherSystemUI;
using Timberborn.WeatherSystem;
using UnityEngine;

namespace Mods.DroughtForecaster.Scripts {
  public class HazardOutlook {

    public bool IsHazard;
    public float MinDaysUntil;
    public float MaxDaysUntil;
    public float HazardDay;
    public int DaysLeft;
    public int MinDuration;
    public int MaxDuration;

  }

  public class HazardOutlookService {

    private readonly WeatherService _weatherService;
    private readonly GameCycleService _gameCycleService;
    private readonly TemperateWeatherDurationService _temperateWeatherDurationService;
    private readonly HazardousWeatherService _hazardousWeatherService;
    private readonly HazardousWeatherHistory _hazardousWeatherHistory;
    private readonly DroughtWeather _droughtWeather;
    private readonly BadtideWeather _badtideWeather;
    private readonly HazardousWeatherApproachingTimer _approachingTimer;

    public HazardOutlookService(WeatherService weatherService,
                                GameCycleService gameCycleService,
                                TemperateWeatherDurationService temperateWeatherDurationService,
                                HazardousWeatherService hazardousWeatherService,
                                HazardousWeatherHistory hazardousWeatherHistory,
                                DroughtWeather droughtWeather,
                                BadtideWeather badtideWeather,
                                HazardousWeatherApproachingTimer approachingTimer) {
      _weatherService = weatherService;
      _gameCycleService = gameCycleService;
      _temperateWeatherDurationService = temperateWeatherDurationService;
      _hazardousWeatherService = hazardousWeatherService;
      _hazardousWeatherHistory = hazardousWeatherHistory;
      _droughtWeather = droughtWeather;
      _badtideWeather = badtideWeather;
      _approachingTimer = approachingTimer;
    }

    public HazardOutlook Calculate() {
      var partialDay = _gameCycleService.PartialCycleDay;
      var startDay = _weatherService.HazardousWeatherStartCycleDay;
      var outlook = new HazardOutlook { IsHazard = _weatherService.IsHazardousWeather };

      if (outlook.IsHazard) {
        var duration = _weatherService.HazardousWeatherDuration;
        var elapsed = partialDay - startDay;
        outlook.HazardDay = elapsed + 1f;
        outlook.DaysLeft = Mathf.CeilToInt(duration - elapsed);
        outlook.MinDuration = duration;
        outlook.MaxDuration = duration;
        return outlook;
      }

      var current = _hazardousWeatherService.CurrentCycleHazardousWeather;
      // Vanilla reveals the start and type once the approaching warning shows.
      if (_approachingTimer.GetProgress() > 0f) {
        outlook.MinDaysUntil = outlook.MaxDaysUntil = startDay - partialDay;
        (outlook.MinDuration, outlook.MaxDuration) = GetRange(current, current);
      } else {
        // No warning yet means the start is at least the warning period away.
        var earliest = _temperateWeatherDurationService._minTemperateWeatherDuration + 1 - partialDay;
        var latest = _temperateWeatherDurationService._maxTemperateWeatherDuration + 1 - partialDay;
        outlook.MinDaysUntil = Mathf.Max(earliest, _approachingTimer._spec.ApproachingNotificationDays);
        outlook.MaxDaysUntil = Mathf.Max(latest, outlook.MinDaysUntil);
        (outlook.MinDuration, outlook.MaxDuration) = GetRange(_droughtWeather, current);
        if (_badtideWeather.CanOccurAtCycle(_gameCycleService.Cycle) && _badtideWeather.ChanceForBadtide > 0f) {
          var (badtideMin, badtideMax) = GetRange(_badtideWeather, current);
          outlook.MinDuration = Math.Min(outlook.MinDuration, badtideMin);
          outlook.MaxDuration = Math.Max(outlook.MaxDuration, badtideMax);
        }
      }
      return outlook;
    }

    private (int min, int max) GetRange(IHazardousWeather weather, IHazardousWeather current) {
      var cyclesCount = _hazardousWeatherHistory.GetCyclesCount(weather.Id);
      // History already includes the current cycle's pick.
      var cycle = weather == current ? cyclesCount : cyclesCount + 1;
      return weather == _droughtWeather
          ? Scale(cycle, _droughtWeather._minDroughtDuration, _droughtWeather._maxDroughtDuration,
                  _droughtWeather._handicapMultiplier, _droughtWeather._handicapCycles)
          : Scale(cycle, _badtideWeather._minDuration, _badtideWeather._maxDuration,
                  _badtideWeather._handicapMultiplier, _badtideWeather._handicapCycles);
    }

    private static (int min, int max) Scale(int cycle, int min, int max, float handicapMultiplier,
                                            int handicapCycles) {
      var multiplier = HazardousWeatherHelper.GetHandicapMultiplier(cycle, handicapMultiplier, handicapCycles);
      var scaledMin = (int)Math.Round(multiplier * min, MidpointRounding.AwayFromZero);
      var scaledMax = (int)Math.Round(multiplier * max, MidpointRounding.AwayFromZero);
      if (min > 0) {
        scaledMin = Math.Max(scaledMin, 1);
        scaledMax = Math.Max(scaledMax, 1);
      }
      return (scaledMin, scaledMax);
    }

  }
}
