using System.Collections.Generic;
using System.Linq;

namespace TabgInstaller.Core.Model
{
    public record GameItem(int Id, string Name, string Category);

    public record SpawnLocation(
        string Name,
        string LobbySpawn,       // x,y,z
        string MatchSpawns,      // x,z;x,z;...
        int RingSize,
        string RingCenter        // x,y,z (for TheStarterPack ring)
    );

    public record CurseInfo(int Id, string Name);

    public static class ItemDatabase
    {
        public static readonly IReadOnlyList<GameItem> AllItems = BuildItems();
        public static readonly IReadOnlyList<SpawnLocation> SpawnLocations = BuildSpawns();
        public static readonly IReadOnlyList<CurseInfo> Curses = BuildCurses();
        public static readonly IReadOnlyList<string> Categories = AllItems.Select(i => i.Category).Distinct().ToList();

        public static IEnumerable<GameItem> ByCategory(string category) =>
            AllItems.Where(i => i.Category == category);

        public static GameItem? ById(int id) =>
            AllItems.FirstOrDefault(i => i.Id == id);

        public static string? GetNameById(int id) =>
            ById(id)?.Name;

        private static List<GameItem> BuildItems()
        {
            return new List<GameItem>
            {
                // ── Ammo ──
                new(0, ".45 ACP", "Ammo"), new(1, "Big Ammo", "Ammo"), new(2, "Bolts", "Ammo"),
                new(3, "Money Ammo", "Ammo"), new(4, "Musket Ammo", "Ammo"), new(6, "Normal Ammo", "Ammo"),
                new(7, "Rocket Ammo", "Ammo"), new(8, "Shotgun Ammo", "Ammo"), new(9, "Small Ammo", "Ammo"),
                new(10, "Soul", "Ammo"), new(11, "Taser Ammo", "Ammo"), new(12, "Water Ammo", "Ammo"),

                // ── Consumables ──
                new(131, "Bandage", "Consumables"), new(132, "Med Kit", "Consumables"),

                // ── Attachments ──
                new(20, "0.5x Scope", "Attachments"), new(21, "2x Scope", "Attachments"),
                new(22, "4x Scope", "Attachments"), new(23, "8x Scope", "Attachments"),
                new(24, "Compensator", "Attachments"), new(25, "Damage Analyzer", "Attachments"),
                new(26, "Healing Barrel", "Attachments"), new(27, "Health Analyzer", "Attachments"),
                new(28, "Heavy Barrel", "Attachments"), new(29, "Laser Sight", "Attachments"),
                new(30, "Double Barrel", "Attachments"), new(31, "Fast Barrel", "Attachments"),
                new(32, "Accuracy Barrel", "Attachments"), new(33, "Fire Rate Barrel", "Attachments"),
                new(34, "Big Slow Bullet Barrel", "Attachments"), new(35, "Periscope Barrel", "Attachments"),
                new(36, "Periscope", "Attachments"), new(38, "Red Dot", "Attachments"),
                new(39, "Suppressor", "Attachments"), new(40, "Suppressor 2", "Attachments"),

                // ── Rifles ──
                new(151, "AK-2K47", "Rifles"), new(152, "AK-47", "Rifles"), new(153, "AUG", "Rifles"),
                new(154, "Beam-AR", "Rifles"), new(155, "Burstgun", "Rifles"), new(156, "Famas", "Rifles"),
                new(157, "Cursed Famas", "Rifles"), new(158, "H1", "Rifles"), new(159, "Liberating M16", "Rifles"),
                new(160, "M16", "Rifles"), new(161, "MP-44", "Rifles"), new(162, "SCAR-H", "Rifles"),

                // ── DMRs ──
                new(285, "Beam-DMR", "DMRs"), new(286, "FAL", "DMRs"), new(287, "Garand", "DMRs"),
                new(288, "Liberating Garand", "DMRs"), new(289, "M14", "DMRs"), new(290, "S7", "DMRs"),
                new(291, "Winchester-1886", "DMRs"),

                // ── SMGs ──
                new(302, "AKS-74U", "SMGs"), new(303, "AWPS-74U", "SMGs"), new(304, "Money Maker Mac", "SMGs"),
                new(305, "Glockinator", "SMGs"), new(306, "Lib. Thompson", "SMGs"), new(307, "M1a1-Thompson", "SMGs"),
                new(308, "Mac-10", "SMGs"), new(309, "MP-40", "SMGs"), new(310, "MP5-K", "SMGs"),
                new(311, "P90", "SMGs"), new(312, "PPSH-41", "SMGs"), new(313, "Tec-9", "SMGs"),
                new(314, "UMP-45", "SMGs"), new(315, "Vector", "SMGs"), new(316, "Z4", "SMGs"),

                // ── Snipers ──
                new(317, "AWP", "Snipers"), new(319, "Barrett", "Snipers"), new(320, "Beam-Sniper", "Snipers"),
                new(321, "Kar98K", "Snipers"), new(322, "Liberating Barrett", "Snipers"),
                new(323, "Musket", "Snipers"), new(325, "Really Big Barrett", "Snipers"),
                new(326, "Sniper Shotgun", "Snipers"), new(327, "Double Shot", "Snipers"),
                new(328, "VSS", "Snipers"),

                // ── Shotguns ──
                new(292, "AA-12", "Shotguns"), new(293, "Blunderbuss", "Shotguns"),
                new(294, "Sawed-off Shotgun", "Shotguns"), new(297, "Mossberg-500", "Shotguns"),
                new(298, "Mossberg-5000", "Shotguns"), new(300, "Rainmaker", "Shotguns"),
                new(301, "The Arnold", "Shotguns"),

                // ── Pistols ──
                new(264, "Beretta 93R", "Pistols"), new(265, "Crossbow Pistol", "Pistols"),
                new(266, "Desert Eagle", "Pistols"), new(267, "Flintlock", "Pistols"),
                new(269, "Auto Revolver", "Pistols"), new(270, "Wind Up Pistol", "Pistols"),
                new(271, "G18c", "Pistols"), new(272, "Glue Gun", "Pistols"),
                new(273, "Hand Gun", "Pistols"), new(274, "Hand Cannon", "Pistols"),
                new(275, "Liberating M1911", "Pistols"), new(276, "Luger-P08", "Pistols"),
                new(277, "M1911", "Pistols"), new(278, "Real Gun", "Pistols"),
                new(279, "Really Big Deagle", "Pistols"), new(280, "Revolver", "Pistols"),
                new(281, "Holy Revolver", "Pistols"), new(283, "Hardballer", "Pistols"),
                new(284, "Taser", "Pistols"),

                // ── Heavy Weapons ──
                new(176, "Liberating Mini Gun", "Heavy"), new(177, "Mega Gun", "Heavy"),
                new(178, "Mini Gun", "Heavy"), new(179, "Missile Launcher", "Heavy"),
                new(180, "Money Stack", "Heavy"), new(181, "Smoke Rocket Launcher", "Heavy"),
                new(182, "Rocket Launcher", "Heavy"), new(185, "Water Gun", "Heavy"),
                new(203, "MGL", "Heavy"), new(217, "Browning M2", "Heavy"),
                new(218, "M1918-BAR", "Heavy"), new(219, "M8", "Heavy"), new(220, "MG-42", "Heavy"),

                // ── Crossbows ──
                new(163, "Auto Crossbow", "Crossbows"), new(164, "Balloon Crossbow", "Crossbows"),
                new(169, "Crossbow", "Crossbows"), new(171, "Firework Crossbow", "Crossbows"),
                new(172, "Gaussbow", "Crossbows"), new(173, "Grappling Hook", "Crossbows"),
                new(174, "Harpoon", "Crossbows"), new(184, "The Promise", "Crossbows"),

                // ── Melee & Shields ──
                new(238, "Ballistic Shield", "Melee"), new(241, "Baton", "Melee"),
                new(242, "Black Katana", "Melee"), new(243, "Boxing Glove", "Melee"),
                new(244, "Cleaver", "Melee"), new(245, "Crowbar", "Melee"),
                new(246, "Crusader Sword", "Melee"), new(248, "Fish", "Melee"),
                new(250, "Holy Sword", "Melee"), new(251, "Inflatable Hammer", "Melee"),
                new(252, "Jarl Axe", "Melee"), new(254, "Katana", "Melee"),
                new(255, "Knife", "Melee"), new(256, "Rapier", "Melee"),
                new(257, "Riot Shield", "Melee"), new(258, "Sabre", "Melee"),
                new(259, "Pan", "Melee"), new(260, "Shield", "Melee"),
                new(261, "Shovel", "Melee"), new(262, "Viking Axe", "Melee"),
                new(263, "Weights", "Melee"),

                // ── Spellbooks ──
                new(221, "Blinding Light", "Spellbooks"), new(222, "Gravity Field", "Spellbooks"),
                new(223, "Gust", "Spellbooks"), new(224, "Healing Aura", "Spellbooks"),
                new(225, "Speed Aura", "Spellbooks"), new(226, "Summon Rock", "Spellbooks"),
                new(227, "Teleport", "Spellbooks"), new(228, "Track", "Spellbooks"),
                new(229, "Fireball", "Spellbooks"), new(230, "Ice Bolt", "Spellbooks"),
                new(231, "Magical Missile", "Spellbooks"), new(232, "Mirage", "Spellbooks"),
                new(233, "Orb of Sight", "Spellbooks"), new(234, "Reveal", "Spellbooks"),
                new(235, "Shockwave", "Spellbooks"), new(236, "Summon Tree", "Spellbooks"),

                // ── Grenades ──
                new(187, "BIG Healing", "Grenades"), new(188, "Black Hole", "Grenades"),
                new(189, "Bombardment", "Grenades"), new(190, "Bouncy", "Grenades"),
                new(191, "Cage", "Grenades"), new(192, "Taser Cage", "Grenades"),
                new(193, "Cluster", "Grenades"), new(195, "Dummy", "Grenades"),
                new(196, "Fire", "Grenades"), new(197, "Grenade", "Grenades"),
                new(198, "Healing", "Grenades"), new(199, "Implosion", "Grenades"),
                new(200, "Knockback", "Grenades"), new(201, "BIG Knockback", "Grenades"),
                new(202, "Launch Pad", "Grenades"), new(204, "Orbital Tase", "Grenades"),
                new(205, "Orbital Strike", "Grenades"), new(207, "Shield", "Grenades"),
                new(208, "Smoke", "Grenades"), new(209, "Snow Storm", "Grenades"),
                new(210, "Splinter", "Grenades"), new(211, "Taser Splinter", "Grenades"),
                new(212, "Stun", "Grenades"), new(214, "Dynamite", "Grenades"),
                new(215, "Volley", "Grenades"), new(216, "Wall", "Grenades"),

                // ── Blessings (Common) ──
                new(42, "C. Bloodlust", "Blessings"), new(43, "C. Cardio", "Blessings"),
                new(44, "C. Dash", "Blessings"), new(45, "C. Health", "Blessings"),
                new(46, "C. Ice", "Blessings"), new(47, "C. Jump", "Blessings"),
                new(48, "C. Poison", "Blessings"), new(49, "C. Recycling", "Blessings"),
                new(50, "C. Regeneration", "Blessings"), new(51, "C. Relax", "Blessings"),
                new(52, "C. Shield", "Blessings"), new(53, "C. Speed", "Blessings"),
                new(54, "C. Spray", "Blessings"), new(55, "C. Storm", "Blessings"),
                new(56, "C. The Hunt", "Blessings"), new(57, "C. Vampire", "Blessings"),
                new(58, "C. Weapon Mastery", "Blessings"),
                // Rare
                new(106, "R. Airstrike", "Blessings"), new(107, "R. Bloodlust", "Blessings"),
                new(108, "R. Cardio", "Blessings"), new(109, "R. Dash", "Blessings"),
                new(110, "R. Health", "Blessings"), new(111, "R. Ice", "Blessings"),
                new(112, "R. Insight", "Blessings"), new(113, "R. Jump", "Blessings"),
                new(114, "R. Lit Beats", "Blessings"), new(115, "R. Poison", "Blessings"),
                new(116, "R. Pull", "Blessings"), new(117, "R. Recycling", "Blessings"),
                new(118, "R. Regeneration", "Blessings"), new(119, "R. Relax", "Blessings"),
                new(120, "R. Shield", "Blessings"), new(121, "R. Speed", "Blessings"),
                new(122, "R. Spray", "Blessings"), new(123, "R. Storm", "Blessings"),
                new(124, "R. The Hunt", "Blessings"), new(125, "R. Vampire", "Blessings"),
                new(126, "R. Weapon Mastery", "Blessings"),
                // Epic
                new(59, "E. Battlecry", "Blessings"), new(60, "E. Bloodlust", "Blessings"),
                new(61, "E. Cardio", "Blessings"), new(62, "E. Charge", "Blessings"),
                new(63, "E. Dash", "Blessings"), new(64, "E. Healing Words", "Blessings"),
                new(65, "E. Health", "Blessings"), new(66, "E. Ice", "Blessings"),
                new(67, "E. Jump", "Blessings"), new(68, "E. Lit Beats", "Blessings"),
                new(69, "E. Poison", "Blessings"), new(70, "E. Recycling", "Blessings"),
                new(71, "E. Regeneration", "Blessings"), new(72, "E. Relax", "Blessings"),
                new(73, "E. Shield", "Blessings"), new(74, "E. Small", "Blessings"),
                new(75, "E. Speed", "Blessings"), new(76, "E. Spray", "Blessings"),
                new(77, "E. Storm Call", "Blessings"), new(78, "E. Storm", "Blessings"),
                new(79, "E. The Hunt", "Blessings"), new(80, "E. Vampire", "Blessings"),
                new(81, "E. Weapon Mastery", "Blessings"), new(82, "E. Words of Justice", "Blessings"),
                new(127, "The Assassin", "Blessings"), new(128, "The Mad Mechanic", "Blessings"),
                // Legendary
                new(83, "L. Battlecry", "Blessings"), new(84, "L. Bloodlust", "Blessings"),
                new(85, "L. Cardio", "Blessings"), new(86, "L. Charge", "Blessings"),
                new(87, "L. Dash", "Blessings"), new(88, "L. Healing Words", "Blessings"),
                new(89, "L. Health", "Blessings"), new(90, "L. Ice", "Blessings"),
                new(91, "L. Jump", "Blessings"), new(92, "L. Lit Beats", "Blessings"),
                new(93, "L. Poison", "Blessings"), new(94, "L. Recycling", "Blessings"),
                new(95, "L. Regeneration", "Blessings"), new(96, "L. Relax", "Blessings"),
                new(97, "L. Shield", "Blessings"), new(98, "L. Speed", "Blessings"),
                new(99, "L. Spray", "Blessings"), new(100, "L. Storm Call", "Blessings"),
                new(101, "L. Storm", "Blessings"), new(102, "L. The Hunt", "Blessings"),
                new(103, "L. Vampire", "Blessings"), new(104, "L. Weapon Mastery", "Blessings"),
                new(105, "L. Words of Justice", "Blessings"),
            };
        }

        private static List<SpawnLocation> BuildSpawns()
        {
            return new List<SpawnLocation>
            {
                new("Big Work", "366,130,-646",
                    "363,-617;341,-638;358,-711;334,-709;313,-683;380,-663;351,-736;288,-709;342,-687;278,-674;284,-626;322,-654",
                    244, "344,0,-656"),
                new("Actual Castle", "-689,145,510",
                    "-702,502;-725,522;-713,572;-710,536;-697,568;-683,543;-692,549;-691,523;-667,532;-671,550;-656,527;-688,568",
                    307, "-706,0,544"),
                new("Point of Impact", "-74,125,611",
                    "-41,540;-5,550;-1,589;-31,625;-47,600;-72,634;-90,600;-129,602;-92,559;-125,541;-71,519;-50,559;27,554",
                    370, "-78,0,566"),
                new("City (Quick Match)", "-674,125,-8",
                    "-578,-17;-611,32;-696,40;-728,-11;-675,-98;-626,-124;-578,-100",
                    250, "-674,0,-8"),
                new("City (Normal)", "-674,125,-8",
                    "-619,-20;-651,-33;-694,-40;-702,2;-664,18;-640,-1;-656,-21;-633,-53;-660,-77;-619,-80;-578,-17;-611,32;-696,40;-728,-11;-675,-98;-626,-124;-578,-100",
                    300, "-674,0,-8"),
                new("Crappy Castle", "-392,130,400",
                    "-345,390;-363,411;-382,421;-419,408;-420,366;-400,354;-371,364;-400,408",
                    200, "-392,0,400"),
                new("Area 64 (Quick Match)", "385,135,468",
                    "427,544;458,510;457,424;390,432;345,439;309,467;356,529",
                    250, "385,0,468"),
                new("Area 64 (Normal)", "385,135,468",
                    "427,544;458,510;457,424;390,432;345,439;309,467;356,529;370,494;350,504;329,490;353,464;395,461;433,463;396,516;433,511;392,501;417,521;428,426;440,448",
                    300, "385,0,468"),
                new("Western", "659,125,600",
                    "710,650;663,643;640,642;615,629;617,601;618,572;654,550;663,543;688,573;685,609;666,619;642,629;619,587;602,652;594,620;675,602;707,605;709,540",
                    300, "659,0,600"),
                new("Normandie", "-411,120,517",
                    "-531,604;-521,662;-481,638;-431,653;-396,594;-383,547;-410,537;-414,501;-458,501;-490,477;-520,514;-556,520;-559,492;-585,576;-362,579;-379,631;-579,540",
                    300, "-411,0,517"),
                new("Sandcastle", "685,140,8",
                    "654,-22;674,-23;691,-34;726,-23;728,0;734,27;705,41;672,39;656,37",
                    200, "685,0,8"),
                new("Fields by H", "-254,140,-128",
                    "-191,-165;-201,-232;-248,-261;-232,-225;-239,-176;-294,-200;-287,-137;-240,-102;-238,-138;-230,-165;-316,-217",
                    250, "-254,0,-128"),
                new("Industry", "98,125,-95",
                    "17,-51;68,-7;98,28;111,-13;77,-48;45,-92;129,-97;180,-115;191,-82;164,-47;211,-31;191,12;157,-10;122,-141;62,-6",
                    300, "98,0,-95"),
                new("Long Wall", "433,140,73",
                    "420,50;447,66;504,96;494,119;461,111;434,105;475,50;450,22;467,80;491,153;453,148;431,133;396,94;526,96;518,55;484,41;446,44",
                    300, "433,0,73"),
                new("Snow Castle", "-385,175,-771",
                    "-367,-728;-344,-760;-330,-796;-358,-817;-382,-790;-413,-808;-401,-774;-403,-793;-383,-753;-419,-744;-373,-778;-360,-784",
                    250, "-385,0,-771"),
                new("Pyramid", "636,125,240",
                    "632,240;633,197;674,200;683,257;642,278;622,300;584,271;586,195;597,228",
                    250, "636,0,240"),
                new("Chaos", "-13,140,-523",
                    "-32,129,-645;-70,133,-635;-71,141,-594;-91,137,-558;-63,131,-513;-24,132,-494;16,137,-515;28,132,-557;10,124,-600",
                    300, "-13,0,-523"),
            };
        }

        private static List<CurseInfo> BuildCurses()
        {
            return new List<CurseInfo>
            {
                new(0, "Recoil"), new(1, "Inaccuracy"), new(2, "Random Shooting"),
                new(3, "Always Shoot"), new(4, "Big"), new(5, "Fog"),
                new(6, "Fragile"), new(7, "Frog"), new(8, "Gravity"),
                new(9, "No Jump"), new(10, "Only Look Right"), new(11, "Rubber Banding"),
                new(12, "Slow Gun"), new(13, "Slow Reload"), new(14, "Slow Bullets"),
                new(15, "Slow Medicine"), new(16, "Small Jump"), new(17, "Small Mag"),
                new(18, "Varying Sensitivity"),
            };
        }
    }
}
