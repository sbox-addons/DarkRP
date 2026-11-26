// DarkRP for s&box - the content registry and the public DarkRP API.
// Based on DarkRP's createJob/createShipment/createEntity/hook system (MIT, Falco Peijnenburg).

/// <summary>
/// Everything DarkRP knows in one scene: config, jobs, shipments, entities, ammo, food, categories, commands and hooks.
/// Rebuilt for every scene (play start, map change), so addon callbacks never outlive a script reload.
/// </summary>
public sealed class RPRegistry
{
	public Scene Scene;
	public RPConfig Config = new RPConfig();

	public readonly List<RPJob> Jobs = new List<RPJob>();
	public readonly List<RPShipment> Shipments = new List<RPShipment>();
	public readonly List<RPEntityDef> Entities = new List<RPEntityDef>();
	public readonly List<RPAmmoDef> Ammo = new List<RPAmmoDef>();
	public readonly List<RPFoodDef> Food = new List<RPFoodDef>();
	public readonly List<RPCategory> Categories = new List<RPCategory>();
	public readonly Dictionary<string, RPWeaponDef> Weapons = new Dictionary<string, RPWeaponDef>();
	public readonly Dictionary<string, RPEntityType> EntityTypes = new Dictionary<string, RPEntityType>();
	public readonly List<RPDoorGroup> DoorGroups = new List<RPDoorGroup>();
	public readonly List<RPAgendaDef> Agendas = new List<RPAgendaDef>();
	public readonly List<RPGroupChat> GroupChats = new List<RPGroupChat>();
	public readonly List<RPDemoteGroup> DemoteGroups = new List<RPDemoteGroup>();
	public readonly List<string> DefaultLaws = new List<string>();
	public readonly Dictionary<string, RPCommandDef> Commands = new Dictionary<string, RPCommandDef>();
	public readonly Dictionary<string, List<RPHookEntry>> Hooks = new Dictionary<string, List<RPHookEntry>>();
	/// <summary>Weapons every player gets regardless of job (keys, pocket, physgun, toolgun...).</summary>
	public readonly List<string> DefaultWeapons = new List<string>();
	/// <summary>Content ids disabled by darkrp/disabled.json or code (kind:id, e.g. "job:hobo").</summary>
	public readonly List<string> Disabled = new List<string>();
	/// <summary>DarkRP modules switched off in darkrp/disabled_defaults.json ("hungermod", "hitmenu", "hud", "propprotection").</summary>
	public readonly List<string> DisabledModules = new List<string>();

	public bool Built;
	public bool Building;
	/// <summary>Bumped whenever content changes (menus rebuild).</summary>
	public int Version;
	/// <summary>Tag for content registered right now ("darkrp", "config" or an addon id).</summary>
	public string CurrentSource = "darkrp";
	public readonly List<string> LoadErrors = new List<string>();

	public void Clear()
	{
		Config = new RPConfig();
		Jobs.Clear(); Shipments.Clear(); Entities.Clear(); Ammo.Clear(); Food.Clear(); Categories.Clear();
		Weapons.Clear(); EntityTypes.Clear(); DoorGroups.Clear(); Agendas.Clear(); GroupChats.Clear(); DemoteGroups.Clear();
		DefaultLaws.Clear(); Commands.Clear(); Hooks.Clear(); DefaultWeapons.Clear(); Disabled.Clear(); DisabledModules.Clear(); LoadErrors.Clear();
		Built = false;
	}

	// ---- lookups ----

	public RPJob Job( string command )
	{
		if ( string.IsNullOrEmpty( command ) ) return null;
		for ( int i = 0; i < Jobs.Count; i++ )
			if ( string.Equals( Jobs[i].Command, command, StringComparison.OrdinalIgnoreCase ) ) return Jobs[i];
		return null;
	}

	public RPJob JobByIndex( int index )
	{
		for ( int i = 0; i < Jobs.Count; i++ ) if ( Jobs[i].Index == index ) return Jobs[i];
		return null;
	}

	public RPJob DefaultJob()
	{
		var j = Job( Config.Str( "defaultjob" ) );
		return j ?? (Jobs.Count > 0 ? Jobs[0] : null);
	}

	public RPShipment Shipment( string name )
	{
		for ( int i = 0; i < Shipments.Count; i++ )
			if ( string.Equals( Shipments[i].Name, name, StringComparison.OrdinalIgnoreCase ) ) return Shipments[i];
		return null;
	}

	public RPEntityDef EntityDef( string cmdOrName )
	{
		for ( int i = 0; i < Entities.Count; i++ )
			if ( string.Equals( Entities[i].Cmd, cmdOrName, StringComparison.OrdinalIgnoreCase ) || string.Equals( Entities[i].Name, cmdOrName, StringComparison.OrdinalIgnoreCase ) ) return Entities[i];
		return null;
	}

	public RPCategory Category( string name, string kind )
	{
		for ( int i = 0; i < Categories.Count; i++ )
			if ( Categories[i].Categorises == kind && string.Equals( Categories[i].Name, name, StringComparison.OrdinalIgnoreCase ) ) return Categories[i];
		return null;
	}

	public RPWeaponDef Weapon( string id )
	{
		if ( string.IsNullOrEmpty( id ) ) return null;
		var key = id.ToLowerInvariant();
		return Weapons.ContainsKey( key ) ? Weapons[key] : null;
	}

	/// <summary>A weapon id or prefab path → the prefab path to give.</summary>
	public string WeaponPrefab( string idOrPath )
	{
		var w = Weapon( idOrPath );
		if ( w != null ) return w.Prefab;
		return idOrPath;
	}

	public string WeaponName( string idOrPath )
	{
		var w = Weapon( idOrPath );
		if ( w != null && !string.IsNullOrEmpty( w.Name ) ) return w.Name;
		return idOrPath;
	}

	public RPEntityType EntityType( string id )
	{
		if ( string.IsNullOrEmpty( id ) ) return null;
		var key = id.ToLowerInvariant();
		return EntityTypes.ContainsKey( key ) ? EntityTypes[key] : null;
	}

	public bool IsDisabled( string kind, string id ) => Disabled.Contains( kind + ":" + (id ?? "").ToLowerInvariant() );

	public bool ModuleDisabled( string module ) => DisabledModules.Contains( (module ?? "").ToLowerInvariant() );

	// ---- registration ----

	public RPJob AddJob( RPJob job )
	{
		if ( job == null || string.IsNullOrWhiteSpace( job.Command ) ) { LoadErrors.Add( "Job without command: " + (job != null ? job.Name : "null") ); return null; }
		job.Command = job.Command.Trim().ToLowerInvariant();
		if ( string.IsNullOrEmpty( job.Name ) ) job.Name = job.Command;
		if ( string.IsNullOrEmpty( job.Source ) ) job.Source = CurrentSource;
		for ( int i = 0; i < Jobs.Count; i++ )
		{
			if ( Jobs[i].Command == job.Command ) { Jobs[i] = job; Version++; return job; }
		}
		Jobs.Add( job );
		Version++;
		return job;
	}

	public RPShipment AddShipment( RPShipment s )
	{
		if ( s == null || string.IsNullOrWhiteSpace( s.Name ) ) return null;
		if ( string.IsNullOrEmpty( s.Source ) ) s.Source = CurrentSource;
		for ( int i = 0; i < Shipments.Count; i++ )
			if ( string.Equals( Shipments[i].Name, s.Name, StringComparison.OrdinalIgnoreCase ) ) { Shipments[i] = s; Version++; return s; }
		Shipments.Add( s );
		Version++;
		return s;
	}

	public RPEntityDef AddEntity( RPEntityDef e )
	{
		if ( e == null || string.IsNullOrWhiteSpace( e.Name ) ) return null;
		if ( string.IsNullOrEmpty( e.Cmd ) ) e.Cmd = "buy" + RPUtil.Safe( e.Name ).Replace( "_", "" );
		e.Cmd = e.Cmd.TrimStart( '/' ).ToLowerInvariant();
		if ( string.IsNullOrEmpty( e.Source ) ) e.Source = CurrentSource;
		for ( int i = 0; i < Entities.Count; i++ )
			if ( Entities[i].Cmd == e.Cmd ) { Entities[i] = e; Version++; return e; }
		Entities.Add( e );
		Version++;
		return e;
	}

	public RPAmmoDef AddAmmo( RPAmmoDef a )
	{
		if ( a == null ) return null;
		if ( string.IsNullOrEmpty( a.Name ) ) a.Name = a.AmmoType;
		if ( string.IsNullOrEmpty( a.Source ) ) a.Source = CurrentSource;
		for ( int i = 0; i < Ammo.Count; i++ )
			if ( string.Equals( Ammo[i].Name, a.Name, StringComparison.OrdinalIgnoreCase ) ) { Ammo[i] = a; Version++; return a; }
		Ammo.Add( a );
		Version++;
		return a;
	}

	public RPFoodDef AddFood( RPFoodDef f )
	{
		if ( f == null || string.IsNullOrWhiteSpace( f.Name ) ) return null;
		if ( string.IsNullOrEmpty( f.Source ) ) f.Source = CurrentSource;
		for ( int i = 0; i < Food.Count; i++ )
			if ( string.Equals( Food[i].Name, f.Name, StringComparison.OrdinalIgnoreCase ) ) { Food[i] = f; Version++; return f; }
		Food.Add( f );
		Version++;
		return f;
	}

	public RPCategory AddCategory( RPCategory c )
	{
		if ( c == null || string.IsNullOrWhiteSpace( c.Name ) ) return null;
		if ( string.IsNullOrEmpty( c.Source ) ) c.Source = CurrentSource;
		for ( int i = 0; i < Categories.Count; i++ )
			if ( Categories[i].Categorises == c.Categorises && string.Equals( Categories[i].Name, c.Name, StringComparison.OrdinalIgnoreCase ) ) { Categories[i] = c; Version++; return c; }
		Categories.Add( c );
		Version++;
		return c;
	}

	public RPWeaponDef AddWeapon( RPWeaponDef w )
	{
		if ( w == null || string.IsNullOrWhiteSpace( w.Id ) ) return null;
		w.Id = w.Id.ToLowerInvariant();
		if ( string.IsNullOrEmpty( w.Source ) ) w.Source = CurrentSource;
		Weapons[w.Id] = w;
		return w;
	}

	public RPEntityType AddEntityType( RPEntityType t )
	{
		if ( t == null || string.IsNullOrWhiteSpace( t.Id ) ) return null;
		t.Id = t.Id.ToLowerInvariant();
		if ( string.IsNullOrEmpty( t.Source ) ) t.Source = CurrentSource;
		EntityTypes[t.Id] = t;
		return t;
	}

	public void AddCommand( RPCommandDef c )
	{
		if ( c == null || string.IsNullOrWhiteSpace( c.Name ) ) return;
		c.Name = c.Name.TrimStart( '/', '!' ).ToLowerInvariant();
		if ( string.IsNullOrEmpty( c.Source ) ) c.Source = CurrentSource;
		Commands[c.Name] = c;
	}

	public void AddHook( string name, string id, Action<RPHook> fn )
	{
		if ( string.IsNullOrEmpty( name ) || fn == null ) return;
		if ( !Hooks.ContainsKey( name ) ) Hooks[name] = new List<RPHookEntry>();
		var list = Hooks[name];
		for ( int i = 0; i < list.Count; i++ )
			if ( list[i].Id == id ) { list[i].Fn = fn; return; }
		list.Add( new RPHookEntry { Id = id ?? "", Fn = fn } );
	}

	public void RemoveHook( string name, string id )
	{
		if ( !Hooks.ContainsKey( name ) ) return;
		var list = Hooks[name];
		for ( int i = list.Count - 1; i >= 0; i-- ) if ( list[i].Id == id ) list.RemoveAt( i );
	}

	public RPHook RunHook( RPHook h )
	{
		if ( h == null || !Hooks.ContainsKey( h.Name ) ) return h;
		var list = Hooks[h.Name];
		for ( int i = 0; i < list.Count; i++ )
		{
			try { list[i].Fn( h ); }
			catch ( Exception e ) { Log.Warning( "DarkRP hook " + h.Name + " (" + list[i].Id + ") failed: " + e.Message ); }
			if ( h.Handled ) break;
		}
		return h;
	}

	// ---- building ----

	/// <summary>Defaults, built-in modules, the data-folder config (host), then addon loaders. Called once per scene.</summary>
	public void Build()
	{
		if ( Building ) return;
		Building = true;
		Clear();
		try
		{
			CurrentSource = "darkrp";
			RPDefaults.Register( this );
			RPModules.Register( this );
			if ( Networking.IsHost || !Networking.IsActive )
			{
				// darkrp/disabled_defaults.json removes DarkRP's own defaults before custom content is added (as in DarkRP).
				RPDisabledDefaults.Apply( this );
				CurrentSource = "config";
				RPDataFiles.LoadAll( this );
			}
			foreach ( var loader in RPRegistries.LoadersFor( Scene ) )
			{
				CurrentSource = loader.Id;
				try { loader.Fn(); }
				catch ( Exception e ) { LoadErrors.Add( "Addon " + loader.Id + ": " + e.Message ); Log.Warning( "DarkRP: addon loader " + loader.Id + " failed: " + e.Message ); }
			}
			CurrentSource = "darkrp";
			ApplyDisabled();
			Finish();
			if ( Networking.IsHost || !Networking.IsActive ) RPDataFiles.WriteReference( this );
		}
		finally
		{
			Building = false;
			Built = true;
			Version++;
		}
		foreach ( var err in LoadErrors ) Log.Warning( "DarkRP: " + err );
	}

	void ApplyDisabled()
	{
		for ( int i = Jobs.Count - 1; i >= 0; i-- ) if ( IsDisabled( "job", Jobs[i].Command ) ) Jobs.RemoveAt( i );
		for ( int i = Shipments.Count - 1; i >= 0; i-- ) if ( IsDisabled( "shipment", Shipments[i].Name ) ) Shipments.RemoveAt( i );
		for ( int i = Entities.Count - 1; i >= 0; i-- ) if ( IsDisabled( "entity", Entities[i].Cmd ) || IsDisabled( "entity", Entities[i].Name ) ) Entities.RemoveAt( i );
		for ( int i = Ammo.Count - 1; i >= 0; i-- ) if ( IsDisabled( "ammo", Ammo[i].Name ) ) Ammo.RemoveAt( i );
		for ( int i = Food.Count - 1; i >= 0; i-- ) if ( IsDisabled( "food", Food[i].Name ) ) Food.RemoveAt( i );
		var cmds = Commands.Keys.ToList();
		foreach ( var c in cmds ) if ( IsDisabled( "command", c ) ) Commands.Remove( c );
		ApplyModules();
	}

	/// <summary>What a disabled module takes away (the module's content and commands).</summary>
	public void ApplyModules()
	{
		if ( ModuleDisabled( "hungermod" ) )
		{
			Config.Set( "hungermod", "false" );
			Jobs.RemoveAll( j => j.Cook );
			Food.Clear();
			Entities.RemoveAll( e => e.Ent == "microwave" );
			foreach ( var c in new[] { "buyfood" } ) Commands.Remove( c );
		}
		if ( ModuleDisabled( "hitmenu" ) )
		{
			foreach ( var j in Jobs ) j.Hitman = false;
			foreach ( var c in new[] { "hit" } ) Commands.Remove( c );
		}
		if ( ModuleDisabled( "propprotection" ) )
		{
			foreach ( var c in new[] { "buddy", "buddies", "ppcleanup", "ppblock", "ppunblock" } ) Commands.Remove( c );
		}
	}

	/// <summary>Assigns team numbers, creates missing categories and sorts everything for the menu.</summary>
	public void Finish()
	{
		EnsureCategories( "jobs", Jobs.Select( j => j.Category ) );
		EnsureCategories( "shipments", Shipments.Where( s => !s.NoShip ).Select( s => s.Category ) );
		EnsureCategories( "weapons", Shipments.Where( s => s.Separate || s.NoShip ).Select( s => s.Category ) );
		EnsureCategories( "entities", Entities.Select( e => e.Category ) );
		EnsureCategories( "ammo", Ammo.Select( a => a.Category ) );

		var jobs = Jobs.OrderBy( j => CategoryOrder( j.Category, "jobs" ) ).ThenBy( j => j.SortOrder ).ToList();
		Jobs.Clear();
		Jobs.AddRange( jobs );
		for ( int i = 0; i < Jobs.Count; i++ ) Jobs[i].Index = i + 1;

		var ships = Shipments.OrderBy( s => s.SortOrder ).ToList();
		Shipments.Clear(); Shipments.AddRange( ships );
		for ( int i = 0; i < Shipments.Count; i++ ) Shipments[i].Index = i;
		var ents = Entities.OrderBy( e => e.SortOrder ).ToList();
		Entities.Clear(); Entities.AddRange( ents );
		for ( int i = 0; i < Entities.Count; i++ ) Entities[i].Index = i;
		for ( int i = 0; i < Ammo.Count; i++ ) Ammo[i].Index = i;
		for ( int i = 0; i < Food.Count; i++ ) Food[i].Index = i;
		var cats = Categories.OrderBy( c => c.SortOrder ).ToList();
		Categories.Clear(); Categories.AddRange( cats );
	}

	void EnsureCategories( string kind, IEnumerable<string> names )
	{
		foreach ( var n in names.ToList() )
		{
			var name = string.IsNullOrEmpty( n ) ? "Other" : n;
			if ( Category( name, kind ) == null ) Categories.Add( new RPCategory { Name = name, Categorises = kind, SortOrder = name == "Other" ? 255 : 150, Source = "auto" } );
		}
	}

	int CategoryOrder( string name, string kind )
	{
		var c = Category( string.IsNullOrEmpty( name ) ? "Other" : name, kind );
		return c != null ? c.SortOrder : 255;
	}
}

public sealed class RPRegistryHolder
{
	public readonly Dictionary<Scene, RPRegistry> ByScene = new Dictionary<Scene, RPRegistry>();
	public readonly List<RPLoader> Loaders = new List<RPLoader>();
}

public sealed class RPRegistries
{
	static readonly RPRegistryHolder H = new RPRegistryHolder();

	/// <summary>The registry of a scene (created on first use; built on first use too).</summary>
	public static RPRegistry For( Scene scene )
	{
		if ( scene == null ) return null;
		if ( H.ByScene.ContainsKey( scene ) ) return H.ByScene[scene];
		Prune();
		var r = new RPRegistry { Scene = scene };
		H.ByScene[scene] = r;
		return r;
	}

	public static RPRegistry Current()
	{
		var r = For( Game.ActiveScene );
		if ( r != null && !r.Built && !r.Building ) r.Build();
		return r;
	}

	public static void Forget( Scene scene )
	{
		if ( scene != null && H.ByScene.ContainsKey( scene ) ) H.ByScene.Remove( scene );
		for ( int i = H.Loaders.Count - 1; i >= 0; i-- ) if ( H.Loaders[i].Scene == scene ) H.Loaders.RemoveAt( i );
	}

	public static void AddLoader( Scene scene, string id, Action fn )
	{
		for ( int i = H.Loaders.Count - 1; i >= 0; i-- )
			if ( H.Loaders[i].Scene == scene && H.Loaders[i].Id == id ) H.Loaders.RemoveAt( i );
		H.Loaders.Add( new RPLoader { Scene = scene, Id = id, Fn = fn } );
		// Already built (the addon's system came after ours): rebuild so its content is included.
		if ( H.ByScene.ContainsKey( scene ) && H.ByScene[scene].Built ) H.ByScene[scene].Built = false;
	}

	public static List<RPLoader> LoadersFor( Scene scene )
	{
		var list = new List<RPLoader>();
		for ( int i = 0; i < H.Loaders.Count; i++ ) if ( H.Loaders[i].Scene == scene ) list.Add( H.Loaders[i] );
		return list;
	}

	static void Prune()
	{
		var dead = new List<Scene>();
		foreach ( var s in H.ByScene.Keys ) if ( !s.IsValid() ) dead.Add( s );
		foreach ( var s in dead ) Forget( s );
	}
}

/// <summary>
/// The public DarkRP API for other addons and custom content. Register content from a callback given to
/// <see cref="OnLoad"/>; everything else (money, jobs, notify...) works at any time on the host.
/// See Docs/API.md.
/// </summary>
public sealed class DarkRP
{
	public const string Version = "1.0.0";

	/// <summary>Admin permission for DarkRP admin commands (admins and the host always have it).</summary>
	public const string AdminPermission = "darkrp.admin";

	public static RPRegistry Registry() => RPRegistries.Current();
	public static RPConfig Config() { var r = Registry(); return r != null ? r.Config : null; }

	/// <summary>
	/// Registers a callback that adds your content (jobs, shipments, hooks, commands...). It runs every time DarkRP builds
	/// its content for a scene, after the defaults and darkrp/*.json. Call it from your GameObjectSystem's constructor.
	/// </summary>
	public static void OnLoad( Scene scene, string addonId, Action register ) => RPRegistries.AddLoader( scene, addonId, register );

	// ---- content ----
	public static RPJob CreateJob( string name, RPJob job ) { job.Name = name; return Registry().AddJob( job ); }
	public static RPShipment CreateShipment( string name, RPShipment s ) { s.Name = name; return Registry().AddShipment( s ); }
	public static RPEntityDef CreateEntity( string name, RPEntityDef e ) { e.Name = name; return Registry().AddEntity( e ); }
	public static RPAmmoDef CreateAmmoType( string ammoType, RPAmmoDef a ) { a.AmmoType = ammoType; return Registry().AddAmmo( a ); }
	public static RPFoodDef CreateFood( string name, RPFoodDef f ) { f.Name = name; return Registry().AddFood( f ); }
	public static RPCategory CreateCategory( RPCategory c ) => Registry().AddCategory( c );
	public static RPWeaponDef RegisterWeapon( string id, string prefab, string name ) => Registry().AddWeapon( new RPWeaponDef { Id = id, Prefab = prefab, Name = name } );
	public static RPWeaponDef RegisterWeapon( RPWeaponDef w ) => Registry().AddWeapon( w );
	public static RPEntityType RegisterEntityType( RPEntityType t ) => Registry().AddEntityType( t );
	public static void AddDoorGroup( string name, string[] jobCommands ) => Registry().DoorGroups.Add( new RPDoorGroup { Name = name, Jobs = jobCommands } );
	public static void CreateAgenda( string title, string[] managers, string[] listeners ) => Registry().Agendas.Add( new RPAgendaDef { Title = title, Managers = managers, Listeners = listeners } );
	public static void CreateGroupChat( string[] jobCommands ) => Registry().GroupChats.Add( new RPGroupChat { Jobs = jobCommands } );
	public static void CreateGroupChat( Func<Connection, bool> check ) => Registry().GroupChats.Add( new RPGroupChat { Check = check } );
	public static void CreateDemoteGroup( string name, string[] jobCommands ) => Registry().DemoteGroups.Add( new RPDemoteGroup { Name = name, Jobs = jobCommands } );
	public static void AddDefaultWeapon( string weaponId ) { if ( !Registry().DefaultWeapons.Contains( weaponId ) ) Registry().DefaultWeapons.Add( weaponId ); }
	public static void AddDefaultLaw( string law ) => Registry().DefaultLaws.Add( law );

	/// <summary>Removes content by kind ("job", "shipment", "entity", "ammo", "food", "command") and id.</summary>
	public static void Disable( string kind, string id ) => Registry().Disabled.Add( kind + ":" + (id ?? "").ToLowerInvariant() );

	/// <summary>Switches off a DarkRP module from code ("hungermod", "hitmenu", "hud", "propprotection"), like
	/// darkrp/disabled_defaults.json does.</summary>
	public static void DisableModule( string module ) { var r = Registry(); if ( !r.ModuleDisabled( module ) ) r.DisabledModules.Add( module.ToLowerInvariant() ); }

	/// <summary>Prop protection, DarkRP's CPPI: may the player do <paramref name="what"/> ("physgun", "toolgun",
	/// "pickup", "damage") to the object? Owners, buddies and admins may; DarkRP entities follow their type's Touch.</summary>
	public static bool CanTouch( Connection c, GameObject go, string what ) => RPPropProtection.CanTouch( c, go, what );

	/// <summary>Prop protection: the SteamId of an object's owner, 0 for the map.</summary>
	public static long GetOwner( GameObject go ) => RPPropProtection.OwnerOf( go );

	/// <summary>True when a DarkRP module is switched off (host and clients).</summary>
	public static bool ModuleDisabled( string module ) { var r = Registry(); return r != null && r.ModuleDisabled( module ); }

	public static RPJob GetJob( string command ) => Registry().Job( command );
	public static List<RPJob> Jobs() => Registry().Jobs;

	// ---- hooks and commands ----
	public static void AddHook( string hook, string id, Action<RPHook> fn ) => Registry().AddHook( hook, id, fn );
	public static void RemoveHook( string hook, string id ) => Registry().RemoveHook( hook, id );
	public static RPHook RunHook( RPHook h ) => Registry() != null ? Registry().RunHook( h ) : h;

	public static void AddChatCommand( string name, string description, Action<RPCmd> handler ) =>
		Registry().AddCommand( new RPCommandDef { Name = name, Description = description, Handler = handler } );

	public static void AddChatCommand( string name, string description, string permission, float delay, Action<RPCmd> handler ) =>
		Registry().AddCommand( new RPCommandDef { Name = name, Description = description, Permission = permission, Delay = delay, Handler = handler } );

	// ---- players (host) ----
	public static RPPlayer GetPlayer( Connection c ) => RP.Of( c );
	public static long GetMoney( Connection c ) => RP.Money( c );
	public static bool CanAfford( Connection c, long amount ) => RP.CanAfford( c, amount );
	public static void AddMoney( Connection c, long amount ) => RP.AddMoney( c, amount );
	public static RPJob GetPlayerJob( Connection c ) => RP.Job( c );
	public static bool ChangeJob( Connection c, string command, bool force ) => RP.ChangeTeam( c, Registry().Job( command ), force, false );
	public static bool IsCP( Connection c ) => RP.IsCP( c );
	public static string GetName( Connection c ) => RP.Name( c );

	// ---- feedback ----
	/// <summary>A notice for one player. Type 0 = info, 1 = error, 2 = undo, 3 = hint, 4 = cleanup (GMod NOTIFY_*).</summary>
	public static void Notify( Connection c, int type, float seconds, string message ) => RPNet.Notify( c, type, seconds, message );
	public static void NotifyAll( int type, float seconds, string message ) => RPNet.NotifyAll( type, seconds, message );
	public static void ChatTo( Connection c, string text, string color ) => RPNet.Chat( c, text, color );
	public static void ChatAll( string text, string color ) => RPNet.ChatAll( text, color );
	public static string FormatMoney( long amount ) => RPUtil.Money( amount );
	public static void LogAction( string text ) => RPLog.Write( text );
}
