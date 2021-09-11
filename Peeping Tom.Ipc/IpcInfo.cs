using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using PeepingTom.Ipc.From;
using PeepingTom.Ipc.To;

namespace PeepingTom.Ipc {
    public static class IpcInfo {
        public const string FromRegistrationName = "PeepingTom.From";
        public const string ToRegistrationName = "PeepingTom.To";

        public static ICallGateProvider<IToMessage, object> GetProvider(DalamudPluginInterface @interface) {
            return @interface.GetIpcProvider<IToMessage, object>(ToRegistrationName);
        }

        public static ICallGateSubscriber<IFromMessage, object> GetSubscriber(DalamudPluginInterface @interface) {
            return @interface.GetIpcSubscriber<IFromMessage, object>(FromRegistrationName);
        }
    }
}
