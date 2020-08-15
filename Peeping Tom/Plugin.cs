using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;

namespace PeepingTom {
    public class PeepingTomPlugin : IDalamudPlugin, IDisposable {
        public string Name => "Peeping Tom";

        internal DalamudPluginInterface Interface { get; private set; }
        internal Configuration Config { get; private set; }
        internal PluginUI Ui { get; private set; }
        internal TargetWatcher Watcher { get; private set; }

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface argument was null");
            this.Config = this.Interface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Config.Initialize(this.Interface);
            this.Watcher = new TargetWatcher(this);
            this.Ui = new PluginUI(this);

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
            this.Interface.UiBuilder.OnBuildUi += this.DrawUI;
            this.Interface.UiBuilder.OnOpenConfigUi += this.ConfigUI;

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
            this.Interface.UiBuilder.OnBuildUi -= DrawUI;
            this.Interface.UiBuilder.OnOpenConfigUi -= ConfigUI;
            this.Interface.CommandManager.RemoveHandler("/ppeepingtom");
            this.Interface.CommandManager.RemoveHandler("/ptom");
            this.Interface.CommandManager.RemoveHandler("/ppeep");
            this.Ui.Dispose();
        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void DrawUI() {
            this.Ui.Draw();
        }

        private void ConfigUI(object sender, EventArgs args) {
            this.Ui.SettingsOpen = true;
        }
    }
}
