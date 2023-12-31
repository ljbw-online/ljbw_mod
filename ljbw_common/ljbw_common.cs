﻿namespace Ljbw_common
{
    enum botToModMessage : byte
    {
        model_output,
        supervised_model_resumed,
        model_paused,
        reinforcement_model_resumed,
        reinforcement_learning_resumed,
        reinforcement_learning_paused,
        paint_random,
        paint_colour,
        reset,
        new_area,
        fix,
        clean,
        UNUSED,
        chat_key,
        starting_position,
        current_position,
        set_control,
        weather,
        veh,
        teleport,
        get_heading,
        set_heading,
        action,
        state
    }
    
    enum modToBotMessage : byte
    {
        starting_position = botToModMessage.starting_position,
        current_position = botToModMessage.current_position,
        current_heading = botToModMessage.get_heading,
        state = botToModMessage.state
    }

    class Class
    {
        public const int script_to_mod_array_length = 17;
        public const int mod_to_script_array_length = 29;

        public static string[] weatherNames = new string[] { "blizzard", "clear", "clearing", "clouds", "extrasunny", "foggy", "halloween", "neutral", "overcast", "rain",
        "smog", "snow", "snowlight", "thunder", "xmas" };

        //public static string[] vehicleNames = new string[] { "voltic", "voltic2", "ruiner2", "cablecar", "deluxo", "rhino", "kosatka", "apc", "cheburek", "airtug" };
        public static string[] vehicleNames = new string[] { "ninef", "ninef2", "blista", "asea", "asea2", "boattrailer", "bus", "armytanker", "armytrailer", "armytrailer2", "freighttrailer", 
            "coach", "airbus", "asterope", "airtug", "ambulance", "barracks", "barracks2", "baller", "baller2", "bjxl", "banshee", "benson", "bfinjection", "biff", "blazer", "blazer2", 
            "blazer3", "bison", "bison2", "bison3", "boxville", "boxville2", "boxville3", "bobcatxl", "bodhi2", "buccaneer", "buffalo", "buffalo2", "bulldozer", "bullet", "blimp", "burrito", 
            "burrito2", "burrito3", "burrito4", "burrito5", "cavalcade", "cavalcade2", "policet", "gburrito", "cablecar", "caddy", "caddy2", "camper", "carbonizzare", "cheetah", "comet2", 
            "cogcabrio", "coquette", "cutter", "gresley", "dilettante", "dilettante2", "dune", "dune2", "hotknife", "dloader", "dubsta", "dubsta2", "dump", "rubble", "docktug", "dominator", 
            "emperor", "emperor2", "emperor3", "entityxf", "exemplar", "elegy2", "f620", "fbi", "fbi2", "felon", "felon2", "feltzer2", "firetruk", "flatbed", "forklift", "fq2", "fusilade", 
            "fugitive", "futo", "granger", "gauntlet", "habanero", "hauler", "handler", "infernus", "ingot", "intruder", "issi2", "Jackal", "journey", "jb700", "khamelion", "landstalker", 
            "lguard", "manana", "mesa", "mesa2", "mesa3", "crusader", "minivan", "mixer", "mixer2", "monroe", "mower", "mule", "mule2", "oracle", "oracle2", "packer", "patriot", "pbus", 
            "penumbra", "peyote", "phantom", "phoenix", "picador", "pounder", "police", "police4", "police2", "police3", "policeold1", "policeold2", "pony", "pony2", "prairie", "pranger", 
            "premier", "primo", "proptrailer", "rancherxl", "rancherxl2", "rapidgt", "rapidgt2", "radi", "ratloader", "rebel", "regina", "rebel2", "rentalbus", "ruiner", "rumpo", "rumpo2", 
            "rhino", "riot", "ripley", "rocoto", "romero", "sabregt", "sadler", "sadler2", "sandking", "sandking2", "schafter2", "schwarzer", "scrap", "seminole", "sentinel", "sentinel2", 
            "zion", "zion2", "serrano", "sheriff", "sheriff2", "speedo", "speedo2", "stanier", "stinger", "stingergt", "stockade", "stockade3", "stratum", "sultan", "superd", "surano", 
            "surfer", "surfer2", "surge", "taco", "tailgater", "taxi", "trash", "tractor", "tractor2", "tractor3", "graintrailer", "baletrailer", "tiptruck", "tiptruck2", "tornado", "tornado2", 
            "tornado3", "tornado4", "tourbus", "towtruck", "towtruck2", "utillitruck", "utillitruck2", "utillitruck3", "voodoo2", "washington", "stretch", "youga", "ztype", "sanchez", 
            "sanchez2", "scorcher", "tribike", "tribike2", "tribike3", "fixter", "cruiser", "BMX", "policeb", "akuma", "carbonrs", "bagger", "bati", "bati2", "ruffian", "daemon", "double", 
            "pcj", "vader", "vigero", "faggio2", "hexer", "annihilator", "buzzard", "buzzard2", "cargobob", "cargobob2", "cargobob3", "skylift", "polmav", "maverick", "nemesis", "frogger", 
            "frogger2", "cuban800", "duster", "stunt", "mammatus", "jet", "shamal", "luxor", "titan", "lazer", "cargoplane", "squalo", "marquis", "dinghy", "dinghy2", "jetmax", "predator", 
            "tropic", "seashark", "seashark2", "submersible", "freightcar", "freight", "freightcont1", "freightcont2", "freightgrain", "tankercar", "metrotrain", "docktrailer", "trailers", 
            "trailers2", "trailers3", "tvtrailer", "raketrailer", "tanker", "trailerlogs", "tr2", "tr3", "tr4", "trflat", "trailersmall", "velum", "adder", "voltic", "vacca", "suntrap", 
            "submersible2", "dukes", "dukes2", "buffalo3", "dominator2", "dodo", "marshall", "blimp2", "gauntlet2", "stalion", "stalion2", "blista2", "blista3", "bifta", "speeder", "kalahari", 
            "paradise", "btype", "jester", "turismor", "alpha", "vestra", "zentorno", "massacro", "huntley", "thrust", "rhapsody", "warrener", "blade", "glendale", "panto", "dubsta3", 
            "pigalle", "monster", "sovereign", "innovation", "hakuchou", "furoregt", "miljet", "besra", "coquette2", "swift", "jester2", "massacro2", "ratloader2", "slamvan", "mule3", "velum2", 
            "tanker2", "casco", "boxville4", "hydra", "insurgent", "insurgent2", "gburrito2", "technical", "dinghy3", "savage", "enduro", "guardian", "lectro", "kuruma", "kuruma2", "trash2", 
            "barracks3", "valkyrie", "slamvan2", "swift2", "luxor2", "feltzer3", "osiris", "virgo", "windsor", "coquette3", "vindicator", "t20", "brawler", "toro", "chino", "faction", 
            "faction2", "moonbeam", "moonbeam2", "primo2", "chino2", "buccaneer2", "voodoo", "Lurcher", "btype2", "verlierer2", "nightshade", "mamba", "limo2", "schafter3", "schafter4", 
            "schafter5", "schafter6", "cog55", "cog552", "cognoscenti", "cognoscenti2", "baller3", "baller4", "baller5", "baller6", "toro2", "seashark3", "dinghy4", "tropic2", "speeder2", 
            "cargobob4", "supervolito", "supervolito2", "valkyrie2", "tampa", "sultanrs", "banshee2", "btype3", "faction3", "minivan2", "sabregt2", "slamvan3", "tornado5", "virgo2", "virgo3", 
            "nimbus", "xls", "xls2", "seven70", "fmj", "bestiagts", "pfister811", "brickade", "rumpo3", "volatus", "prototipo", "reaper", "tug", "windsor2", "lynx", "gargoyle", "tyrus", 
            "sheava", "omnis", "le7b", "contender", "trophytruck", "trophytruck2", "rallytruck", "cliffhanger", "bf400", "tropos", "brioso", "tampa2", "tornado6", "faggio3", "faggio", "raptor", 
            "vortex", "avarus", "sanctus", "youga2", "hakuchou2", "nightblade", "chimera", "esskey", "wolfsbane", "zombiea", "zombieb", "defiler", "daemon2", "ratbike", "shotaro", "manchez", 
            "blazer4", "elegy", "tempesta", "italigtb", "italigtb2", "nero", "nero2", "specter", "specter2", "diablous", "diablous2", "blazer5", "ruiner2", "dune4", "dune5", "phantom2", 
            "voltic2", "penetrator", "boxville5", "wastelander", "technical2", "fcr", "fcr2", "comet3", "ruiner3", "turismo2", "infernus2", "gp1", "ruston", "trailers4", "xa21", "caddy3", 
            "vagner", "phantom3", "nightshark", "cheetah2", "torero", "hauler2", "trailerlarge", "technical3", "insurgent3", "apc", "tampa3", "dune3", "trailersmall2", "halftrack", "ardent", 
            "oppressor", "vigilante", "bombushka", "alphaz1", "seabreeze", "tula", "havok", "hunter", "microlight", "rogue", "pyro", "howard", "mogul", "starling", "nokota", "molotok", 
            "rapidgt3", "retinue", "cyclone", "visione", "z190", "viseris", "comet5", "raiden", "riata", "sc1", "autarch", "savestra", "gt500", "comet4", "neon", "sentinel3", "khanjali", 
            "barrage", "volatol", "akula", "deluxo", "stromberg", "chernobog", "riot2", "avenger", "avenger2", "thruster", "yosemite", "hermes", "hustler", "streiter", "revolter", "pariah", 
            "kamacho", "entity2", "cheburek", "jester3", "caracara", "hotring", "seasparrow", "flashgt", "ellie", "michelli", "fagaloa", "dominator3", "tyrant", "tezeract", "gb200", "issi3", 
            "taipan", "blimp3", "mule4", "pounder2", "speedo4", "pbus2", "patriot2", "swinger", "terbyte", "oppressor2", "strikeforce", "menacer", "scramjet", "freecrawler", "stafford", 
            "bruiser", "bruiser2", "bruiser3", "brutus", "brutus2", "brutus3", "cerberus", "cerberus2", "cerberus3", "clique", "deathbike", "deathbike2", "deathbike3", "deveste", "deviant", 
            "dominator4", "dominator5", "dominator6", "impaler", "impaler2", "impaler3", "impaler4", "imperator", "imperator2", "imperator3", "issi4", "issi5", "issi6", "italigto", "monster3", 
            "monster4", "monster5", "rcbandito", "scarab", "scarab2", "scarab3", "schlagen", "slamvan4", "slamvan5", "slamvan6", "toros", "tulip", "vamos", "zr380", "zr3802", "zr3803", 
            "caracara2", "drafter", "dynasty", "emerus", "gauntlet3", "gauntlet4", "hellion", "issi7", "jugular", "krieger", "locust", "paragon", "paragon2", "peyote2", "nebula", "neo", 
            "novak", "rrocket", "s80", "thrax", "zion3", "zorrusso", "asbo", "formula2", "formula", "furia", "imorgon", "jb7002", "kanjo", "komoda", "minitank", "outlaw", "rebla", "retinue2", 
            "stryder", "sugoi", "sultan2", "vagrant", "vstr", "yosemite2", "zhaba", "club", "coquette4", "dukes3", "gauntlet5", "glendale2", "landstalker2", "manana2", "openwheel1", 
            "openwheel2", "penumbra2", "peyote3", "seminole2", "tigon", "yosemite3", "youga3", "veto2", "squaddie", "dinghy5", "annihilator2", "italirsx", "veto", "toreador", "slamtruck", 
            "weevil", "vetir", "alkonost", "patrolboat", "avisa", "brioso2", "verus", "longfin", "seasparrow2", "seasparrow3", "winky", "manchez2", "kosatka", "freightcar2", "comet6", 
            "dominator7", "dominator8", "euros", "futo2", "rt3000", "sultan3", "tailgater2", "growler", "vectre", "remus", "calico", "cypher", "jester4", "zr350", "previon", "warrener2", 
            "shinobi", "reever", "champion", "cinquemila", "iwagen", "astron", "baller7", "buffalo4", "comet7", "deity", "ignus", "jubilee", "patriot3", "zeno", "granger2", "youga4", "mule5", 
            "omnisegt", "sentinel4", "ruiner4", "brioso3", "corsita", "draugur", "kanjosj", "postlude", "torero2", "vigero2", "lm87", "tenf", "tenf2", "rhinehart", "conada", "greenwood", 
            "sm722", "weevil2" };
    }

}