# COM3D2.ShortMenuVanillaDatabase
A completely standalone compliment to ShortMenuLoader(SML), SMVD replaces a function in unmanaged code that is responsible for partially loading menu files in arc files. The original is very slow, this one isn't.

# Highlights #

- Faster Cold Start Times
- Faster Edit Mode Loads
- Minor Multi-threading
- Optimized Code
- Self Correcting Smart Cache
- Pairs perfectly with SML.
- BepinEx Only

# Technicals #

the `cm3d2.dll` has a small database that loads only the data in menu files needed to show you an icon, description, name etc, of a menu file in edit mode. This database is only concerned with loading menu files inside of arc files, but it's slow. Very slow... Taking up to 5 minutes sometimes to load everything. Until this database finishes loading, edit mode will stop and wait for it, effectively stopping you from entering edit mode for minutes at a time. This huge slow down was discovered with SML being so fast it has to wait for this database. That's why this is a compliment to SML. You can use it standalone perfectly fine but you'll get the full speed buffs with SML.

You may be wondering, "but sometimes I can enter edit mode immediately!". That's because the database begins loading as soon as you hit the title screen and loads in the background. If you play for 5 minutes and then enter edit mode, you won't notice a difference. If you wait for 5 minutes in the title screen and then enter edit mode, it'll seem instant. But that's just because you gave the database time to finish. Furthermore, the database only needs to load once per game run. So once it's done loading, it's done loading until you restart your game.

# How it Works #

SMVD intercepts the calls for unmanaged code and instead redirects everything to SMVD, the `cm3d2.dll` is never called and this not only helps with marshalling overhead but puts a bit more code and control in the hands of modders, not to mention it being faster... The plugin then emulates what the unmanaged code seems to do, though we have no exact way of knowing since we can't see the unmanaged code, and provides the same output. As far as the game is concerned, it's asking for something and receiving the same product in exchange that it normally would. Maybe slightly different.

On the very first load, SMVD will load all relevant arc files that contain menu files and will create it's database, this can take less than a minute on average. Once the database is complete, it saves a smart cache, very similar to the one in SML. Once the smart cache is built, database loads are nearly instant, less than 10 seconds in the worst cases.

# Usage #

1. Place the provided DLL from the release section into your `BepinEx/plugins` folder.
2. Place the dll from https://github.com/JustAGuest4168/CM3D2.Toolkit/releases into the same directory.
3. ???
4. Profit, the first run will build a cache. Use with SML for best results!

Please note, SMVD was built against version `COM 2.3.1` and `BepinEx 5.4.15` and you should be using these for best results. Version 2.5/3.0 of COM3D2 isn't supported since I don't know if it'll work and don't care to support it.
