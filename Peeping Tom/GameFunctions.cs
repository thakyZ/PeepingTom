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
                // got this by checking what accesses rciData below
                rciPtr = this.Plugin.Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 40 BA ?? ?? ?? ?? 48 8B D9 E8 ?? ?? ?? ?? 48 8B F8 48 85 C0 74 16");
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

            // NOTES LAST UPDATED: 5.45

            // offsets and stuff come from the beginning of case 0x2c (around line 621 in IDA)
            // if 29f8 ever changes, I'd just scan for it in old binary and find what it is in the new binary at the same spot
            // 40 55 53 57 41 54 41 55 41 56 48 8D 6C 24 ??
            var uiModule = this.Plugin.Interface.Framework.Gui.GetUIModule();
            var getListPtr = FollowPtrChain(uiModule, new[] {0, 0x110});
            var getList = Marshal.GetDelegateForFunctionPointer<GetListDelegate>(getListPtr);
            var list = getList(uiModule);
            var rciData = Marshal.ReadIntPtr(list + 0x1A0);

            unsafe {
                // offsets at sig E8 ?? ?? ?? ?? 33 C0 EB 4C
                // this is called at the end of the 2c case
                var raw = (int*) rciData;
                *(raw + 10) = actor.ActorId;
                *(raw + 11) = actor.ActorId;
                *(raw + 12) = actor.ActorId;
                *(raw + 13) = -536870912;
                *(raw + 311) = 0;
            }

            this._requestCharInfo(rciData);
        }
    }
}
