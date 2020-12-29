using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;

namespace PeepingTom {
    public class PeepingTomPlugin : IDalamudPlugin {
        public string Name => "Peeping Tom";

        internal DalamudPluginInterface Interface { get; private set; } = null!;
        internal Configuration Config { get; private set; } = null!;
        internal PluginUi Ui { get; private set; } = null!;
        internal TargetWatcher Watcher { get; private set; } = null!;
        internal GameFunctions GameFunctions { get; private set; } = null!;

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface argument was null");
            this.Config = this.Interface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Config.Initialize(this.Interface);
            this.Watcher = new TargetWatcher(this);
            this.GameFunctions = new GameFunctions(this);
            this.Ui = new PluginUi(this);

            this.Interface.CommandManager.AddHandler("/ppeepingtom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Use with no arguments to show the list. Use with \"c\" or \"config\" to show the config"
            });
            this.Interface.CommandManager.AddHandler("/ptom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom"
            });
            this.Interface.CommandManager.AddHandler("/ppeep", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom"
            });

            this.Interface.Framework.OnUpdateEvent += this.Watcher.OnFrameworkUpdate;
            this.Interface.ClientState.OnLogin += this.OnLogin;
            this.Interface.ClientState.OnLogout += this.OnLogout;
            this.Interface.UiBuilder.OnBuildUi += this.DrawUi;
            this.Interface.UiBuilder.OnOpenConfigUi += this.ConfigUi;

            this.Watcher.StartThread();
        }

        private void OnCommand(string command, string args) {
            if (args == "config" || args == "c") {
                this.Ui.SettingsOpen = true;
            } else {
                this.Ui.WantsOpen = true;
            }
        }

        private void OnLogin(object sender, EventArgs args) {
            if (!this.Config.OpenOnLogin) {
                return;
            }

            this.Ui.WantsOpen = true;
        }

        private void OnLogout(object sender, EventArgs args) {
            this.Ui.WantsOpen = false;
            this.Watcher.ClearPrevious();
        }

        protected virtual void Dispose(bool includeManaged) {
            this.Interface.Framework.OnUpdateEvent -= this.Watcher.OnFrameworkUpdate;
            this.Interface.ClientState.OnLogin -= this.OnLogin;
            this.Interface.ClientState.OnLogout -= this.OnLogout;
            this.Watcher.WaitStopThread();
            this.Watcher.Dispose();
            this.Interface.UiBuilder.OnBuildUi -= this.DrawUi;
            this.Interface.UiBuilder.OnOpenConfigUi -= this.ConfigUi;
            this.Interface.CommandManager.RemoveHandler("/ppeepingtom");
            this.Interface.CommandManager.RemoveHandler("/ptom");
            this.Interface.CommandManager.RemoveHandler("/ppeep");
            this.Ui.Dispose();
        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void DrawUi() {
            this.Ui.Draw();
        }

        private void ConfigUi(object sender, EventArgs args) {
            this.Ui.SettingsOpen = true;
        }
    }
}
