using Dalamud.Hooking;
using Dalamud.Plugin;
using System;

namespace PeepingTom {
    class HookManager : IDisposable {
        private readonly PeepingTomPlugin plugin;

        private readonly Hook<LoginDelegate> loginHook;
        private readonly Hook<LogoutDelegate> logoutHook;

        private delegate void LoginDelegate(IntPtr ptr, IntPtr ptr2);
        private delegate void LogoutDelegate(IntPtr ptr);

        public HookManager(PeepingTomPlugin plugin) {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "PeepingTomPlugin cannot be null");

            IntPtr loginPtr = this.plugin.Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 54 41 56 41 57 48 83 EC 20 48 8B F2");
            if (loginPtr != IntPtr.Zero) {
                this.loginHook = new Hook<LoginDelegate>(loginPtr, new LoginDelegate(this.OnLogin), this);
                this.loginHook.Enable();
            } else {
                PluginLog.Log("Could not hook LoginDelegate");
            }

            IntPtr logoutPtr = this.plugin.Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B D9 E8 ?? ?? ?? ?? 33 ED 66 C7 03 00 00");
            if (logoutPtr != IntPtr.Zero) {
                this.logoutHook = new Hook<LogoutDelegate>(logoutPtr, new LogoutDelegate(this.OnLogout), this);
                this.logoutHook.Enable();
            } else {
                PluginLog.Log("Could not hook LogoutDelegate");
            }
        }

        private void OnLogin(IntPtr ptr, IntPtr ptr2) {
            this.loginHook.Original(ptr, ptr2);

            if (!this.plugin.Config.OpenOnLogin) {
                return;
            }

            this.plugin.Ui.WantsOpen = true;
        }

        private void OnLogout(IntPtr ptr) {
            this.logoutHook.Original(ptr);

            this.plugin.Ui.WantsOpen = false;
            this.plugin.Watcher.ClearPrevious();
        }

        public void Dispose() {
            this.loginHook?.Dispose();
            this.logoutHook?.Dispose();
        }
    }
}
