using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.IoC;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using PeepingTom.Resources;
using XivCommon;
using Condition = Dalamud.Game.ClientState.Conditions.Condition;

namespace PeepingTom {
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PeepingTomPlugin : IDalamudPlugin {
        public string Name => "Peeping Tom";

        [PluginService]
        internal DalamudPluginInterface Interface { get; init; } = null!;

        [PluginService]
        internal ChatGui ChatGui { get; init; } = null!;

        [PluginService]
        internal ClientState ClientState { get; init; } = null!;

        [PluginService]
        private CommandManager CommandManager { get; init; } = null!;

        [PluginService]
        internal Condition Condition { get; init; } = null!;

        [PluginService]
        internal DataManager DataManager { get; init; } = null!;

        [PluginService]
        internal Framework Framework { get; init; } = null!;

        [PluginService]
        internal GameGui GameGui { get; init; } = null!;

        [PluginService]
        internal ObjectTable ObjectTable { get; init; } = null!;

        [PluginService]
        internal TargetManager TargetManager { get; init; } = null!;

        [PluginService]
        internal ToastGui ToastGui { get; init; } = null!;

        internal Configuration Config { get; }
        internal PluginUi Ui { get; }
        internal TargetWatcher Watcher { get; }
        internal XivCommonBase Common { get; }
        internal IpcManager IpcManager { get; }

        internal bool InPvp { get; private set; }

        public PeepingTomPlugin() {
            this.Common = new XivCommonBase();
            this.Config = this.Interface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Config.Initialize(this.Interface);
            this.Watcher = new TargetWatcher(this);
            this.Ui = new PluginUi(this);
            this.IpcManager = new IpcManager(this);

            OnLanguageChange(this.Interface.UiLanguage);
            this.Interface.LanguageChanged += OnLanguageChange;

            this.CommandManager.AddHandler("/ppeepingtom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Use with no arguments to show the list. Use with \"c\" or \"config\" to show the config",
            });
            this.CommandManager.AddHandler("/ptom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom",
            });
            this.CommandManager.AddHandler("/ppeep", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom",
            });

            this.ClientState.Login += this.OnLogin;
            this.ClientState.Logout += this.OnLogout;
            this.ClientState.TerritoryChanged += this.OnTerritoryChange;
            this.Interface.UiBuilder.Draw += this.DrawUi;
            this.Interface.UiBuilder.OpenConfigUi += this.ConfigUi;
        }

        public void Dispose() {
            this.Interface.UiBuilder.OpenConfigUi -= this.ConfigUi;
            this.Interface.UiBuilder.Draw -= this.DrawUi;
            this.ClientState.TerritoryChanged -= this.OnTerritoryChange;
            this.ClientState.Logout -= this.OnLogout;
            this.ClientState.Login -= this.OnLogin;
            this.CommandManager.RemoveHandler("/ppeep");
            this.CommandManager.RemoveHandler("/ptom");
            this.CommandManager.RemoveHandler("/ppeepingtom");
            this.Interface.LanguageChanged -= OnLanguageChange;
            this.IpcManager.Dispose();
            this.Ui.Dispose();
            this.Watcher.Dispose();
            this.Common.Dispose();
        }

        private static void OnLanguageChange(string langCode) {
            Language.Culture = new CultureInfo(langCode);
        }

        private void OnTerritoryChange(object? sender, ushort e) {
            try {
                var territory = this.DataManager.GetExcelSheet<TerritoryType>()!.GetRow(e);
                this.InPvp = territory?.IsPvpZone == true;
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

        private void OnLogin(object? sender, EventArgs args) {
            if (!this.Config.OpenOnLogin) {
                return;
            }

            this.Ui.WantsOpen = true;
        }

        private void OnLogout(object? sender, EventArgs args) {
            this.Ui.WantsOpen = false;
            this.Watcher.ClearPrevious();
        }

        private void DrawUi() {
            this.Ui.Draw();
        }

        private void ConfigUi() {
            this.Ui.SettingsOpen = true;
        }
    }
}
