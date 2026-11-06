# Will It Last?

A Timberborn mod that tells you whether your water and food will last the next bad season (drought or badtide).

## Will your water and food last the next bad season?

Adds two status icons with popups that calculates based on current production / consumption, when the longest a hazard could hit, if you would have enough supplies to last the next season, assuming no production during a bad season

- Green: Your food or water supply is expected to last through the bad season.
- Amber: Your supply may run out during the bad season if you do not stockpile more.
- Red: Forecast to run out before the bad season ends.

## Requirements

Timberborn 1.1.0 or later.

## Building

Built with the [Timberborn modding tools](https://github.com/mechanistry/timberborn-modding). Place this folder in `Assets/Mods/` and build with the Mod Builder.

## Changelog

### 1.1.0

- Status text no longer gives a day number ("Runs out during the bad season" instead of "Runs out on day N of the bad season").
- Stock that could run out before the bad season starts now shows "Runs out in N days", instead of hinting that the season starts soon.
- Tooltip shows the worst-case stock at the start of the bad season ("At least ~N left"), and hides it when stock runs out first.

### 1.0.0

- Initial release.