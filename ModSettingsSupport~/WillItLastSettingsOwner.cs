using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace Mods.DroughtForecaster.ModSettingsSupport {
  // Mod Settings stores values as "ModSetting.<ModId>.<type name>.<property name>";
  public class WillItLastSettingsOwner : ModSettingsOwner {

    public ModSetting<bool> ShowWater { get; } =
      new(true, ModSettingDescriptor.CreateLocalized("WillItLast.Settings.ShowWater"));

    public ModSetting<bool> ShowFood { get; } =
      new(true, ModSettingDescriptor.CreateLocalized("WillItLast.Settings.ShowFood"));

    public WillItLastSettingsOwner(ISettings settings,
                                   ModSettingsOwnerRegistry modSettingsOwnerRegistry,
                                   ModRepository modRepository)
        : base(settings, modSettingsOwnerRegistry, modRepository) {
    }

    public override ModSettingsContext ChangeableOn => ModSettingsContext.All;

    protected override string ModId => "acasperw.WillItLast";

  }
}
