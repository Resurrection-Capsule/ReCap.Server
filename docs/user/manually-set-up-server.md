# Manually setting up Resurrection Capsule Server

> [!NOTE]
> This page is under construction. All content within is subject to change, and might not be accurate.

> [!WARNING]
> This method is intended for [TODO: advanced use only? dedicated Darkspore server hosting only?]
> 
> If you're just looking to play Darkspore, [use the Resurrection Capsule Hub](/docs/user/how-to-setup.md) instead.

## Additional prerequisites

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) [TODO: decide whether or not to publish the server self-contained, remove this if so]
- [Microsoft Visual C++ 2019 redistributable](https://aka.ms/vs/16/release/vc_redist.x64.exe) [TODO: old/C++ server only?]
- [Resurrection Capsule Server](https://github.com/Resurrection-Capsule/ReCap/releases/latest) 

---

## Prepare the game and the ReCap Server

1. Find `Darkspore.exe` - depending of your Darkspore version, its location will be different:
- Steam: `%programfiles(x86)%\Steam\steamapps\common\Darkspore\DarksporeBin\Darkspore.exe` by default
- Disc: `%programfiles(x86)%\Electronic Arts\Darkspore\DarksporeBin\Darkspore.exe` by default
2. Copy the `patch_darkspore_exe.exe` file from the patcher folder (from the ReCap release) to the aforementioned `DarksporeBin` folder.
3. Run the copied `patch_darkspore_exe.exe` file, and a new window should pop up.
4. Once the success message appears, you can close it, after which a new file called `Darkspore_local.exe` should appear in the `DarksporeBin` folder.

**NOTE**: From now on, you should launch `Darkspore_local.exe` instead of the original `Darkspore.exe`.

---

## Start Darkspore

1. Start the ReCap server.
2. Launch `Darkspore_local.exe`.
3. Press Play in the Darkspore launcher and wait for the login screen.
4. (ONLY REQUIRED ON FIRST RUN)
	1. Click the Register button on the login screen
	2. Create a user account with a blank password (it will only exist on your computer, no internet connection required).
5. Login to your previously-created account.