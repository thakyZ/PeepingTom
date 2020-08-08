using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;

namespace PeepingTom {
    [Serializable]
    class Configuration : IPluginConfiguration {
        public int Version { get; set; } = 1;

        [NonSerialized]
        private DalamudPluginInterface pi;

        public bool MarkTargeted { get; set; } = false;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2235:Mark all non-serializable fields", Justification = "it works?")]
        public Vector4 TargetedColour { get; set; } = new Vector4(0f, 1f, 0f, 1f);
        public float TargetedSize { get; set; } = 2f;

        public bool MarkTargeting { get; set; } = false;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2235:Mark all non-serializable fields", Justification = "it works?")]
        public Vector4 TargetingColour { get; set; } = new Vector4(1f, 0f, 0f, 1f);
        public float TargetingSize { get; set; } = 2f;

        public bool DebugMarkers { get; set; } = false;

        public bool KeepHistory { get; set; } = true;
        public bool HistoryWhenClosed { get; set; } = true;
        public int NumHistory { get; set; } = 5;

        public bool LogParty { get; set; } = true;
        public bool LogAlliance { get; set; } = false;
        public bool LogInCombat { get; set; } = false;
        public bool LogSelf { get; set; } = false;

        public bool FocusTargetOnHover { get; set; } = true;
        public bool PlaySoundOnTarget { get; set; } = false;
        public string SoundPath { get; set; } = null;
        public float SoundCooldown { get; set; } = 10f;
        public bool PlaySoundWhenClosed { get; set; } = false;

        public bool OpenOnLogin { get; set; } = false;
        public bool AllowMovement { get; set; } = true;
        public bool ShowInCombat { get; set; } = false;
        public bool ShowInInstance { get; set; } = false;
        public bool ShowInCutscenes { get; set; } = false;

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.pi = pluginInterface;
        }

        public void Save() {
            this.pi.SavePluginConfig(this);
        }
    }
}
