using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace Mods.DroughtForecaster.Scripts {
  // lives in a separate assembly shipped as a .bin file so is not loaded automatically
  public class ModSettingsLoader : IModStarter {

    private const string ModSettingsAssemblyName = "ModSettings.Core";
    private static readonly string SupportAssemblyPath =
        Path.Combine("ModSettingsSupport", "WillItLastModSettings.bin");

    public void StartMod(IModEnvironment modEnvironment) {
      var modSettings = AppDomain.CurrentDomain.GetAssemblies()
          .FirstOrDefault(assembly => assembly.GetName().Name == ModSettingsAssemblyName);
      if (modSettings == null) {
        return;
      }
      var path = Path.Combine(modEnvironment.ModPath, SupportAssemblyPath);
      if (!File.Exists(path)) {
        Debug.LogWarning($"[Will It Last?] Mod Settings support file not found at {path}.");
        return;
      }
      try {
        if (!IsCompatible(modSettings)) {
          Debug.LogWarning("[Will It Last?] Unsupported Mod Settings version; settings menu disabled.");
          return;
        }
        Assembly.Load(File.ReadAllBytes(path));
      } catch (Exception exception) {
        Debug.LogWarning($"[Will It Last?] Cannot load Mod Settings support: {exception}");
      }
    }

    // Mirrors every Mod Settings member used by WillItLastModSettings
    private static bool IsCompatible(Assembly modSettings) {
      const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
      var owner = modSettings.GetType("ModSettings.Core.ModSettingsOwner");
      var registry = modSettings.GetType("ModSettings.Core.ModSettingsOwnerRegistry");
      var setting = modSettings.GetType("ModSettings.Core.ModSetting`1");
      var descriptor = modSettings.GetType("ModSettings.Core.ModSettingDescriptor");
      var context = modSettings.GetType("ModSettings.Core.ModSettingsContext");
      if (owner == null || registry == null || setting == null || descriptor == null || context == null) {
        return false;
      }
      var ownerParameters = owner.GetConstructors(instance)
          .Select(constructor => constructor.GetParameters().Select(p => p.ParameterType.FullName))
          .Any(parameters => parameters.SequenceEqual(new[] {
              "Timberborn.SettingsSystem.ISettings", registry.FullName, "Timberborn.Modding.ModRepository"
          }));
      var changeableOn = owner.GetProperty("ChangeableOn", instance);
      var modId = owner.GetProperty("ModId", instance);
      return ownerParameters
          && changeableOn?.PropertyType == context && changeableOn.GetMethod.IsVirtual
          && modId?.PropertyType == typeof(string) && modId.GetMethod.IsVirtual
          && Enum.IsDefined(context, "All")
          && setting.MakeGenericType(typeof(bool)).GetConstructor(new[] { typeof(bool), descriptor }) != null
          && descriptor.GetMethod("CreateLocalized", new[] { typeof(string) })?.ReturnType == descriptor;
    }

  }
}
