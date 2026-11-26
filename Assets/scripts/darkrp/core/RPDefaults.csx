// DarkRP for s&box - default content: DarkRP's default jobs, shipments, entities, ammo, food and settings
// (config/jobrelated.lua, addentities.lua, ammotypes.lua, hungermod; MIT, Falco Peijnenburg), using s&box models
// and clothing instead of Half-Life 2 / Counter-Strike content. Server owners change or remove any of it in
// darkrp/*.json; addons with DarkRP.OnLoad.

public static class RPDefaults
{
	const string C = "models/citizen_clothes/";

	public static void Register( RPRegistry r )
	{
		Weapons( r );
		EntityTypes( r );
		Categories( r );
		Jobs( r );
		Shipments( r );
		Entities( r );
		Ammo( r );
		Food( r );
		Groups( r );

		r.DefaultLaws.Add( "Do not attack other citizens except in self-defence." );
		r.DefaultLaws.Add( "Do not steal or break into people's homes." );
		r.DefaultLaws.Add( "Money printers/drugs are illegal." );

		foreach ( var w in new[] { "keys", "pocket", "fists", "physgun", "toolgun", "camera" } ) r.DefaultWeapons.Add( w );
	}

	static void Weapons( RPRegistry r )
	{
		// DarkRP's own weapons (Assets/darkrp/weapons). Guns are Sandbox's guns re-tuned to DarkRP's numbers.
		W( r, "arrest_stick", "Arrest Baton", "darkrp/weapons/arrest_stick.prefab", false );
		W( r, "unarrest_stick", "Unarrest Baton", "darkrp/weapons/unarrest_stick.prefab", false );
		W( r, "stunstick", "Stun Stick", "darkrp/weapons/stunstick.prefab", false );
		W( r, "door_ram", "Battering Ram", "darkrp/weapons/door_ram.prefab", false );
		W( r, "keys", "Keys", "darkrp/weapons/keys.prefab", false );
		W( r, "lockpick", "Lock Pick", "darkrp/weapons/lockpick.prefab", false );
		W( r, "pocket", "Pocket", "darkrp/weapons/pocket.prefab", false );
		W( r, "med_kit", "Medic Kit", "darkrp/weapons/med_kit.prefab", false );
		W( r, "weaponchecker", "Weapon Checker", "darkrp/weapons/weaponchecker.prefab", false );
		W( r, "weapon_keypadchecker", "Keypad Checker", "darkrp/weapons/keypadchecker.prefab", false );
		W( r, "weapon_ak472", "AK47", "darkrp/weapons/weapon_ak472.prefab", true );
		W( r, "weapon_deagle2", "Deagle", "darkrp/weapons/weapon_deagle2.prefab", true );
		W( r, "weapon_fiveseven2", "FiveSeven", "darkrp/weapons/weapon_fiveseven2.prefab", true );
		W( r, "weapon_glock2", "Glock", "darkrp/weapons/weapon_glock2.prefab", true );
		W( r, "weapon_m42", "M4", "darkrp/weapons/weapon_m42.prefab", true );
		W( r, "weapon_mac102", "Mac10", "darkrp/weapons/weapon_mac102.prefab", true );
		W( r, "weapon_mp52", "MP5", "darkrp/weapons/weapon_mp52.prefab", true );
		W( r, "weapon_p2282", "P228", "darkrp/weapons/weapon_p2282.prefab", true );
		W( r, "weapon_pumpshotgun2", "Pump Shotgun", "darkrp/weapons/weapon_pumpshotgun2.prefab", true );
		W( r, "ls_sniper", "Silenced Sniper", "darkrp/weapons/ls_sniper.prefab", true );

		// Sandbox's own weapons, so jobs and shipments can use them too.
		W( r, "physgun", "Physics Gun", "weapons/physgun/physgun.prefab", false );
		W( r, "toolgun", "Tool Gun", "weapons/toolgun/toolgun.prefab", false );
		W( r, "camera", "Camera", "weapons/camera/camera.prefab", false );
		W( r, "crowbar", "Crowbar", "weapons/crowbar/crowbar.prefab", false );
		W( r, "fists", "Fists", "weapons/fists/fists.prefab", false );
		W( r, "usp", "USP", "weapons/usp/usp.prefab", true );
		W( r, "colt1911", "Colt 1911", "weapons/colt1911/colt1911.prefab", true );
		W( r, "revolver", "Revolver", "weapons/revolver/revolver.prefab", true );
		W( r, "m4a1", "AR-4", "weapons/m4a1/m4a1.prefab", true );
		W( r, "mp5", "MP5 (Sandbox)", "weapons/mp5/mp5.prefab", true );
		W( r, "shotgun", "Shotgun", "weapons/shotgun/shotgun.prefab", true );
		W( r, "sniper", "Sniper", "weapons/sniper/sniper.prefab", true );
		W( r, "rpg", "RPG", "weapons/rpg/rpg.prefab", true );
		W( r, "grenade", "Grenade", "weapons/grenade/grenade.prefab", true );
	}

	static void W( RPRegistry r, string id, string name, string prefab, bool license ) =>
		r.AddWeapon( new RPWeaponDef { Id = id, Name = name, Prefab = prefab, NeedsLicense = license } );

	static void EntityTypes( RPRegistry r )
	{
		E( r, "spawned_money", "Money", "RPMoneyEntity", "models/darkrp/money_stack.vmdl", 1.5f, true );
		E( r, "spawned_shipment", "Shipment", "RPShipmentEntity", "models/sbox/warehouse/woodencrates/woodencrate_01.vmdl", 0.5f, false );
		E( r, "spawned_weapon", "Weapon", "RPSpawnedWeapon", "", 1f, true );
		E( r, "spawned_food", "Food", "RPFoodEntity", "models/citizen_props/hotdog01.vmdl", 1f, true );
		E( r, "spawned_ammo", "Ammo", "RPAmmoEntity", "weapons/ammo/ammobox_pistol/ammobox_pistol.vmdl", 1f, true );
		E( r, "money_printer", "Money Printer", "RPMoneyPrinter", "models/sbox/office/computer_drive_unit/computer_drive_unit.vmdl", 1f, true );
		E( r, "drug_lab", "Drug Lab", "RPDrugLab", "models/sbox/kitchen/kitchen_work_counter_04/kitchen_work_counter_04.vmdl", 0.5f, false );
		E( r, "drug", "Drugs", "RPDrug", "models/citizen_props/sodacan01.vmdl", 1f, true );
		E( r, "gunlab", "Gun Lab", "RPGunLab", "models/sbox/industrial/lockers/locker_single.vmdl", 0.6f, false );
		E( r, "microwave", "Microwave", "RPMicrowave", "models/citizen_props/oldoven.vmdl", 0.5f, true );
		E( r, "darkrp_tip_jar", "Tip Jar", "RPTipJar", "models/citizen_props/beertankard01.vmdl", 1.5f, true );
		E( r, "letter", "Letter", "RPLetter", "models/citizen_props/newspaper01.vmdl", 1f, true );
		E( r, "darkrp_cheque", "Cheque", "RPCheque", "models/citizen_props/newspaper01.vmdl", 0.6f, true );
		E( r, "darkrp_laws", "Law Board", "RPLawBoard", "models/darkrp/lawboard.vmdl", 1f, false );
		E( r, "darkrp_door", "Door", "RPDoorEntity", "models/sbox/doors/door_a/door_a.vmdl", 1f, false );
		// Prop protection, as DarkRP's FPP: DarkRP's entities can't be physgunned or tooled (only picked up), except
		// the tip jar and the law board. Doors are for admins only.
		r.EntityType( "darkrp_tip_jar" ).Touch = "physgun,toolgun,pickup";
		r.EntityType( "darkrp_laws" ).Touch = "physgun";
		r.EntityType( "darkrp_door" ).Touch = "";
	}

	static void E( RPRegistry r, string id, string name, string comp, string model, float scale, bool pocket ) =>
		r.AddEntityType( new RPEntityType { Id = id, Name = name, Component = comp, Model = model, Scale = scale, Pocketable = pocket } );

	static void Categories( RPRegistry r )
	{
		Cat( r, "Citizens", "jobs", "#006b00", 100 );
		Cat( r, "Civil Protection", "jobs", "#1919aa", 101 );
		Cat( r, "Gangsters", "jobs", "#4b4b4b", 101 );
		Cat( r, "Other", "jobs", "#006b00", 255 );
		Cat( r, "Pistols", "weapons", "#006b00", 100 );
		Cat( r, "Other", "weapons", "#006b00", 255 );
		Cat( r, "Rifles", "shipments", "#006b00", 100 );
		Cat( r, "Shotguns", "shipments", "#006b00", 101 );
		Cat( r, "Snipers", "shipments", "#006b00", 102 );
		Cat( r, "Other", "shipments", "#006b00", 255 );
		Cat( r, "Other", "entities", "#006b00", 255 );
		Cat( r, "Other", "ammo", "#006b00", 255 );
	}

	static void Cat( RPRegistry r, string name, string kind, string color, int order ) =>
		r.AddCategory( new RPCategory { Name = name, Categorises = kind, Color = color, SortOrder = order } );

	static void Jobs( RPRegistry r )
	{
		int sal = 45;

		r.AddJob( new RPJob
		{
			Name = "Citizen", Command = "citizen", Color = "#149614", Salary = sal, Max = 0, Category = "Citizens", CanDemote = false,
			Description = "The Citizen is the most basic level of society you can hold besides being a hobo. You have no specific role in city life.",
			// no outfit: citizens wear their own s&box avatar
		} );

		r.AddJob( new RPJob
		{
			Name = "Civil Protection", Command = "cp", Color = "#1919aa", Salary = (int)(sal * 1.45f), Max = 4, Category = "Civil Protection",
			Vote = true, HasLicense = true, Police = true,
			Weapons = new[] { "arrest_stick", "unarrest_stick", "weapon_glock2", "stunstick", "door_ram", "weaponchecker" },
			Ammo = new[] { "pistol:60" },
			Outfit = new[] { C + "hat/police_hat/police_hat.clothing", C + "shirt/flannel_shirt/variations/blue_shirt/blue_shirt.clothing", C + "vest/tactical_vest/models/tactical_vest.clothing", C + "trousers/smarttrousers/trousers.smart.clothing", C + "shoes/boots/black_boots.clothing" },
			Description = "The protector of every citizen that lives in the city. You have the power to arrest criminals and protect innocents. Hit a player with your arrest baton to put them in jail. Bash a player with a stunstick and they may learn to obey the law. The Battering Ram can break down the door of a criminal, with a warrant for their arrest. Type /wanted <name> to alert the public to the presence of a criminal.",
		} );

		r.AddJob( new RPJob
		{
			Name = "Gangster", Command = "gangster", Color = "#4b4b4b", Salary = sal, Max = 3, Category = "Gangsters",
			Outfit = new[] { C + "hat/bandana_mask/bandana_mask.clothing", C + "jacket/hoodie/hoodie_black.clothing", C + "trousers/jeans/jeans_black.clothing", C + "shoes/trainers/trainers.clothing", C + "gloves/fingerless_gloves/fingerless_gloves.clothing" },
			Description = "The lowest person of crime. A gangster generally works for the Mobboss who runs the crime family. The Mob boss sets your agenda and you follow it or you might be punished.",
		} );

		r.AddJob( new RPJob
		{
			Name = "Mob boss", Command = "mobboss", Color = "#191919", Salary = (int)(sal * 1.34f), Max = 1, Category = "Gangsters", Hitman = true,
			Weapons = new[] { "lockpick", "unarrest_stick" },
			Outfit = new[] { C + "hat/fedora/fedora.clothing", C + "shirt/waistcoat_and_shirt/waistcoat_and_shirt.clothing", C + "jacket/brown_leather_jacket/black_leather_jacket.clothing", C + "trousers/smarttrousers/trousers.smart.clothing", C + "shoes/boots/black_boots.clothing", C + "watch/modern_watch/modern_watch_black.clothing" },
			Description = "The Mob boss is the boss of the criminals in the city. With their power they coordinate the gangsters and form an efficient crime organization. They have the ability to break into houses by using a lockpick. The Mob boss posesses the ability to unarrest you.",
		} );

		r.AddJob( new RPJob
		{
			Name = "Gun Dealer", Command = "gundealer", Color = "#ff8c00", Salary = sal, Max = 2, Category = "Citizens",
			Outfit = new[] { C + "hat/baseball_cap/baseball_cap_black.clothing", C + "jacket/bomber_jacket/bomber_jacket.clothing", C + "trousers/cargopants/brown_cargo_pants.clothing", C + "shoes/boots/army_boots.clothing" },
			Description = "A Gun Dealer is the only person who can sell guns to other people. Make sure you aren't caught selling illegal firearms to the public! You might get arrested!",
		} );

		r.AddJob( new RPJob
		{
			Name = "Medic", Command = "medic", Color = "#2f4f4f", Salary = sal, Max = 3, Category = "Citizens", Medic = true,
			Weapons = new[] { "med_kit" },
			Outfit = new[] { C + "jacket/labcoat/labcoat.clothing", C + "shirt/polo_shirt/polo_shirt_white.clothing", C + "trousers/smarttrousers/trousers.smart.clothing", C + "gloves/rubber_gloves/rubber_gloves.clothing", C + "shoes/sneakers/sneakers.clothing", C + "glasses/nerdy_glasses/nerdy_glasses.clothing" },
			Description = "With your medical knowledge you work to restore players to full health. Without a medic, people cannot be healed. Left click with the Medical Kit to heal other players. Right click with the Medical Kit to heal yourself.",
		} );

		r.AddJob( new RPJob
		{
			Name = "Civil Protection Chief", Command = "chief", Color = "#1414ff", Salary = (int)(sal * 1.67f), Max = 1, Category = "Civil Protection",
			HasLicense = true, Police = true, Chief = true, NeedToChangeFrom = new[] { "cp" },
			Weapons = new[] { "arrest_stick", "unarrest_stick", "weapon_deagle2", "stunstick", "door_ram", "weaponchecker" },
			Ammo = new[] { "pistol:60" },
			Outfit = new[] { C + "hat/police_hat/police_hat.clothing", C + "shirt/waistcoat_and_shirt/waistcoat_and_shirt.clothing", C + "vest/tactical_vest/models/tactical_vest.clothing", C + "trousers/smarttrousers/trousers.smart.clothing", C + "shoes/boots/black_boots.clothing", C + "gloves/rubber_gloves/rubber_gloves_black.clothing" },
			Description = "The Chief is the leader of the Civil Protection unit. Coordinate the police force to enforce law in the city. Hit a player with arrest baton to put them in jail. Bash a player with a stunstick and they may learn to obey the law. The Battering Ram can break down the door of a criminal, with a warrant for their arrest. Type /wanted <name> to alert the public to the presence of a criminal. Type /jailpos to set the Jail Position.",
		} );

		r.AddJob( new RPJob
		{
			Name = "Mayor", Command = "mayor", Color = "#961414", Salary = (int)(sal * 1.89f), Max = 1, Category = "Civil Protection",
			Vote = true, Police = true, Mayor = true,
			Outfit = new[] { C + "shirt/waistcoat_and_shirt/waistcoat_and_shirt.clothing", C + "jacket/winter_coat/winter_coat.clothing", C + "trousers/smarttrousers/trousers.smart.clothing", C + "shoes/boots/black_boots.clothing", C + "watch/modern_watch/modern_watch.clothing", C + "necklace/vintage_necklace/vintage_necklace.clothing" },
			Description = "The Mayor of the city creates laws to govern the city. If you are the mayor you may create and accept warrants. Type /wanted <name> to warrant a player. Type /jailpos to set the Jail Position. Type /lockdown to initiate a lockdown of the city. Everyone must be inside during a lockdown. The cops patrol the area. /unlockdown to end a lockdown.",
		} );

		r.AddJob( new RPJob
		{
			Name = "Hobo", Command = "hobo", Color = "#502d00", Salary = 0, Max = 5, Category = "Citizens", CanDemote = false, Hobo = true,
			Outfit = new[] { C + "hat/trapper_hat/trapper_hat_cloth.clothing", C + "jumper/cardigan/cardigan_dirty.clothing", C + "trousers/loosetrousers/loose_trousers.clothing", C + "shoes/slippers/slippers.clothing", C + "hair/patchy_beard/patchy_beard.clothing" },
			Description = "The lowest member of society. Everybody laughs at you. You have no home. Beg for your food and money. Sing for everyone who passes to get money. Make your own wooden home somewhere in a corner or outside someone else's door.",
		} );

		r.AddJob( new RPJob
		{
			Name = "Cook", Command = "cook", Color = "#ee6363", Salary = 45, Max = 2, Category = "Citizens", Cook = true,
			Outfit = new[] { C + "hat/chefs_hat/chefs_hat.clothing", C + "shirt/chef_shirt/chef_shirt.clothing", C + "vest/chefs_apron/chefs_apron.clothing", C + "trousers/loosetrousers/loose_trousers.clothing", C + "shoes/crocs_and_socks/clogs_and_socks.clothing" },
			Description = "As a cook, it is your responsibility to feed the other members of your city. You can spawn a microwave and sell the food you make: /buymicrowave",
		} );
	}

	static void Shipments( RPRegistry r )
	{
		S( r, "Desert eagle", "weapon_deagle2", "weapons/magnum/w_magnum.vmdl", 215, 10, true, 215, true, "gundealer", "Pistols" );
		S( r, "Fiveseven", "weapon_fiveseven2", "weapons/usp/w_usp.vmdl", 0, 10, true, 205, true, "gundealer", "Pistols" );
		S( r, "Glock", "weapon_glock2", "weapons/usp/w_usp.vmdl", 0, 10, true, 160, true, "gundealer", "Pistols" );
		S( r, "P228", "weapon_p2282", "weapons/1911/w_1911.vmdl", 0, 10, true, 185, true, "gundealer", "Pistols" );
		S( r, "AK47", "weapon_ak472", "weapons/m4a1/w_m4a1.vmdl", 2450, 10, false, 0, false, "gundealer", "Rifles" );
		S( r, "MP5", "weapon_mp52", "weapons/mp5/w_mp5.vmdl", 2200, 10, false, 0, false, "gundealer", "Rifles" );
		S( r, "M4", "weapon_m42", "weapons/m4a1/w_m4a1.vmdl", 2450, 10, false, 0, false, "gundealer", "Rifles" );
		S( r, "Mac 10", "weapon_mac102", "weapons/mp5/w_mp5.vmdl", 2150, 10, false, 0, false, "gundealer", "Other" );
		S( r, "Pump shotgun", "weapon_pumpshotgun2", "weapons/shotgun/w_spaghellim4.vmdl", 1750, 10, false, 0, false, "gundealer", "Shotguns" );
		S( r, "Sniper rifle", "ls_sniper", "weapons/m700/w_m700.vmdl", 3750, 10, false, 0, false, "gundealer", "Snipers" );
	}

	static void S( RPRegistry r, string name, string ent, string model, int price, int amount, bool separate, int priceSep, bool noShip, string allowed, string cat ) =>
		r.AddShipment( new RPShipment { Name = name, Entity = ent, Model = model, Price = price, Amount = amount, Separate = separate, PriceSep = priceSep, NoShip = noShip, Allowed = new[] { allowed }, Category = cat } );

	static void Entities( RPRegistry r )
	{
		r.AddEntity( new RPEntityDef { Name = "Drug lab", Ent = "drug_lab", Model = "models/sbox/kitchen/kitchen_work_counter_04/kitchen_work_counter_04.vmdl", Price = 400, Max = 3, Cmd = "buydruglab", Allowed = new[] { "gangster", "mobboss" } } );
		r.AddEntity( new RPEntityDef { Name = "Money printer", Ent = "money_printer", Model = "models/sbox/office/computer_drive_unit/computer_drive_unit.vmdl", Price = 1000, Max = 2, Cmd = "buymoneyprinter" } );
		r.AddEntity( new RPEntityDef { Name = "Tip Jar", Ent = "darkrp_tip_jar", Model = "models/citizen_props/beertankard01.vmdl", Price = 0, Max = 2, Cmd = "tipjar" } );
		r.AddEntity( new RPEntityDef { Name = "Gun lab", Ent = "gunlab", Model = "models/sbox/industrial/lockers/locker_single.vmdl", Price = 500, Max = 1, Cmd = "buygunlab", Allowed = new[] { "gundealer" } } );
		r.AddEntity( new RPEntityDef { Name = "Microwave", Ent = "microwave", Model = "models/citizen_props/oldoven.vmdl", Price = 400, Max = 1, Cmd = "buymicrowave", Allowed = new[] { "cook" } } );
	}

	static void Ammo( RPRegistry r )
	{
		r.AddAmmo( new RPAmmoDef { AmmoType = "pistol", Name = "Pistol ammo", Model = "weapons/ammo/ammobox_pistol/ammobox_pistol.vmdl", Price = 30, Amount = 24 } );
		r.AddAmmo( new RPAmmoDef { AmmoType = "buckshot", Name = "Shotgun ammo", Model = "weapons/ammo/ammobox_shotgun/ammobox_shotgun.vmdl", Price = 50, Amount = 8 } );
		r.AddAmmo( new RPAmmoDef { AmmoType = "smg1", Name = "Rifle ammo", Model = "weapons/ammo/ammobox_pistol/ammobox_pistol.vmdl", Price = 80, Amount = 30 } );
		r.AddAmmo( new RPAmmoDef { AmmoType = "sniper", Name = "Sniper ammo", Model = "weapons/ammo/rifleshell/rifleshell.vmdl", Price = 100, Amount = 10 } );
	}

	static void Food( RPRegistry r )
	{
		F( r, "Hot dog", "models/citizen_props/hotdog01.vmdl", 20, 20 );
		F( r, "Melon", "models/sbox/home/watermelon/watermelon.vmdl", 20, 20 );
		F( r, "Ice cream", "models/citizen_props/icecreamcone01.vmdl", 10, 10 );
		F( r, "Chips", "models/citizen_props/chippacket01.vmdl", 10, 10 );
		F( r, "Pop can", "models/citizen_props/sodacan01.vmdl", 5, 5 );
		F( r, "Cola", "models/sbox/kitchen/cola_can/cola_can.vmdl", 5, 5 );
		F( r, "Canned food", "models/sbox/kitchen/packaged_food/can_long.vmdl", 30, 30 );
	}

	static void F( RPRegistry r, string name, string model, int energy, int price ) =>
		r.AddFood( new RPFoodDef { Name = name, Model = model, Energy = energy, Price = price } );

	static void Groups( RPRegistry r )
	{
		r.DoorGroups.Add( new RPDoorGroup { Name = "Cops and Mayor only", Jobs = new[] { "chief", "cp", "mayor" } } );
		r.DoorGroups.Add( new RPDoorGroup { Name = "Gundealer only", Jobs = new[] { "gundealer" } } );
		r.Agendas.Add( new RPAgendaDef { Title = "Gangster's agenda", Managers = new[] { "mobboss" }, Listeners = new[] { "gangster" } } );
		r.Agendas.Add( new RPAgendaDef { Title = "Police agenda", Managers = new[] { "mayor", "chief" }, Listeners = new[] { "cp" } } );
		r.GroupChats.Add( new RPGroupChat { Name = "police", Check = c => RP.IsCP( c ) } );
		r.GroupChats.Add( new RPGroupChat { Name = "gangsters", Jobs = new[] { "mobboss", "gangster" } } );
		r.DemoteGroups.Add( new RPDemoteGroup { Name = "Cops", Jobs = new[] { "cp", "chief" } } );
		r.DemoteGroups.Add( new RPDemoteGroup { Name = "Gangsters", Jobs = new[] { "gangster", "mobboss" } } );
	}
}
