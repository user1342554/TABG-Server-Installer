using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class ReferencePanelViewModel : ObservableObject
    {
        [ObservableProperty] private string _referenceText = "";

        [RelayCommand]
        private void ShowCommands()
        {
            ReferenceText = @"TABG SERVER COMMANDS REFERENCE
================================

CITRUSLIB COMMANDS
------------------

/perm-get <player>          - Gets the permission status of the player (Perm Level: 1)
/id <name>                  - Gets the ID of a player with the given name (Perm Level: 1)
/get_pos <name>(optional)   - Queries a player's position (Perm Level: 1)
/start [time]               - Starts the countdown timer (Perm Level: 1)
/send <player> <player>     - Sends the first player to the second player (Perm Level: 2)
/send_pos <name> (x) (y) (z) - Teleports the specified player to coordinates (Perm Level: 2)
/goto_pos (x) (y) (z)      - Teleports you to the specified coordinates (Perm Level: 2)
/bring <player>             - Brings a player to you (Perm Level: 2)
                              Note: doesn't work while target is in vehicle, causes lags if in air
/give [id] [amount]         - Gives you an item with an optional amount (Perm Level: 2)
/goto <player>              - Teleports you to the specified player (Perm Level: 2)
/perm-set <player>          - SETS the permission status of the player (Perm Level: 4)
                              Note: requires restart

MOREMODS COMMANDS
-----------------

/dub                        - Doubles your inventory using KeepInventory (Perm Level: 4)
/dh                         - Drops selected grenade (only on kill, not death) (Perm Level: 4)
/adv                        - Gives you a kill and advances your weapon (Perm Level: 4)
/curse [player name] <id>   - Gives you or another player a curse (Perm Level: 4)
/cleanse [player name] <id> - Cleanses a curse from you or another player (Perm Level: 4)
/ban [player name]          - Ban a player (pardon by deleting ID from config file)
/disconnect [player name]   - Kicks someone from the server (they can immediately rejoin)

TIPS
----
- Type commands in the chat by pressing Enter
- Give yourself a higher perm level by pasting your EpicID from the Guestbook into PlayerPerms.json";
        }

        [RelayCommand]
        private void ShowItems()
        {
            ReferenceText = @"TABG ITEM IDS REFERENCE
========================

AMMO
----
0   .45 ACP             1   Big Ammo              2   Bolts
3   Money Ammo           4   Musket Ammo            6   Normal Ammo
7   Rocket Ammo          8   Shotgun Ammo           9   Small Ammo
10  Soul                 11  Taser Ammo             12  Water Ammo

CONSUMABLES
-----------
131 Bandage              132 Med Kit

ATTACHMENTS
-----------
20  0.5x Scope           21  2x Scope               22  4x Scope
23  8x Scope             24  Compensator            25  Damage Analyzer
26  Healing Barrel       27  Health Analyzer         28  Heavy Barrel
29  Laser Sight          30  Double Barrel           31  Fast Barrel
32  Accuracy Barrel      33  Fire Rate Barrel        34  Big Slow Bullet Barrel
35  Periscope Barrel     36  Periscope              38  Red Dot
39  Suppressor           40  Suppressor002

RIFLES
------
151 AK-2K47              152 AK-47                  153 AUG
154 Beam-AR              155 Burstgun               156 Famas
157 Cursed Famas         158 H1                     159 Liberating M16
160 M16                  161 MP-44                  162 SCAR-H

CROSSBOWS & SPECIAL
--------------------
163 Automatic Crossbow   164 Balloon Crossbow       169 Crossbow
171 Firework Crossbow    172 Gaussbow               173 Grappling Hook
174 Harpoon              184 The Promise

HEAVY WEAPONS
-------------
176 Liberating Mini Gun  177 Mega Gun               178 Mini Gun
179 Missile Launcher     180 Money Stack            181 Smoke Rocket Launcher
182 Rocket Launcher      185 Water Gun              203 MGL
217 Browning M2          218 M1918-BAR              219 M8
220 MG-42

PISTOLS
-------
264 Beretta 93R          265 Crossbow Pistol        266 Desert Eagle
267 Flintlock            269 Auto Revolver          270 Wind Up Pistol
271 G18c                 272 Glue Gun               273 Hand Gun
274 Hand Cannon          275 Liberating M1911       276 Luger-P08
277 M1911                278 Real Gun               279 Really Big Deagle
280 Revolver             281 Holy Revolver          283 Hardballer
284 Taser

DMRs
----
285 Beam-DMR             286 FAL                    287 Garand
288 Liberating Garand    289 M14                    290 S7
291 Winchester-1886

SHOTGUNS
--------
292 AA-12                293 Blunderbuss            294 Sawed-off Shotgun
297 Mossberg-500         298 Mossberg-5000          300 Rainmaker
301 The Arnold

SMGs
----
302 AKS-74U              303 AWPS-74U               304 Money Maker Mac
305 Glockinator          306 Lib. Thompson          307 M1a1-Thompson
308 Mac-10               309 MP-40                  310 MP5-K
311 P90                  312 PPSH-41                313 Tec-9
314 UMP-45               315 Vector                 316 Z4

SNIPERS
-------
317 AWP                  319 Barrett                320 Beam-Sniper
321 Kar98K               322 Liberating Barrett     323 Musket
325 Really Big Barrett   326 Sniper Shotgun         327 Double Shot
328 VSS

MELEE & SHIELDS
---------------
238 Ballistic Shield     241 Baton                  242 Black Katana
243 Boxing Glove         244 Cleaver                245 Crowbar
246 Crusader Sword       248 Fish                   250 Holy Sword
251 Inflatable Hammer    252 Jarl Axe               254 Katana
255 Knife                256 Rapier                 257 Riot Shield
258 Sabre                259 Pan (Shallow Pot)      260 Shield
261 Shovel               262 Viking Axe             263 Weights

SPELLBOOKS
----------
221 Blinding Light       222 Gravity Field          223 Gust
224 Healing Aura         225 Speed Aura             226 Summon Rock
227 Teleport             228 Track                  229 Fireball
230 Ice Bolt             231 Magical Missile        232 Mirage
233 Orb of Sight         234 Reveal                 235 Shockwave
236 Summon Tree

GRENADES
--------
187 BIG Healing          188 Black Hole             189 Bombardment
190 Bouncy               191 Cage                   192 Taser Cage
193 Cluster              194 Cluster Dummy          195 Dummy
196 Fire                 197 Grenade                198 Healing
199 Implosion            200 Knockback              201 BIG Knockback
202 Launch Pad           204 Orbital Tase           205 Orbital Strike
207 Shield               208 Smoke                  209 Snow Storm
210 Splinter             211 Taser Splinter         212 Stun
214 Dynamite             215 Volley                 216 Wall

BLESSINGS (Common: 42-58, Epic: 59-82, Rare: 106-128, Legendary: 83-105)
----------
42  C.Bloodlust   43  C.Cardio       44  C.Dash         45  C.Health
46  C.Ice         47  C.Jump         48  C.Poison       49  C.Recycling
50  C.Regen       51  C.Relax        52  C.Shield       53  C.Speed
54  C.Spray       55  C.Storm        56  C.The Hunt     57  C.Vampire
58  C.Weapon M.

CURSES
------
0   Recoil               1   Inaccuracy             2   Random Shooting
3   Always Shoot          4   Big                    5   Fog
6   Fragile              7   Frog                   8   Gravity
9   No Jump              10  Only Look Right        11  Rubber Banding
12  Slow Gun             13  Slow Reload            14  Slow Bullets
15  Slow Medicine        16  Small Jump             17  Small Mag
18  Varying Sensitivity";
        }

        [RelayCommand]
        private void ShowLoadouts()
        {
            ReferenceText = @"LOADOUT PRESETS REFERENCE
=========================

FORMAT: (Name):(percentage)%(ItemID):(Qty),(ItemID):(Qty),.../(next loadout)
Example: Sniper:10%317:1,248:1,132:3,1:255,232:1,40:1,22:1/

SPECIAL PREFIXES:
- GunGame/       Advances loadout on kill (progressive weapons)
- ReverseGunGame/ Advances loadout on death
- KeepInventory/ Items persist through death (for scavenge mode)

──────────────────────────────────────────

DEATHMATCH (35 loadouts, 10% weight each)
Paste after 'Loadouts=' in TheStarterPack.txt:

AWPS:10%303:1,1:255,53:1/AK2K:10%151:1,6:255,6:255/Mossberg5K:10%298:1,8:255,70:1,53:1,54:1,259:1/Katana:10%254:1,119:1/BurstGun:10%155:1,6:255,6:255,6:255/WindUps:10%225:1,270:1,270:1,9:255,9:255/Big and Melee:10%261:1,89:1,125:1/Money Makers:10%234:1,180:1,304:1,304:1,53:1,55:1,9:255,9:255,3:255,3:255/Sniper Shotgun:10%225:1,326:1,8:255,112:1,99:1,57:1,28:1/M16 and Gauss:10%160:1,6:255,6:255,172:1,2:50,118:1,29:1,38:1/Flint and UMP:10%267:1,4:50,314:1,9:255,9:145,117:1,52:1,38:1,29:1/Water:10%76:1,185:1,126:1,123:1,12:255,12:255,12:255,12:255,12:255,12:255,12:255,12:255,12:255,255:1/Tank with boxing:10%89:1,243:1,238:1,227:1/Revolvers and Rock:10%226:1,281:1,281:1,6:255,121:1,50:1/AWP Mirage and Tec:10%317:1,1:200,232:1,313:1,9:255,9:45,22:1,40:1,52:1,117:1/STG and Missile:10%161:1,6:255,6:145,231:1,24:1,38:1,118:1,45:1/Scar and Ballon:10%22:1,24:1,118:1,162:1,6:255,6:145,2:30,164:1/FAL G18C and Ballistic:10%286:1,6:255,6:145,271:1,9:255,238:1,24:1,38:1,53:1,43:1/MGL:10%203:1,1:255,1:255,122:1,117:1,53:1/MP5 and Kar:10%310:1,9:255,1:100,321:1,118:1,47:1,29:1,38:1,38:1/M14 and Blunder:10%289:1,6:255,293:1,4:50,38:1,24:1,53:1,108:1/Crossbow and MP44:10%163:1,2:150,161:1,6:250,126:1,50:1,38:1,38:1/Thomson and Sawed:10%294:1,8:100,307:1,9:255,9:145,31:1,38:1,53:1,43:1/Rock and Glue:10%272:1,9:255,9:255,226:1,73:1,99:1,111:1/Speed and Pot:10%75:1,75:1,71:1,259:1,232:1/BeamAR and Tree:10%154:1,9:255,9:255,236:1,108:1/Big Deagle:10%279:1,6:255,53:1,263:1,223:1/MiniGun and Knockback:10%178:1,118:1,6:255,6:255,235:1/Hammer and Famas:10%157:1,6:255,38:1,251:1,118:1/Barret and Baller:10%53:1,38:1,1:255,9:255,283:1,319:1,39:1/Bar and Garand:10%218:1,6:255,1:255,118:1,38:1,22:1,287:1/Winchester and Shovel:10%1:255,291:1,38:1,22:1,46:1,121:1,261:1/Barettas and Hubert:10%248:1,9:255,9:255,264:1,264:1,264:1,264:1,118:1,53:1/Tazers and Cleaver:10%244:1,284:1,11:50,53:1,57:1,47:1/AA12:10%292:1,8:255,278:1,6:121,115:1,53:1/

LoadoutCurses for plugin config: 14,14,17,1/1,0,14/17,13/6/12/13/4,9/6

──────────────────────────────────────────

GUN GAME - 35 Kills (35 progressive loadouts)
Paste after 'Loadouts=' in TheStarterPack.txt:

GunGame/AK2K47:100%151:1,6:255,6:255,38:1/Crossbow:100%169:1,2:255,38:1/Barret:100%322:1,1:255,38:1/Mossberg5K:100%298:1,8:255/Glockinator:100%305:1,9:255,9:255/AA12:100%292:1,8:255,38:1/Burstgun:100%155:1,6:255,6:255,38:1/Missile:100%231:1/Blunder:100%293:1,4:255/H1:100%158:1,6:255,6:255,38:1/AutoCrossbow:100%163:1,2:255,38:1/Rainmaker:100%300:1,8:255,38:1/UMP:100%314:1,9:255,38:1/MG42:100%220:1,6:255,6:255,38:1/STG:100%161:1,6:255,38:1/AUG:100%153:1,6:255/M14:100%289:1,6:255,38:1/VSS:100%328:1,9:255,9:255,38:1/Garand:100%287:1,1:255,38:1/Minigun:100%178:1,6:255,6:255,6:255,6:255/MP40:100%309:1,9:255,38:1/M16:100%160:1,6:255,38:1/Kar98:100%321:1,1:255,38:1/CursedFamas:100%157:1,6:255,38:1/BeamAR:100%154:1,9:255,9:255/Z4:100%316:1,9:255,38:1/Deagle:100%279:1,6:255/WindUp:100%270:1,9:255/Flintlock:100%267:1,4:255/Beretta:100%264:1,9:255/Tec9:100%313:1,9:255,9:255/Holy Revolver:100%281:1,6:255/Mac:100%304:1,9:255/Luger:100%276:1,9:255/Fish:100%248:1/Money:100%180:1/

──────────────────────────────────────────

GUN GAME - 25 Kills (25 progressive loadouts)
Paste after 'Loadouts=' in TheStarterPack.txt:

GunGame/Minigun:100%178:1,6:255,6:255,6:255,6:255/Crossbow:100%169:1,2:255,38:1/Mossberg5K:100%298:1,8:255/Glockinator:100%305:1,9:255,9:255/AA12:100%292:1,8:255,38:1/Burstgun:100%155:1,6:255,6:255,38:1/Blunder:100%293:1,4:255/CursedFamas:100%157:1,6:255,38:1/AutoCrossbow:100%163:1,2:255,38:1/Rainmaker:100%300:1,8:255,38:1/UMP:100%314:1,9:255,38:1/MG42:100%220:1,6:255,6:255,38:1/M14:100%289:1,6:255,38:1/VSS:100%328:1,9:255,9:255,38:1/Garand:100%287:1,1:255,38:1/MP40:100%309:1,9:255,38:1/M16:100%160:1,6:255,38:1/Kar98:100%321:1,1:255,38:1/BeamAR:100%154:1,9:255,9:255/Deagle:100%279:1,6:255/Flintlock:100%267:1,4:255/Tec9:100%313:1,9:255,9:255/Holy Revolver:100%281:1,6:255/Luger:100%276:1,9:255/Pan:100%259:1/Money:100%180:1/

──────────────────────────────────────────

SCAVENGE (KeepInventory mode)
Paste after 'Loadouts=' in TheStarterPack.txt:

KeepInventory/Ammo and Medkits:100%1:50,2:80,4:50,6:255,6:245,8:100,9:255,9:245,132:5/";
        }

        [RelayCommand]
        private void ShowSpawns()
        {
            ReferenceText = @"SPAWN LOCATIONS REFERENCE
==========================

SINGLE SPAWN POINTS (for lobby CustomSpawnPoint in TheStarterPack.txt)
----------------------------------------------------------------------
Tall Work           -313,170,-530       WW2 Town            -465,175,-481
Containers          -169,125,159        Finland             -573,130,301
Tall Work (alt)     -422,130,82         Circle Town         141,140,-375
Chaos               -13,140,-523        Big Power           723,125,-689
Small Power         439,130,-274        Small Power Houses  478,125,-446
Oasis               631,126,487         Middle Of Sand      454,140,313
Finland Cabin       -492,120,333        City Bridge         -787,125,69
Fields by POI       -23,140,338         H                   -162,140,-214
Snow                -534,170,-311

MULTI-SPAWN AREAS (match spawns - 2D coordinates separated by semicolons)
These go into the match spawn configuration.
Format: x,z;x,z;x,z;...  (lobby spawn on next line as x,y,z)
--------------------------------------------------------------------------

CITY (Quick Match)     -578,-17;-611,32;-696,40;-728,-11;-675,-98;-626,-124;-578,-100
  Lobby: -674,125,-8

CITY NORMAL            -619,-20;-651,-33;-694,-40;-702,2;-664,18;-640,-1;-656,-21;-633,-53;-660,-77;-619,-80;-578,-17;-611,32;-696,40;-728,-11;-675,-98;-626,-124;-578,-100
  Lobby: -674,125,-8

CRAPPY CASTLE          -345,390;-363,411;-382,421;-419,408;-420,366;-400,354;-371,364;-400,408
  Lobby: -392,130,400

AREA 64 (Quick Match)  427,544;458,510;457,424;390,432;345,439;309,467;356,529
  Lobby: 385,135,468

AREA 64 NORMAL         427,544;458,510;457,424;390,432;345,439;309,467;356,529;370,494;350,504;329,490;353,464;395,461;433,463;396,516;433,511;392,501;417,521;428,426;440,448
  Lobby: 385,135,468

WESTERN                710,650;663,643;640,642;615,629;617,601;618,572;654,550;663,543;688,573;685,609;666,619;642,629;619,587;602,652;594,620,675,602;707,605;709,540
  Lobby: 659,125,600

NORMANDIE              -531,604;-521,662;-481,638;-431,653;-396,594;-383,547;-410,537;-414,501;-458,501;-490,477;-520,514;-556,520;-559,492;-585,576;-362,579;-379,631;-579,540
  Lobby: -411,120,517

SANDCASTLE             640,7:654-22;674,-23;691,-34;726,-23;728,0;734,27;705,41;672,39;656,37
  Lobby: 685,140,8

FIELDS BY H            -191,-165;-201,-232;-248,-261;-232,-225;-239,-176;-294,-200;-287,-137;-240,-102;-238,-138;-230,-165;-316,-217
  Lobby: -254,140,-128

ACTUAL CASTLE          -702,502;-725,522;-713,572;-710,536;-697,568;-683,543;-692,549;-691,523;-667,532;-671,550;-656,527;-688,568
  Lobby: -689,145,510

BIG WORK               363,-617;341,-638;358,-711;334,-709;313,-683;380,-663;351,-736;288,-709;342,-687;278,-674;284,-626;322,-654
  Lobby: 366,130,-646

POINT OF IMPACT        -41,540;-5,550;-1,589;-31,625;-47,600;-72,634;-90,600;-129,602;-92,559;-125,541;-71,519;-50,559;27,554
  Lobby: -74,125,611

INDUSTRY               17,-51;68,-7;98,28;111,-13;77,-48;45,-92;129,-97;180,-115;191,-82;164,-47;211,-31;191,12;157,-10;122,-141;62,-6
  Lobby: 98,125,-95

LONG WALL              420,50;447,66;504,96;494,119;461,111;434,105;475,50;450,22;467,80;491,153;453,148;431,133;396,94;526,96;518,55;484,41;446,44
  Lobby: 433,140,73

SNOW CASTLE            -367,-728;-344,-760;-330,-796;-358,-817;-382,-790;-413,-808;-401,-774;-403,-793;-383,-753;-419,-744;-373,-778;-360,-784
  Lobby: -385,175,-771

PYRAMID                632,240;633,197;674,200;683,257;642,278;622,300;584,271;586,195;597,228
  Lobby: 636,125,240

CHAOS                  -32,129,-645;-70,133,-635;-71,141,-594;-91,137,-558;-63,131,-513;-24,132,-494;16,137,-515;28,132,-557,10,124,-600";
        }

        [RelayCommand]
        private void ShowMatchSettings()
        {
            ReferenceText = @"MATCH SETTINGS GUIDE
=====================

TheStarterPack.txt is the main file for configuring match mechanics.
Edit it in the server root directory. Here are the key settings:

LOADOUT FORMAT
--------------
Loadouts=(Name):(percentage 1-100)%(ItemID):(Quantity),(ItemID):(Quantity),.../(next loadout)
Example: Sniper:10%317:1,248:1,132:3,1:255,232:1,40:1,22:1/

Max quantity per cell: 255
Each loadout ends with /
Multiple items separated by commas

LOADOUT CHAINS (GunGame)
------------------------
Type 'Loadouts=GunGame/...' to advance the loadout on each kill.
Match your KillsToWin to the number of loadouts.
Use ForceKillAtStart=true!

REVERSE GUN GAME
-----------------
Type 'Loadouts=ReverseGunGame/...' to advance on death instead.
Needs 2 empty loadouts at the beginning to work correctly.

KEEP INVENTORY (Scavenge)
--------------------------
Type 'Loadouts=KeepInventory/...' to keep items after death.
Great for scavenge servers. Items stack with each death.
Do NOT combine with GunGame.

RING SETTINGS
-------------
In game_settings.txt:
  RingSizes=200, 200, 200
  RingSpeeds=35, 35, 35

Both are arrays of three values. RingSizes must match the MatchCore ring size.
For deathmatch-style (no ring closing): use large sizes with tiny speeds like
  RingSizes=244, 244, 244
  RingSpeeds=0.001, 50, 0.001

LOADOUT CURSES
--------------
Set 'LoadoutCurses = ' in the plugin config.
Format: curse IDs separated by commas, loadouts separated by /
Example: 2,4/8,14 = curses 2+4 on loadout 1, curses 8+14 on loadout 2
Loadouts after the final / receive no curses.
Put cursed loadouts at the beginning of 'Loadouts='.

OTHER SETTINGS
--------------
WinCondition=Default|KillsToWin|Debug
ForceKillAtStart=true|false     - Kill players out of trucks at start
DropItemsOnDeath=true|false     - Drop items on death
HealOnKill=true|false           - Heal on kill
HealOnKillAmount=20             - HP percentage healed on kill
CanGoDown=true|false            - Team members go down vs instant respawn
SpelldropEnabled=true|false     - Enable spell drops from sky
PreMatchTimeout=15              - Pre-lobby timeout in minutes
PeriMatchTimeout=15             - Match timeout in minutes

SPAWN POINTS
------------
ValidSpawnPoints=0-5 (predefined) or 6 (custom)
  0=Tall Work, 1=Circle Town, 2=Western, 3=Containers, 4=Chaos, 5=Factory
CustomSpawnPoint=x,y,z          - Used when ValidSpawnPoints contains 6

DROP WEAPONS
------------
Enable/disable weapon dropping via the 'StarterpackFixes' config boolean.

GAME MODE INDEXES (for GameMode setting)
-----------------------------------------
0  No text                      1  Medieval (Crossbows only)
2  Arena (Shotguns+Speed)       3  Mayhem (Grenades only)
4  Demolition (MLG only)        5  Big Guns (Mini+Mega guns)
6  Ninja Musket Duel            7  Napoleonic
8  Trick Shooting               9  Back to Basics (Pistols)
10 CQC (Shotguns)              11 Sharpshooter (Snipers)
12 Sorcerers Skirmish           13 Battle of Titans";
        }
    }
}
