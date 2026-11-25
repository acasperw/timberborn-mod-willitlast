# Will It Last?

A Timberborn mod that tells you whether your water and food will last the next bad season (drought or badtide).

## Will your water and food last the next bad season?

Adds two status icons with popups that calculates based on current production / consumption, when the longest a hazard could hit, if you would have enough supplies to last the next season, assuming no production during a bad season

- Green dot: Your food or water supply is expected to last through the bad season.
- Orange dot: Your supply may run out during the bad season if you do not stockpile more.
- Red dot: Forecast to run out before the bad season ends.
- Gray dot: A forecast is unavailable for the current weather.

Modded weather can have different season rules. When its possible duration cannot be determined from the
game's weather settings, the forecast is unavailable until the hazardous weather begins. With Alternative
Weather System, forecasts are available during hazardous weather once its remaining duration is revealed;
the mod uses AWS's schedule even when Moddable Weathers is also installed. Running multiple weather systems
can still change weather effects independently, so forecasts remain estimates.

## Requirements

Timberborn 1.1.0 or later.

## Building

Built with the [Timberborn modding tools](https://github.com/mechanistry/timberborn-modding). Place this folder in `Assets/Mods/` and build with the Mod Builder.

## Changelog

# 1.4.0

- Fixed a crash when switching to modded weather.
- Added support for Alternative Weather System's weather schedule.
- The forecast shows a grey dot when it can't be calculated, such as for modded weather with an unknown length.

### 1.3.5

- Added French, German, Spanish, Polish, Brazilian Portuguese, Russian, Simplified Chinese, Japanese, Korean, Ukrainian, and Turkish translations.
- Translations were assisted by AI. Suggestions and corrections are welcome on [GitHub](https://github.com/acasperw/timberborn-mod-willitlast).

### 1.3.0
Slight better colors for contrast 

### 1.2.0

- Detects anything that satisfies thirst, instead of just water. (eg. Emberpelts)

### 1.1.1

- Status text no longer gives a day number ("Runs out during the bad season" instead of "Runs out on day N of the bad season").
- Stock that could run out before the bad season starts now shows "Runs out in N days", instead of hinting that the season starts soon.
- Tooltip shows the worst-case stock at the start of the bad season ("At least ~N left"), and hides it when stock runs out first.

### 1.0.0

- Initial release.