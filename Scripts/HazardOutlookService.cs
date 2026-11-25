using System;
using System.Linq;
using System.Reflection;
using Bindito.Core;
using Timberborn.GameCycleSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.HazardousWeatherSystemUI;
using Timberborn.WeatherSystem;
using UnityEngine;

namespace Mods.DroughtForecaster.Scripts {
  public class HazardOutlook {

    public bool IsAvailable = true;
    public bool IsHazard;
    public int HazardStartCycleDay;
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
    private readonly bool _alternativeWeatherDetected;
    private readonly AlternativeWeatherOutlook _alternativeWeatherOutlook;

    public HazardOutlookService(IContainer container,
                                WeatherService weatherService,
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
      var alternativeAssembly = AppDomain.CurrentDomain.GetAssemblies()
          .FirstOrDefault(assembly => assembly.GetName().Name == "AlternativeWeatherSystem");
      _alternativeWeatherDetected = alternativeAssembly != null;
      if (_alternativeWeatherDetected) {
        var scheduleType = alternativeAssembly.GetType("AlternativeWeatherSystem.Weather.WeatherSchedule");
        if (scheduleType == null) {
          Debug.LogWarning("[Will It Last?] Alternative Weather System schedule not found; forecast unavailable.");
        } else {
          try {
            _alternativeWeatherOutlook = new AlternativeWeatherOutlook(container.GetInstance(scheduleType));
          } catch (BinditoException exception) {
            Debug.LogWarning($"[Will It Last?] Cannot resolve Alternative Weather System schedule: {exception}");
          }
        }
      }
    }

    public HazardOutlook Calculate() {
      if (_alternativeWeatherDetected) {
        return _alternativeWeatherOutlook?.Calculate(_gameCycleService) ?? new HazardOutlook { IsAvailable = false };
      }
      var partialDay = _gameCycleService.PartialCycleDay;
      var startDay = _weatherService.HazardousWeatherStartCycleDay;
      var outlook = new HazardOutlook { IsHazard = _weatherService.IsHazardousWeather };

      if (outlook.IsHazard) {
        var duration = _weatherService.HazardousWeatherDuration;
        var elapsed = partialDay - startDay;
        if (startDay < 1 || duration <= 0 || elapsed < 0f || elapsed >= duration) {
          outlook.IsAvailable = false;
          return outlook;
        }
        outlook.HazardStartCycleDay = startDay;
        outlook.HazardDay = elapsed + 1f;
        outlook.DaysLeft = Mathf.CeilToInt(duration - elapsed);
        outlook.MinDuration = duration;
        outlook.MaxDuration = duration;
        return outlook;
      }

      var current = _hazardousWeatherService.CurrentCycleHazardousWeather;
      if (current == null || startDay < 1) {
        outlook.IsAvailable = false;
        return outlook;
      }
      var isVanillaWeather = current == _droughtWeather || current == _badtideWeather;
      if (!isVanillaWeather) {
        outlook.IsAvailable = false;
      } else if (_approachingTimer.GetProgress() > 0f) {
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

    private class AlternativeWeatherOutlook {

      private readonly object _schedule;
      private readonly PropertyInfo _current;
      private readonly PropertyInfo _cycle;
      private readonly PropertyInfo _mildDays;
      private readonly PropertyInfo _typeId;
      private readonly PropertyInfo _days;
      private readonly MethodInfo _running;
      private readonly MethodInfo _carrierOf;
      private readonly MethodInfo _value;
      private readonly bool _isSupported;

      public AlternativeWeatherOutlook(object schedule) {
        _schedule = schedule;
        var scheduleType = schedule.GetType();
        _current = scheduleType.GetProperty("Current");
        var planType = _current?.PropertyType;
        _cycle = planType?.GetProperty("Cycle");
        _mildDays = planType?.GetProperty("MildDays");
        _running = planType?.GetMethod("Running", new[] { typeof(int) });
        var spellType = _running?.ReturnType;
        _typeId = spellType?.GetProperty("TypeId");
        _days = spellType?.GetProperty("Days");
        _value = spellType?.GetMethod("Value", new[] { typeof(string) });
        _carrierOf = scheduleType.GetMethod("CarrierOf", new[] { typeof(string) });
        _isSupported = _current != null && _cycle?.PropertyType == typeof(int) &&
            _mildDays?.PropertyType == typeof(int) && _typeId?.PropertyType == typeof(string) &&
            _days?.PropertyType == typeof(int) && _value?.ReturnType == typeof(double) &&
            _carrierOf != null && _carrierOf.ReturnType.IsEnum;
        if (!_isSupported) {
          Debug.LogWarning("[Will It Last?] Unsupported Alternative Weather System API; forecast unavailable.");
        }
      }

      public HazardOutlook Calculate(GameCycleService gameCycleService) {
        var unavailable = new HazardOutlook { IsAvailable = false };
        if (!_isSupported) {
          return unavailable;
        }
        var plan = _current.GetValue(_schedule);
        if (plan == null || (int)_cycle.GetValue(plan) != gameCycleService.Cycle) {
          return unavailable;
        }
        var spell = _running.Invoke(plan, new object[] { gameCycleService.CycleDay });
        if (spell == null) {
          return unavailable;
        }
        var typeId = (string)_typeId.GetValue(spell);
        var carrier = _carrierOf.Invoke(_schedule, new object[] { typeId }).ToString();
        if (carrier != "Drought" && carrier != "Badtide") {
          return unavailable;
        }
        unavailable.IsHazard = true;
        var mildDays = (int)_mildDays.GetValue(plan);
        var startDay = gameCycleService.CycleDay <= mildDays ? 1 : mildDays + 1;
        var duration = (int)_days.GetValue(spell);
        var elapsed = gameCycleService.PartialCycleDay - startDay;
        if (duration <= 0 || elapsed < 0f || elapsed >= duration) {
          return unavailable;
        }
        var daysLeft = duration - elapsed;
        var notice = (double)_value.Invoke(spell, new object[] { "DurationNotice" });
        var noticeDays = double.IsNaN(notice) || notice < 0 ? 0 :
            notice >= duration ? duration : (int)notice;
        if (daysLeft > noticeDays) {
          return unavailable;
        }
        // Another weather mod can end the game cycle before AWS's planned spell ends.
        duration = Math.Min(duration, gameCycleService._cycleDurationInDays - startDay + 1);
        if (duration <= 0 || elapsed >= duration) {
          return unavailable;
        }
        return new HazardOutlook {
            IsHazard = true,
            HazardStartCycleDay = startDay,
            HazardDay = elapsed + 1f,
            DaysLeft = Mathf.CeilToInt(duration - elapsed),
            MinDuration = duration,
            MaxDuration = duration
        };
      }

    }

  }
}
