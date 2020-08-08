using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;

namespace PeepingTom {
    public class PeepingTomPlugin : IDalamudPlugin, IDisposable {
        public string Name => "Peeping Tom";

        internal DalamudPluginInterface Interface { get; private set; }
        internal Configuration config;
        internal PluginUI ui;
        private HookManager hookManager;
        private TargetWatcher watcher;

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface argument was null");
            this.config = this.Interface.GetPluginConfig() as Configuration ?? new Configuration();
            this.config.Initialize(this.Interface);
            this.watcher = new TargetWatcher(this);
            this.ui = new PluginUI(this, this.config, this.Interface, this.watcher);
            this.hookManager = new HookManager(this.Interface, this.ui, this.config);

            this.Interface.CommandManager.AddHandler("/ppeepingtom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Use with no arguments to show the list. Use with \"c\" or \"config\" to show the config"
            });
            this.Interface.CommandManager.AddHandler("/ptom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom"
            });
            this.Interface.CommandManager.AddHandler("/ppeep", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom"
            });

            this.Interface.Framework.OnUpdateEvent += this.watcher.OnFrameworkUpdate;
            this.Interface.UiBuilder.OnBuildUi += this.DrawUI;
            this.Interface.UiBuilder.OnOpenConfigUi += this.ConfigUI;
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
            this.Interface.Framework.OnUpdateEvent -= this.watcher.OnFrameworkUpdate;
            this.watcher.Dispose();
            this.Interface.UiBuilder.OnBuildUi -= DrawUI;
            this.Interface.UiBuilder.OnOpenConfigUi -= ConfigUI;
            this.Interface.CommandManager.RemoveHandler("/ppeepingtom");
            this.Interface.CommandManager.RemoveHandler("/ptom");
            this.Interface.CommandManager.RemoveHandler("/ppeep");
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
