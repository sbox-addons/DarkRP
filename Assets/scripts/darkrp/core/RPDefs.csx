// DarkRP for s&box - content definitions (jobs, shipments, entities, ammo, food, categories...).
// Ported from DarkRP for Garry's Mod (https://github.com/FPtje/DarkRP), Copyright (c) 2014 Falco Peijnenburg,
// MIT License (see DARKRP_NOTICE.txt). Field names follow DarkRP's createJob/createShipment/createEntity tables.

/// <summary>A job (DarkRP "team"). Create one with <c>DarkRP.CreateJob</c> or in <c>darkrp/jobs.json</c>.</summary>
public sealed class RPJob
{
	/// <summary>Team number, assigned when registered (1 = first job). Like GMod's TEAM_* values.</summary>
	public int Index;
	public string Name = "";
	/// <summary>Chat command and unique id: <c>/citizen</c>, <c>/job cp</c>...</summary>
	public string Command = "";
	/// <summary>Team colour as hex, e.g. "#14961e".</summary>
	public string Color = "#ffffff";
	public string Description = "";
	/// <summary>Player models (.vmdl). Empty: the citizen model, dressed with <see cref="Outfit"/>.</summary>
	public string[] Models = new string[0];
	/// <summary>Clothing (.clothing paths or "path@tint"). Null: the player's own s&box avatar. Empty: underwear only.</summary>
	public string[] Outfit;
	/// <summary>Weapon ids (see <c>DarkRP.RegisterWeapon</c>) or weapon prefab paths given on spawn.</summary>
	public string[] Weapons = new string[0];
	/// <summary>Ammo given on spawn, "type:amount" (types: pistol, smg1, rifle, buckshot, sniper or a .ammo path).</summary>
	public string[] Ammo = new string[0];
	public int Salary = 45;
	/// <summary>How many players may have this job at once. 0 = unlimited; between 0 and 1 = a share of the players.</summary>
	public float Max;
	public string Category = "Other";
	public int SortOrder = 100;
	/// <summary>Becoming this job needs a vote of the other players.</summary>
	public bool Vote;
	/// <summary>Members carry a gun license.</summary>
	public bool HasLicense;
	/// <summary>Members can be demoted with /demote.</summary>
	public bool CanDemote = true;
	/// <summary>0: everyone, 1: admins only (darkrp.admin permission), 2: the host only.</summary>
	public int Admin;
	/// <summary>Only players in one of these jobs (commands) can switch to this one.</summary>
	public string[] NeedToChangeFrom = new string[0];
	public float StartHealth = 100;
	public float StartArmor;
	/// <summary>Civil protection: may arrest, set wanted, request warrants.</summary>
	public bool Police;
	public bool Chief;
	public bool Mayor;
	public bool Medic;
	public bool Hobo;
	public bool Cook;
	public bool Hitman;
	/// <summary>Hidden from the menu (still reachable by command if allowed).</summary>
	public bool Hidden;
	/// <summary>Where the job came from: "darkrp", "config" (jobs.json) or an addon id.</summary>
	public string Source = "";
	/// <summary>Code only: extra check before joining (true = allowed). Runs on the host.</summary>
	public Func<Connection, bool> CustomCheck;
	public string CustomCheckFailMsg = "";
	/// <summary>Code only: called on the host after a member spawned and got their loadout.</summary>
	public Action<Connection> OnSpawn;
	/// <summary>Code only: called on the host when a member dies.</summary>
	public Action<Connection> OnDeath;
}

/// <summary>A shipment or single weapon sold in the menu. DarkRP.createShipment.</summary>
public sealed class RPShipment
{
	public int Index;
	public string Name = "";
	/// <summary>Weapon id (see <c>DarkRP.RegisterWeapon</c>) or a weapon prefab path.</summary>
	public string Entity = "";
	/// <summary>World model shown in the menu (empty: the weapon's own).</summary>
	public string Model = "";
	/// <summary>Price of the whole shipment.</summary>
	public int Price;
	/// <summary>Weapons in one shipment crate.</summary>
	public int Amount = 10;
	/// <summary>Also sold as a single weapon (Weapons tab) for <see cref="PriceSep"/>.</summary>
	public bool Separate;
	public int PriceSep;
	/// <summary>Only sold separately, never as a crate.</summary>
	public bool NoShip;
	/// <summary>Job commands allowed to buy it; empty = everyone.</summary>
	public string[] Allowed = new string[0];
	public string Category = "Other";
	public int SortOrder = 100;
	public bool Hidden;
	public string Source = "";
	public Func<Connection, bool> CustomCheck;
	public string CustomCheckFailMsg = "";
}

/// <summary>A buyable entity (money printer, drug lab...). DarkRP.createEntity.</summary>
public sealed class RPEntityDef
{
	public int Index;
	public string Name = "";
	/// <summary>Entity type id (see <c>DarkRP.RegisterEntityType</c>) or a prefab path.</summary>
	public string Ent = "";
	public string Model = "";
	public int Price;
	/// <summary>How many one player may own at once (0 = unlimited).</summary>
	public int Max = 2;
	/// <summary>Chat command that buys it: /buymoneyprinter.</summary>
	public string Cmd = "";
	public string[] Allowed = new string[0];
	public string Category = "Other";
	public int SortOrder = 100;
	public bool Hidden;
	public string Source = "";
	public Func<Connection, bool> CustomCheck;
	public string CustomCheckFailMsg = "";
}

/// <summary>Buyable ammo. DarkRP.createAmmoType.</summary>
public sealed class RPAmmoDef
{
	public int Index;
	/// <summary>DarkRP ammo type: pistol, smg1, rifle, buckshot, sniper (or a .ammo path).</summary>
	public string AmmoType = "pistol";
	public string Name = "";
	public string Model = "";
	public int Price = 30;
	public int Amount = 24;
	public string[] Allowed = new string[0];
	public string Category = "Other";
	public int SortOrder = 100;
	public string Source = "";
	public Func<Connection, bool> CustomCheck;
}

/// <summary>Food for the hunger module. DarkRP.createFood.</summary>
public sealed class RPFoodDef
{
	public int Index;
	public string Name = "";
	public string Model = "";
	/// <summary>Energy restored (0-100).</summary>
	public int Energy = 20;
	public int Price = 10;
	/// <summary>Only cooks can buy it (when a cook is online).</summary>
	public bool RequiresCook = true;
	public string Source = "";
}

/// <summary>Menu category. DarkRP.createCategory.</summary>
public sealed class RPCategory
{
	public string Name = "";
	/// <summary>jobs, entities, shipments, weapons, ammo or food.</summary>
	public string Categorises = "jobs";
	public string Color = "#3c3c3c";
	public bool StartExpanded = true;
	public int SortOrder = 100;
	public string Source = "";
}

/// <summary>A weapon id that jobs and shipments can refer to ("weapon_ak472" → its prefab).</summary>
public sealed class RPWeaponDef
{
	public string Id = "";
	public string Name = "";
	public string Prefab = "";
	public string Model = "";
	/// <summary>Needs a gun license to carry legally (weapon checker).</summary>
	public bool NeedsLicense = true;
	public string Source = "";
}

/// <summary>A spawnable entity type ("money_printer" → script component or prefab).</summary>
public sealed class RPEntityType
{
	public string Id = "";
	public string Name = "";
	/// <summary>Prefab to clone, or empty to build one from <see cref="Component"/> + <see cref="Model"/>.</summary>
	public string Prefab = "";
	/// <summary>Script component type added to the object (e.g. "RPMoneyPrinter").</summary>
	public string Component = "";
	public string Model = "";
	/// <summary>Model scale when built from a model.</summary>
	public float Scale = 1f;
	/// <summary>Players may pick it up with the pocket.</summary>
	public bool Pocketable = true;
	/// <summary>Prop protection (like DarkRP's FPP blocked entities): what the owner (and their buddies) may do to it,
	/// comma separated: "physgun", "toolgun", "pickup". Anything not listed needs a CanTouchEntity hook to allow it.
	/// Server owners can change it per type (propprotection.json "entityPermissions" or the spawn menu page).</summary>
	public string Touch = "pickup";
	public string Source = "";
}

/// <summary>Doors only usable by these jobs (police station...). AddDoorGroup.</summary>
public sealed class RPDoorGroup
{
	public string Name = "";
	public string[] Jobs = new string[0];
}

/// <summary>An agenda shown to the listed jobs, set by the managers. DarkRP.createAgenda.</summary>
public sealed class RPAgendaDef
{
	public string Title = "";
	public string[] Managers = new string[0];
	public string[] Listeners = new string[0];
}

/// <summary>A /g group chat between jobs. DarkRP.createGroupChat.</summary>
public sealed class RPGroupChat
{
	/// <summary>Id for darkrp/disabled_defaults.json (the jobs joined by commas when empty).</summary>
	public string Name = "";
	public string[] Jobs = new string[0];
	/// <summary>Code only: who may read/send (overrides <see cref="Jobs"/>).</summary>
	public Func<Connection, bool> Check;
}

/// <summary>Jobs that share a demotion ban. DarkRP.createDemoteGroup.</summary>
public sealed class RPDemoteGroup
{
	public string Name = "";
	public string[] Jobs = new string[0];
}

/// <summary>A chat command (/give, /job...).</summary>
public sealed class RPCommandDef
{
	public string Name = "";
	public string Description = "";
	/// <summary>"" (everyone), "admin" or any permission string.</summary>
	public string Permission = "";
	/// <summary>Seconds between uses per player.</summary>
	public float Delay;
	public Action<RPCmd> Handler;
	public string Source = "";
}

/// <summary>The arguments of a running chat command.</summary>
public sealed class RPCmd
{
	public Connection Caller;
	public string Command = "";
	public string[] Args = new string[0];
	/// <summary>Everything after the command, unsplit.</summary>
	public string ArgString = "";

	public string Arg( int i ) => i < Args.Length ? Args[i] : "";
	public int ArgCount => Args.Length;
	/// <summary>Everything from argument <paramref name="i"/> on.</summary>
	public string Rest( int i )
	{
		if ( i >= Args.Length ) return "";
		var parts = new List<string>();
		for ( int k = i; k < Args.Length; k++ ) parts.Add( Args[k] );
		return string.Join( " ", parts );
	}
	public void Reply( string text ) => DarkRP.Notify( Caller, 0, 4, text );
	public void Error( string text ) => DarkRP.Notify( Caller, 1, 4, text );
}

/// <summary>
/// Data passed to DarkRP hooks (<c>DarkRP.AddHook</c>). Which fields are set depends on the hook (see Docs/API.md).
/// "can..." hooks start with <see cref="Allowed"/> = true: call <see cref="Deny"/> to block.
/// </summary>
public sealed class RPHook
{
	public string Name = "";
	/// <summary>The player the hook is about (GMod's ply).</summary>
	public Connection Player;
	/// <summary>A second player (target, victim, buyer...).</summary>
	public Connection Target;
	public RPJob Job;
	public RPJob OldJob;
	public RPShipment Shipment;
	public RPEntityDef EntityDef;
	public long Amount;
	public string Text = "";
	public string Reason = "";
	public GameObject Entity;
	public bool Allowed = true;
	/// <summary>Set to stop later hooks from running.</summary>
	public bool Handled;
	public object Data;

	public void Deny( string reason )
	{
		Allowed = false;
		if ( !string.IsNullOrEmpty( reason ) ) Reason = reason;
	}
}

public sealed class RPHookEntry
{
	public string Id = "";
	public Action<RPHook> Fn;
}

/// <summary>A callback that registers custom content ("loadCustomDarkRPItems").</summary>
public sealed class RPLoader
{
	public Scene Scene;
	public string Id = "";
	public Action Fn;
}

/// <summary>One config setting (darkrp/config.json, the admin menu and DarkRP.Config()).</summary>
public sealed class RPSetting
{
	public string Key = "";
	/// <summary>bool, int, float or string.</summary>
	public string Kind = "int";
	public string Default = "";
	public string Value = "";
	public string Group = "General";
	public string Help = "";
	/// <summary>Clients get it too (HUD, menu).</summary>
	public bool Shared;
}
