using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NGameObjectModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nGameObject",
            ("GetPosition", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetPosition),
            ("GetHitPoints", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetHitPoints),
            ("GetMaxHitPoints", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMaxHitPoints),
            ("IsAlive", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsAlive),
            ("GetTeam", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTeam),
            ("GetTargetID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetID),
            ("GetCenterPoint", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetCenterPoint),
            ("GetOrientation", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetOrientation),
            ("GetFacing", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetFacing),
            ("GetFootprintRadius", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetFootprintRadius),
            ("ValidateHostileTarget", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ValidateHostileTarget),
            ("ValidateFriendlyTarget", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ValidateFriendlyTarget),
            ("SetAnimationState", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetAnimationState));
    }

    // Retail contracts (Ghidra 2026-06-06): GetCenterPoint @0x009fb7f0 (3 floats, lua error on
    // missing); GetOrientation @0x00a02bb0 (4 floats XYZW); GetFacing @0x009fb9a0 (3 floats,
    // forward derived from orientation); GetFootprintRadius @0x009fbb20 (1 float, 0 on null);
    // ValidateHostileTarget @0x009fd3c0 / ValidateFriendlyTarget @0x009fd460 (2-3 args, 1 bool);
    // SetAnimationState @0x009fc000 (2 args, state id string→FNV or number, 0 returns,
    // NETWORK broadcast — server sends 0xA5 to all clients).

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetCenterPoint(nint L)
    {
        float x = 0, y = 0, z = 0;
        bool found = false;
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
                found = bridge is not null && bridge.TryGetPosition(ReadId(L), out x, out y, out z);
            }
        }
        catch
        {
            found = false;
        }
        if (!found)
        {
            LuaNative.lua_pushstring(L, "Could not find object!");
            return LuaNative.lua_error(L);
        }
        LuaNative.lua_pushnumber(L, x);
        LuaNative.lua_pushnumber(L, y);
        LuaNative.lua_pushnumber(L, z);
        return 3;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetOrientation(nint L)
    {
        float x = 0, y = 0, z = 0, w = 1;
        bool found = false;
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
                found = bridge is not null && bridge.TryGetOrientation(ReadId(L), out x, out y, out z, out w);
            }
        }
        catch
        {
            found = false;
        }
        if (!found)
        {
            LuaNative.lua_pushstring(L, "Could not find object!");
            return LuaNative.lua_error(L);
        }
        LuaNative.lua_pushnumber(L, x);
        LuaNative.lua_pushnumber(L, y);
        LuaNative.lua_pushnumber(L, z);
        LuaNative.lua_pushnumber(L, w);
        return 4;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetFacing(nint L)
    {
        float x = 0, y = 0, z = 0, w = 1;
        bool found = false;
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
                found = bridge is not null && bridge.TryGetOrientation(ReadId(L), out x, out y, out z, out w);
            }
        }
        catch
        {
            found = false;
        }
        if (!found)
        {
            LuaNative.lua_pushstring(L, "Could not find object!");
            return LuaNative.lua_error(L);
        }
        // Forward = orientation applied to the +Y unit axis (Spore-engine convention, Z-up).
        // UNCONFIRMED axis choice — only affects FX direction until verified on the wire.
        var forward = System.Numerics.Vector3.Transform(
            new System.Numerics.Vector3(0f, 1f, 0f),
            new System.Numerics.Quaternion(x, y, z, w));
        LuaNative.lua_pushnumber(L, forward.X);
        LuaNative.lua_pushnumber(L, forward.Y);
        LuaNative.lua_pushnumber(L, forward.Z);
        return 3;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetFootprintRadius(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var exists = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && bridge is not null && bridge.ObjectExists(ReadId(L));
            // Noun physics footprint not parsed yet; 0.5 = generic creature radius placeholder.
            LuaNative.lua_pushnumber(L, exists ? 0.5f : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ValidateHostileTarget(nint L) => ValidateTarget(L, hostile: true);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ValidateFriendlyTarget(nint L) => ValidateTarget(L, hostile: false);

    private static int ValidateTarget(nint L, bool hostile)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER || LuaNative.lua_type(L, 2) != LuaNative.LUA_TNUMBER)
            {
                LuaNative.lua_pushboolean(L, 0);
                return 1;
            }
            var sourceId = ReadId(L);
            var targetId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2));
            var allowDead = LuaNative.lua_gettop(L) >= 3 && LuaNative.lua_toboolean(L, 3) != 0;
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;

            var valid = bridge is not null
                && bridge.ObjectExists(sourceId)
                && bridge.ObjectExists(targetId)
                && (allowDead || bridge.GetHitPoints(targetId) > 0f)
                && (hostile
                    ? bridge.GetTeam(sourceId) != bridge.GetTeam(targetId)
                    : bridge.GetTeam(sourceId) == bridge.GetTeam(targetId));
            LuaNative.lua_pushboolean(L, valid ? 1 : 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetAnimationState(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) return 0;
            var objectId = ReadId(L);
            uint state = LuaNative.lua_type(L, 2) switch
            {
                3 => (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2)),
                4 => AssetData.Parser.WireHash.Fnv1a(LuaNative.ToManagedString(L, 2) ?? string.Empty),
                _ => 0u,
            };
            if (state != 0)
                ScriptContextRegistry.Get(L)?.GameBridge?.BroadcastAnimationState(objectId, state);
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static uint ReadId(nint L) =>
        (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetPosition(nint L)
    {
        float x = 0, y = 0, z = 0;
        bool found = false;
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var id = ReadId(L);
                var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
                found = bridge is not null && bridge.TryGetPosition(id, out x, out y, out z);
            }
        }
        catch
        {
            found = false;
        }
        if (!found)
        {
            LuaNative.lua_pushstring(L, "Could not find object!");
            return LuaNative.lua_error(L);
        }
        LuaNative.lua_pushnumber(L, x);
        LuaNative.lua_pushnumber(L, y);
        LuaNative.lua_pushnumber(L, z);
        return 3;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetHitPoints(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            LuaNative.lua_pushnumber(L, bridge?.GetHitPoints(id) ?? 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMaxHitPoints(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            LuaNative.lua_pushnumber(L, bridge?.GetMaxHitPoints(id) ?? 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsAlive(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushboolean(L, 0); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var hp = bridge?.GetHitPoints(id) ?? 0f;
            LuaNative.lua_pushboolean(L, hp > 0f ? 1 : 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTeam(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) return 0;
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is null || !bridge.ObjectExists(id)) return 0;
            LuaNative.lua_pushnumber(L, (float)bridge.GetTeam(id));
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTargetID(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            LuaNative.lua_pushnumber(L, bridge is not null ? (float)bridge.GetTargetId(id) : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }
}
