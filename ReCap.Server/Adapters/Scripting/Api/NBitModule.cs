using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NBitModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nBit",
            ("Or", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&BitOr),
            ("And", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&BitAnd),
            ("Xor", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&BitXor),
            ("Not", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&BitNot),
            ("LShift", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&BitLShift),
            ("RShift", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&BitRShift),
            ("Mask", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&BitMask));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int BitOr(nint L)
    {
        try
        {
            var top = LuaNative.lua_gettop(L);
            uint acc = 0;
            for (var i = 1; i <= top; i++)
                acc |= (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, i));
            LuaNative.lua_pushnumber(L, (float)acc);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int BitAnd(nint L)
    {
        try
        {
            var top = LuaNative.lua_gettop(L);
            if (top == 0)
            {
                LuaNative.lua_pushnumber(L, 0f);
                return 1;
            }
            var acc = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 1));
            for (var i = 2; i <= top; i++)
                acc &= (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, i));
            LuaNative.lua_pushnumber(L, (float)acc);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int BitXor(nint L)
    {
        try
        {
            var top = LuaNative.lua_gettop(L);
            uint acc = 0;
            for (var i = 1; i <= top; i++)
                acc ^= (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, i));
            LuaNative.lua_pushnumber(L, (float)acc);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int BitNot(nint L)
    {
        try
        {
            var a = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 1));
            LuaNative.lua_pushnumber(L, (float)(~a));
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int BitLShift(nint L)
    {
        try
        {
            var a = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var b = (int)System.Math.Round((double)LuaNative.lua_tonumber(L, 2));
            LuaNative.lua_pushnumber(L, (float)(a << b));
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int BitRShift(nint L)
    {
        try
        {
            var a = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var b = (int)System.Math.Round((double)LuaNative.lua_tonumber(L, 2));
            LuaNative.lua_pushnumber(L, (float)(a >> b));
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    // Ghidra nBit::Mask@0x009fa170: value & ~f1 & ~f2 ... (clears bits; NOT And). Variadic.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int BitMask(nint L)
    {
        try
        {
            var top = LuaNative.lua_gettop(L);
            uint acc = top >= 1 ? (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 1)) : 0u;
            for (var i = 2; i <= top; i++)
                acc &= ~(uint)System.Math.Round((double)LuaNative.lua_tonumber(L, i));
            LuaNative.lua_pushnumber(L, (float)acc);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }
}
