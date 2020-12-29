using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PeepingTom {
    public class GameFunctions {
        private delegate IntPtr GetListDelegate(IntPtr basePtr);
        private delegate long RequestCharInfoDelegate(IntPtr ptr);

        private PeepingTomPlugin Plugin { get; }

        private readonly RequestCharInfoDelegate? _requestCharInfo;

        public GameFunctions(PeepingTomPlugin plugin) {
            this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "PeepingTomPlugin cannot be null");

            IntPtr rciPtr;
            try {
                rciPtr = this.Plugin.Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 83 7B 30 00 74 47");
            } catch (KeyNotFoundException) {
                rciPtr = IntPtr.Zero;
            }
            if (rciPtr == IntPtr.Zero) {
                PluginLog.Log("Could not find the signature for the examine window function - will not be able to open examine window.");
                return;
            }

            this._requestCharInfo = Marshal.GetDelegateForFunctionPointer<RequestCharInfoDelegate>(rciPtr);
        }

        private static IntPtr FollowPtrChain(IntPtr start, IEnumerable<int> offsets) {
            foreach (var offset in offsets) {
                start = Marshal.ReadIntPtr(start, offset);
                if (start == IntPtr.Zero) {
                    break;
                }
            }

            return start;
        }

        public void OpenExamineWindow(Actor actor) {
            if (this._requestCharInfo == null) {
                return;
            }

            var framework = this.Plugin.Interface.Framework.Address.BaseAddress;

            var getListPtr = FollowPtrChain(framework, new[] { 0x29f8, 0, 0x110 });
            var getList = Marshal.GetDelegateForFunctionPointer<GetListDelegate>(getListPtr);
            var list = getList(Marshal.ReadIntPtr(framework + 0x29f8));
            var rciData = Marshal.ReadIntPtr(list + 0x188);

            Marshal.WriteInt32(rciData + 0x28, actor.ActorId);
            Marshal.WriteInt32(rciData + 0x2c, actor.ActorId);
            Marshal.WriteInt32(rciData + 0x30, actor.ActorId);

            this._requestCharInfo(rciData);
        }

    }
}
