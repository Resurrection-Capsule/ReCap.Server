using System.Runtime.InteropServices;

namespace ReCap.Server.Adapters.Scripting.Native;

internal sealed class LuaStateHandle : SafeHandle
{
    public LuaStateHandle() : base(IntPtr.Zero, ownsHandle: true) { }
    public override bool IsInvalid => handle == IntPtr.Zero;
    protected override bool ReleaseHandle()
    {
        LuaNative.lua_close(handle);
        return true;
    }
}
