using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Globalization;
using Lumina.Excel.GeneratedSheets;
using PeepingTom.Resources;
using XivCommon;

namespace PeepingTom {
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PeepingTomPlugin : IDalamudPlugin {
        public string Name => "Peeping Tom";

        internal DalamudPluginInterface Interface { get; private set; } = null!;
        internal Configuration Config { get; private set; } = null!;
        internal PluginUi Ui { get; private set; } = null!;
        internal TargetWatcher Watcher { get; private set; } = null!;
        internal XivCommonBase Common { get; private set; } = null!;

        internal bool InPvp { get; private set; }

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.Interface = pluginInterface;
            this.Common = new XivCommonBase(this.Interface);
            this.Config = this.Interface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Config.Initialize(this.Interface);
            this.Watcher = new TargetWatcher(this);
            this.Ui = new PluginUi(this);

            OnLanguageChange(this.Interface.UiLanguage);
            this.Interface.OnLanguageChanged += OnLanguageChange;

            this.Interface.CommandManager.AddHandler("/ppeepingtom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Use with no arguments to show the list. Use with \"c\" or \"config\" to show the config",
            });
            this.Interface.CommandManager.AddHandler("/ptom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom",
            });
            this.Interface.CommandManager.AddHandler("/ppeep", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom",
            });

            this.Interface.Framework.OnUpdateEvent += this.Watcher.OnFrameworkUpdate;
            this.Interface.ClientState.OnLogin += this.OnLogin;
            this.Interface.ClientState.OnLogout += this.OnLogout;
            this.Interface.ClientState.TerritoryChanged += this.OnTerritoryChange;
            this.Interface.UiBuilder.OnBuildUi += this.DrawUi;
            this.Interface.UiBuilder.OnOpenConfigUi += this.ConfigUi;

            this.Watcher.StartThread();
        }

        public void Dispose() {
            this.Common.Dispose();
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
            this.Interface.OnLanguageChanged -= OnLanguageChange;
        }

        private static void OnLanguageChange(string langCode) {
            Language.Culture = new CultureInfo(langCode);
        }

        private void OnTerritoryChange(object sender, ushort e) {
            try {
                var territory = this.Interface.Data.GetExcelSheet<TerritoryType>().GetRow(e);
                this.InPvp = territory.IsPvpZone;
            } catch (KeyNotFoundException) {
                PluginLog.Warning("Could not get territory for current zone");
            }
        }

        private void OnCommand(string command, string args) {
            if (args is "config" or "c") {
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

        private void DrawUi() {
            this.Ui.Draw();
        }

        private void ConfigUi(object sender, EventArgs args) {
            this.Ui.SettingsOpen = true;
        }
    }
}
