using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PeepingTom {
    public class GameFunctions {
        private delegate IntPtr GetListDelegate(IntPtr basePtr);
        private delegate long RequestCharInfoDelegate(IntPtr ptr);

        private readonly PeepingTomPlugin plugin;

        private readonly RequestCharInfoDelegate _requestCharInfo = null;

        public GameFunctions(PeepingTomPlugin plugin) {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "PeepingTomPlugin cannot be null");

            IntPtr rciPtr;
            try {
                rciPtr = this.plugin.Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 40 BA 3B 01 00 00");
                //                                                               old: 48 89 5C 24 ?? 57 48 83 EC 40 BA 1C 01 00 00
                //                                                               old: 48 89 5C 24 ?? 57 48 83 EC 40 BA 1B 01 00 00
            } catch (KeyNotFoundException) {
                rciPtr = IntPtr.Zero;
            }
            if (rciPtr == IntPtr.Zero) {
                PluginLog.Log("Could not find the signature for the examine window function - will not be able to open examine window.");
                return;
            }

            this._requestCharInfo = Marshal.GetDelegateForFunctionPointer<RequestCharInfoDelegate>(rciPtr);
        }

        private static IntPtr FollowPtrChain(IntPtr start, int[] offsets) {
            foreach (int offset in offsets) {
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

            IntPtr framework = this.plugin.Interface.Framework.Address.BaseAddress;

            IntPtr getListPtr = FollowPtrChain(framework, new int[] { 0x29f8, 0, 0x110 });
            var getList = Marshal.GetDelegateForFunctionPointer<GetListDelegate>(getListPtr);
            IntPtr list = getList(Marshal.ReadIntPtr(framework + 0x29f8));
            IntPtr rciData = Marshal.ReadIntPtr(list + 0x188);

            Marshal.WriteInt32(rciData + 0x28, actor.ActorId);
            Marshal.WriteInt32(rciData + 0x2c, actor.ActorId);
            Marshal.WriteInt32(rciData + 0x30, actor.ActorId);

            this._requestCharInfo(rciData);
        }

    }
}
