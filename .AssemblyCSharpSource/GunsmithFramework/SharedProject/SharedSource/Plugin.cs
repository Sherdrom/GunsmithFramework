namespace GunsmithFramework
{
    public partial class GunsmithFramework : IAssemblyPlugin
    {
        public IConfigService ConfigService { get; set; } = null!;
        public IPluginManagementService PluginService { get; set; } = null!;
        public ILoggerService LoggerService { get; set; } = null!;

        public static ContentPackage? Package { get; private set; }
        private Harmony? harmonyInstance;

        public void Initialize()
        {
            harmonyInstance = new Harmony("GunsmithFramework");
            if (PluginService.TryGetPackageForPlugin<GunsmithFramework>(out ContentPackage ownerPackage))
            {
                Package = ownerPackage;
            }

            InitializePlatform();
            LoggerService.Log("GunsmithFramework initialized.");
        }

        public void OnLoadCompleted()
        {
            harmonyInstance?.PatchAll();
            GunsmithNpcPresetPatch.ReportReady();
            if (harmonyInstance != null)
            {
                GunsmithQuickAttachmentBarrelSelectorPatch.PatchOptionalVce(harmonyInstance);
            }
            OnLoadCompletedPlatform();
        }

        public void PreInitPatching()
        {
        }

        public void Dispose()
        {
            GunsmithLuaHooks.Clear();
            harmonyInstance?.UnpatchSelf();
            DisposePlatform();
            GunsmithRuntimeStates.Clear();
            harmonyInstance = null;
        }

        partial void InitializePlatform();
        partial void OnLoadCompletedPlatform();
        partial void DisposePlatform();
    }
}
