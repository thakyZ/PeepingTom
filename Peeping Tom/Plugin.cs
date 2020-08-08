using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;

namespace PeepingTom {
    public class PeepingTomPlugin : IDalamudPlugin, IDisposable {
        public string Name => "Peeping Tom";

        private DalamudPluginInterface pi;
        private Configuration config;
        private PluginUI ui;
        private HookManager hookManager;
        private TargetWatcher watcher;

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.pi = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface argument was null");
            this.config = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.config.Initialize(this.pi);
            this.watcher = new TargetWatcher(this.pi, this.config);
            this.ui = new PluginUI(this, this.config, this.pi, this.watcher);
            this.hookManager = new HookManager(this.pi, this.ui, this.config);

            this.pi.CommandManager.AddHandler("/ppeepingtom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Use with no arguments to show the list. Use with \"c\" or \"config\" to show the config"
            });
            this.pi.CommandManager.AddHandler("/ptom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom"
            });
            this.pi.CommandManager.AddHandler("/ppeep", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom"
            });

            this.pi.Framework.OnUpdateEvent += this.watcher.OnFrameworkUpdate;
            this.pi.UiBuilder.OnBuildUi += this.DrawUI;
            this.pi.UiBuilder.OnOpenConfigUi += this.ConfigUI;
        }

        private void OnCommand(string command, string args) {
            if (args == "config" || args == "c") {
                this.ui.SettingsVisible = true;
            } else {
                this.ui.Visible = true;
            }
        }

        protected virtual void Dispose(bool includeManaged) {
            this.hookManager.Dispose();
            this.pi.Framework.OnUpdateEvent -= this.watcher.OnFrameworkUpdate;
            this.watcher.Dispose();
            this.pi.UiBuilder.OnBuildUi -= DrawUI;
            this.pi.UiBuilder.OnOpenConfigUi -= ConfigUI;
            this.pi.CommandManager.RemoveHandler("/ppeepingtom");
            this.pi.CommandManager.RemoveHandler("/ptom");
            this.pi.CommandManager.RemoveHandler("/ppeep");
            this.ui.Dispose();
        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void DrawUI() {
            this.ui.Draw();
        }

        private void ConfigUI(object sender, EventArgs args) {
            this.ui.SettingsVisible = true;
        }
    }
}
