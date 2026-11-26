// DarkRP for s&box - prop protection, in the spirit of DarkRP's Falco's Prop Protection (FPP, MIT, Falco Peijnenburg):
// only a prop's owner (and the buddies they pick) may physgun, tool, pick up or break it; map props are for admins;
// props can't kill players; blocked models; cloud/mounted content only for admins unless allowed; spawn delay and size
// limit; disconnected players' props are kept protected and removed after a delay (given back if they return).
// Settings: Spawn menu > Utilities > Server > Prop Protection, or data/darkrp/propprotection.json. Switched off as a whole
// with the "propprotection" module in darkrp/disabled_defaults.json.
using System.Text.Json.Nodes;

/// <summary>Prop protection settings (data/darkrp/propprotection.json).</summary>
public sealed class RPPPSettings
{
	public bool Enabled = true;
	public bool Physgun = true;
	public bool Toolgun = true;
	public bool Pickup = true;
	public bool PropDamage = true;
	public bool AntiPropKill = true;
	public bool WorldProps = true;
	public bool CloudProps = false;
	public bool CloudEntities = false;
	public bool MountedContent = false;
	public bool BlockedModelsForAdmins = false;
	public int MaxPropSize = 0;
	public float SpawnDelay = 0.5f;
	public int CleanupDelay = 120;
	public bool LogSpawns = true;
	public bool AdminsTouchEntities = true;
	public List<string> BlockedModels = RPPPSettings.DefaultBlocked();
	/// <summary>Per DarkRP entity type: what owners may do to it ("physgun,toolgun,pickup"). Changes from the menu,
	/// then the file, then the type's own Touch.</summary>
	public readonly Dictionary<string, string> EntityTouch = new Dictionary<string, string>();
	public JsonObject EntityNode;

	public string EntityPerms( string typeId )
	{
		var id = (typeId ?? "").ToLowerInvariant();
		if ( EntityTouch.ContainsKey( id ) ) return EntityTouch[id];
		if ( EntityNode != null && RPJson.HasKey( EntityNode, id ) ) return RPPPSettings.CleanPerms( RPJson.Str( EntityNode, id, "" ) );
		var reg = DarkRP.Registry();
		var t = reg != null ? reg.EntityType( id ) : null;
		return t != null ? RPPPSettings.CleanPerms( t.Touch ) : "pickup";
	}

	public static bool HasPerm( string perms, string what ) => ("," + perms + ",").Contains( "," + what + "," );

	/// <summary>"Physgun, PICKUP x" → "physgun,pickup" (known actions only, in order).</summary>
	public static string CleanPerms( string perms )
	{
		var p = "," + (perms ?? "").ToLowerInvariant().Replace( " ", "," ) + ",";
		var list = new List<string>();
		foreach ( var a in new string[] { "physgun", "toolgun", "pickup" } ) if ( p.Contains( "," + a + "," ) ) list.Add( a );
		return string.Join( ",", list );
	}

	public static List<string> DefaultBlocked() => new List<string>
	{
		"models/sbox/warehouse/oil_drums/oil_drum_explosive.vmdl",
		"models/sbox/construction/dynamite/dynamite.vmdl",
	};

	public string ToJson( bool pretty )
	{
		var w = new RPJsonWriter { Pretty = pretty };
		w.BeginObject();
		if ( pretty ) w.Str( "_readme", "DarkRP prop protection. Change it in the spawn menu (Utilities > Server > Prop Protection) or here (restart the map). Every setting is explained in darkrp/README.txt." );
		w.Bool( "enabled", Enabled ).Bool( "physgun", Physgun ).Bool( "toolgun", Toolgun ).Bool( "pickup", Pickup );
		w.Bool( "propDamage", PropDamage ).Bool( "antiPropKill", AntiPropKill ).Bool( "worldProps", WorldProps );
		w.Bool( "cloudProps", CloudProps ).Bool( "cloudEntities", CloudEntities ).Bool( "mountedContent", MountedContent );
		w.Bool( "blockedModelsForAdmins", BlockedModelsForAdmins ).Num( "maxPropSize", MaxPropSize ).Flt( "spawnDelay", SpawnDelay );
		w.Num( "cleanupDelay", CleanupDelay ).Bool( "logSpawns", LogSpawns ).Bool( "adminsTouchEntities", AdminsTouchEntities );
		w.StrArray( "blockedModels", BlockedModels.ToArray() );
		w.BeginObject( "entityPermissions" );
		var reg = DarkRP.Registry();
		var ids = reg != null ? reg.EntityTypes.Keys.OrderBy( x => x ).ToList() : EntityTouch.Keys.ToList();
		foreach ( var id in ids ) w.Str( id, EntityPerms( id ) );
		w.EndObject();
		w.EndObject();
		return w.ToString();
	}

	public static RPPPSettings FromJson( string json )
	{
		var s = new RPPPSettings();
		var o = RPJson.Parse( json );
		if ( o == null ) return s;
		s.Enabled = RPJson.Bool( o, "enabled", s.Enabled );
		s.Physgun = RPJson.Bool( o, "physgun", s.Physgun );
		s.Toolgun = RPJson.Bool( o, "toolgun", s.Toolgun );
		s.Pickup = RPJson.Bool( o, "pickup", s.Pickup );
		s.PropDamage = RPJson.Bool( o, "propDamage", s.PropDamage );
		s.AntiPropKill = RPJson.Bool( o, "antiPropKill", s.AntiPropKill );
		s.WorldProps = RPJson.Bool( o, "worldProps", s.WorldProps );
		s.CloudProps = RPJson.Bool( o, "cloudProps", s.CloudProps );
		s.CloudEntities = RPJson.Bool( o, "cloudEntities", s.CloudEntities );
		s.MountedContent = RPJson.Bool( o, "mountedContent", s.MountedContent );
		s.BlockedModelsForAdmins = RPJson.Bool( o, "blockedModelsForAdmins", s.BlockedModelsForAdmins );
		s.MaxPropSize = RPJson.Int( o, "maxPropSize", s.MaxPropSize );
		s.SpawnDelay = RPJson.Flt( o, "spawnDelay", s.SpawnDelay );
		s.CleanupDelay = RPJson.Int( o, "cleanupDelay", s.CleanupDelay );
		s.LogSpawns = RPJson.Bool( o, "logSpawns", s.LogSpawns );
		s.AdminsTouchEntities = RPJson.Bool( o, "adminsTouchEntities", s.AdminsTouchEntities );
		s.EntityNode = RPJson.Obj( o, "entityPermissions" );
		if ( RPJson.HasKey( o, "blockedModels" ) ) s.BlockedModels = RPJson.StrArray( o, "blockedModels", new string[0] ).ToList();
		return s;
	}

	/// <summary>Changes one setting by its JSON key; false if the key or value is bad.</summary>
	public bool Set( string key, string value )
	{
		var v = (value ?? "").Trim().ToLowerInvariant();
		bool b = v == "true" || v == "1" || v == "on" || v == "yes";
		bool isBool = b || v == "false" || v == "0" || v == "off" || v == "no";
		float f = RPUtil.ParseOrNaN( v );
		if ( key == "enabled" && isBool ) Enabled = b;
		else if ( key == "physgun" && isBool ) Physgun = b;
		else if ( key == "toolgun" && isBool ) Toolgun = b;
		else if ( key == "pickup" && isBool ) Pickup = b;
		else if ( key == "propDamage" && isBool ) PropDamage = b;
		else if ( key == "antiPropKill" && isBool ) AntiPropKill = b;
		else if ( key == "worldProps" && isBool ) WorldProps = b;
		else if ( key == "cloudProps" && isBool ) CloudProps = b;
		else if ( key == "cloudEntities" && isBool ) CloudEntities = b;
		else if ( key == "mountedContent" && isBool ) MountedContent = b;
		else if ( key == "blockedModelsForAdmins" && isBool ) BlockedModelsForAdmins = b;
		else if ( key == "logSpawns" && isBool ) LogSpawns = b;
		else if ( key == "adminsTouchEntities" && isBool ) AdminsTouchEntities = b;
		else if ( key == "maxPropSize" && !float.IsNaN( f ) ) MaxPropSize = Math.Max( 0, (int)f );
		else if ( key == "spawnDelay" && !float.IsNaN( f ) ) SpawnDelay = Math.Clamp( f, 0, 60 );
		else if ( key == "cleanupDelay" && !float.IsNaN( f ) ) CleanupDelay = Math.Max( -1, (int)f );
		else return false;
		return true;
	}
}

/// <summary>Static state of the prop protection (reset with each play session).</summary>
public sealed class RPPPState
{
	public RPPPSettings Host;
	public string ClientJson;
	public RPPPSettings Client;
	/// <summary>Owner SteamId → SteamIds they trust (host).</summary>
	public readonly Dictionary<long, List<long>> Buddies = new Dictionary<long, List<long>>();
	/// <summary>SteamId of a player who left → when their props are removed.</summary>
	public readonly Dictionary<long, float> CleanupAt = new Dictionary<long, float>();
	public readonly Dictionary<long, string> LeftNames = new Dictionary<long, string>();
	public readonly Dictionary<long, float> LastSpawn = new Dictionary<long, float>();
	public readonly Dictionary<long, float> LastNotice = new Dictionary<long, float>();
	public bool ConVarSet;
	public bool LastActive;
	/// <summary>Settings changed from the menu: written to the file on the next tick (sliders send many changes).</summary>
	public bool Dirty;
}

/// <summary>Marks a prop (the root object) with the player who owns it, and refuses physgun grabs by others.
/// Added by the host to everything with physics: spawned props get their spawner, map props are "world" (0).</summary>
[Title( "DarkRP Prop Owner" ), Hide]
public sealed class RPPropGuard : Component, IPhysgunEvent
{
	public long OwnerSteamId;
	public string OwnerName = "";

	public void OnPhysgunGrab( IPhysgunEvent.GrabEvent e )
	{
		if ( e.Cancelled || !RPPropProtection.Active() || IsProxy ) return;
		var s = RPPropProtection.S();
		if ( !s.Physgun && Components.Get<RPEntity>() == null ) return;
		if ( !RPPropProtection.CanTouch( e.Grabber, GameObject, "physgun" ) )
		{
			e.Cancelled = true;
			RPPropProtection.Deny( e.Grabber, GameObject, "physgun" );
		}
	}
}

/// <summary>DarkRP prop protection (host decides; clients read the synced settings for the menu and the HUD).</summary>
public static class RPPropProtection
{
	static readonly RPPPState H = new RPPPState();
	public const string File = "propprotection.json";
	public const string BuddyFile = "pp_buddies.json";

	public static void Reset()
	{
		H.Host = null; H.ClientJson = null; H.Client = null;
		H.Buddies.Clear(); H.CleanupAt.Clear(); H.LeftNames.Clear(); H.LastSpawn.Clear(); H.LastNotice.Clear();
		H.ConVarSet = false; H.LastActive = false; H.Dirty = false;
	}

	static bool IsServer() => Networking.IsHost || !Networking.IsActive;

	/// <summary>The settings: the host's own, or what the host sent (clients).</summary>
	public static RPPPSettings S()
	{
		if ( IsServer() )
		{
			if ( H.Host == null ) Load();
			return H.Host;
		}
		var w = RPWorld.Current();
		var json = w != null ? w.PropProtectionJson : "";
		if ( H.Client == null || json != H.ClientJson )
		{
			H.ClientJson = json;
			H.Client = RPPPSettings.FromJson( json );
		}
		return H.Client;
	}

	/// <summary>True when prop protection is on (setting) and the module isn't disabled.</summary>
	public static bool Active() => !DarkRP.ModuleDisabled( "propprotection" ) && S().Enabled;

	// ---- files ----

	public static void Load()
	{
		var text = RPDataFiles.Read( File );
		H.Host = text != null ? RPPPSettings.FromJson( text ) : new RPPPSettings();
		Save();
		H.Buddies.Clear();
		var o = RPJson.Parse( RPDataFiles.Read( BuddyFile ) );
		var owners = RPJson.StrArray( o, "owners", new string[0] );
		foreach ( var owner in owners )
		{
			long id = ParseId( owner );
			if ( id == 0 ) continue;
			var list = new List<long>();
			foreach ( var b in RPJson.StrArray( o, owner, new string[0] ) ) { long bid = ParseId( b ); if ( bid != 0 ) list.Add( bid ); }
			H.Buddies[id] = list;
		}
	}

	public static void Save()
	{
		var json = H.Host.ToJson( true );
		if ( RPDataFiles.Read( File ) != json ) RPDataFiles.Write( File, json );
		Publish();
	}

	static void SaveBuddies()
	{
		var w = new RPJsonWriter();
		w.BeginObject();
		w.Str( "_readme", "Prop protection buddies: for each owner (SteamId), the players who may touch their props. Players change this in the spawn menu or with /buddy." );
		var owners = H.Buddies.Keys.Where( k => H.Buddies[k].Count > 0 ).ToList();
		w.StrArray( "owners", owners.Select( k => k.ToString() ).ToArray() );
		foreach ( var k in owners ) w.StrArray( k.ToString(), H.Buddies[k].Select( x => x.ToString() ).ToArray() );
		w.EndObject();
		RPDataFiles.Write( BuddyFile, w.ToString() );
		Publish();
	}

	/// <summary>Host: sends the settings and buddy lists to everyone.</summary>
	public static void Publish()
	{
		var w = RPWorld.Current();
		if ( w == null || w.IsProxy || H.Host == null ) return;
		var json = H.Host.ToJson( false );
		if ( w.PropProtectionJson != json ) w.PropProtectionJson = json;
		foreach ( var k in H.Buddies.Keys )
		{
			var v = string.Join( ",", H.Buddies[k] );
			var key = k.ToString();
			if ( !w.PPBuddies.ContainsKey( key ) || w.PPBuddies[key] != v ) w.PPBuddies[key] = v;
		}
	}

	// ---- who may touch what ----

	/// <summary>A SteamId from text (floats can't hold 17 digits).</summary>
	public static long ParseId( string text )
	{
		long id = 0;
		long.TryParse( (text ?? "").Trim(), out id );
		return id;
	}

	public static long SteamOf( Connection c )
	{
		if ( c == null ) return 0;
		long id = 0;
		long.TryParse( c.SteamId.ToString(), out id );
		return id;
	}

	/// <summary>The owner of an object's root: its guard, a DarkRP entity's owner, or Sandbox's Ownable. 0 = world.</summary>
	public static long OwnerOf( GameObject go )
	{
		if ( go == null || !go.IsValid() ) return 0;
		var root = go.Root;
		var g = root.Components.Get<RPPropGuard>();
		if ( g != null ) return g.OwnerSteamId;
		return OwnerFromComponents( root );
	}

	static long OwnerFromComponents( GameObject root )
	{
		var ent = root.Components.Get<RPEntity>();
		if ( ent != null ) return ParseId( ent.OwnerId );
		var own = root.Components.Get<Ownable>();
		if ( own != null && own.Owner != null ) return SteamOf( own.Owner );
		return 0;
	}

	public static bool IsBuddy( long owner, long other )
	{
		if ( IsServer() ) return H.Buddies.ContainsKey( owner ) && H.Buddies[owner].Contains( other );
		var w = RPWorld.Current();
		if ( w == null || !w.PPBuddies.ContainsKey( owner.ToString() ) ) return false;
		return ("," + w.PPBuddies[owner.ToString()] + ",").Contains( "," + other + "," );
	}

	/// <summary>May <paramref name="c"/> do <paramref name="what"/> ("physgun", "toolgun", "pickup", "damage") to the
	/// object? Owners, their buddies and admins may; map props only admins (when "worldProps" is on).</summary>
	public static bool CanTouch( Connection c, GameObject go, string what )
	{
		if ( c == null || go == null || !go.IsValid() || !Active() ) return true;
		var root = go.Root;
		if ( root.Components.Get<Player>() != null ) return true;
		var s = S();
		var admin = RP.IsAdmin( c );
		var ent = root.Components.Get<RPEntity>();
		if ( ent != null && what != "damage" )
		{
			// DarkRP entities (FPP's blocked entities): only what their type allows, unless a hook says otherwise.
			var typeAllows = RPPPSettings.HasPerm( s.EntityPerms( ent.TypeId ), what );
			var eh = DarkRP.RunHook( new RPHook { Name = "CanTouchEntity", Player = c, Entity = root, Text = what, Reason = ent.TypeId, Allowed = typeAllows } );
			if ( !eh.Allowed && !(admin && s.AdminsTouchEntities) ) return false;
		}
		if ( admin ) return true;
		var owner = OwnerOf( root );
		var me = SteamOf( c );
		if ( owner == 0 )
		{
			// Dropped money, shipments and food belong to nobody: anyone may do what their type allows.
			if ( ent != null ) return true;
			return !s.WorldProps;
		}
		if ( owner == me ) return true;
		var allowed = IsBuddy( owner, me );
		var hook = DarkRP.RunHook( new RPHook { Name = "CanTouchProp", Player = c, Text = what, Reason = owner.ToString(), Allowed = allowed } );
		return hook.Allowed;
	}

	/// <summary>Tells a player (at most every 2 seconds) why they can't touch something.</summary>
	public static void Deny( Connection c, GameObject go, string what )
	{
		if ( c == null || go == null ) return;
		var id = SteamOf( c );
		if ( H.LastNotice.ContainsKey( id ) && Time.Now - H.LastNotice[id] < 2 ) return;
		H.LastNotice[id] = Time.Now;
		var ent = go.Root.Components.Get<RPEntity>();
		if ( ent != null && !RPPPSettings.HasPerm( S().EntityPerms( ent.TypeId ), what ) )
		{
			var t = DarkRP.Registry().EntityType( ent.TypeId );
			var verb = what == "physgun" ? "use the physgun on" : what == "toolgun" ? "use the tool gun on" : "pick up";
			DarkRP.Notify( c, 1, 3, "You can't " + verb + " " + (t != null ? "a " + t.Name.ToLowerInvariant() : "that") + "." );
			return;
		}
		var owner = OwnerOf( go );
		DarkRP.Notify( c, 1, 3, owner == 0 ? "That belongs to the map." : "That belongs to " + OwnerName( owner ) + "." );
	}

	public static string OwnerName( long steamId )
	{
		foreach ( var c in Connection.All ) if ( SteamOf( c ) == steamId ) return RP.Name( c );
		if ( H.LeftNames.ContainsKey( steamId ) ) return H.LeftNames[steamId] + " (left)";
		return "someone who left";
	}

	// ---- models ----

	public static string NormalizeModel( string path )
	{
		var p = (path ?? "").Trim().Replace( '\\', '/' ).TrimStart( '/' ).ToLowerInvariant();
		if ( p.EndsWith( ".vmdl_c" ) ) p = p.Substring( 0, p.Length - 2 );
		return p;
	}

	/// <summary>Is the model (path or cloud ident) on the blocked list? Entries can use * as a wildcard.</summary>
	public static bool IsBlocked( string model )
	{
		var m = NormalizeModel( model );
		if ( m == "" ) return false;
		foreach ( var entry in S().BlockedModels )
		{
			var pattern = NormalizeModel( entry );
			if ( pattern == "" ) continue;
			if ( pattern.Contains( "*" ) ? Glob( pattern, 0, m, 0 ) : pattern == m ) return true;
		}
		return false;
	}

	static bool Glob( string p, int pi, string t, int ti )
	{
		while ( pi < p.Length )
		{
			if ( p[pi] == '*' )
			{
				for ( int k = ti; k <= t.Length; k++ ) if ( Glob( p, pi + 1, t, k ) ) return true;
				return false;
			}
			if ( ti >= t.Length || p[pi] != t[ti] ) return false;
			pi++; ti++;
		}
		return ti == t.Length;
	}

	static string ModelOf( Global.ISpawnEvents.SpawnData e )
	{
		var prop = e.Spawner as PropSpawner;
		if ( prop != null ) return prop.Model != null && !string.IsNullOrEmpty( prop.Model.ResourcePath ) && prop.FullIdent == null ? prop.Model.ResourcePath : prop.Path;
		var mount = e.Spawner as MountSpawner;
		if ( mount != null && mount.Model != null ) return mount.Model.ResourcePath;
		return e.Spawner != null ? e.Spawner.Data : "";
	}

	/// <summary>"cloud prop", "cloud entity", "mounted" or "" for content shipped with the game/addons.</summary>
	public static string ContentKind( ISpawner sp )
	{
		if ( sp == null ) return "";
		if ( sp is MountSpawner || sp is MountPrefabSpawner ) return "mounted";
		var dupe = sp as DuplicatorSpawner;
		if ( dupe != null ) return dupe.Dupe != null && dupe.Dupe.Packages != null && dupe.Dupe.Packages.Count > 0 ? "cloud prop" : "";
		if ( sp.FullIdent == null ) return "";
		return sp is EntitySpawner ? "cloud entity" : "cloud prop";
	}

	// ---- events (host) ----

	public static void OnSpawn( Global.ISpawnEvents.SpawnData e )
	{
		if ( e.Cancelled || e.Player == null || !Active() ) return;
		var c = e.Player;
		var s = S();
		var admin = RP.IsAdmin( c );
		var id = SteamOf( c );

		if ( !admin && s.SpawnDelay > 0 && H.LastSpawn.ContainsKey( id ) && Time.Now - H.LastSpawn[id] < s.SpawnDelay )
		{
			e.Cancelled = true;
			return;
		}

		var kind = ContentKind( e.Spawner );
		if ( !admin && kind == "cloud prop" && !s.CloudProps ) { Refuse( e, "Only admins can spawn props from sbox.game." ); return; }
		if ( !admin && kind == "cloud entity" && !s.CloudEntities ) { Refuse( e, "Only admins can spawn entities from sbox.game." ); return; }
		if ( !admin && kind == "mounted" && !s.MountedContent ) { Refuse( e, "Only admins can spawn content from mounted games." ); return; }

		var model = ModelOf( e );
		if ( (!admin || s.BlockedModelsForAdmins) && IsBlocked( model ) ) { Refuse( e, "That model is blocked on this server." ); return; }

		if ( !admin && s.MaxPropSize > 0 && e.Spawner != null )
		{
			var size = e.Spawner.Bounds.Size;
			if ( Math.Max( size.x, Math.Max( size.y, size.z ) ) > s.MaxPropSize ) { Refuse( e, "That is too big (the limit is " + s.MaxPropSize + " units)." ); return; }
		}

		var h = DarkRP.RunHook( new RPHook { Name = "PlayerSpawnObject", Player = c, Text = model, Reason = kind } );
		if ( !h.Allowed ) { e.Cancelled = true; if ( !string.IsNullOrEmpty( h.Reason ) ) DarkRP.Notify( c, 1, 4, h.Reason ); return; }
		H.LastSpawn[id] = Time.Now;
	}

	static void Refuse( Global.ISpawnEvents.SpawnData e, string why )
	{
		e.Cancelled = true;
		DarkRP.Notify( e.Player, 1, 4, why );
	}

	public static void OnPostSpawn( Global.ISpawnEvents.PostSpawnData e )
	{
		if ( e.Objects == null ) return;
		var c = e.Player;
		foreach ( var go in e.Objects ) Guard( go, c );
		if ( !Active() || c == null ) return;
		var s = S();
		var admin = RP.IsAdmin( c );
		// Dupes and spawners can bring in several models: check every renderer.
		if ( !admin || s.BlockedModelsForAdmins )
		{
			int removed = 0;
			foreach ( var go in e.Objects )
			{
				if ( !go.IsValid() ) continue;
				foreach ( var r in go.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
				{
					if ( r.Model != null && IsBlocked( r.Model.ResourcePath ) ) { go.Destroy(); removed++; break; }
				}
			}
			if ( removed > 0 ) DarkRP.Notify( c, 1, 4, removed + " object(s) used a blocked model and were removed." );
		}
		if ( s.LogSpawns ) RPLog.Write( RP.Name( c ) + " (" + SteamOf( c ) + ") spawned " + (e.Spawner != null ? (e.Spawner.Data ?? e.Spawner.DisplayName) : "?") );
	}

	public static void OnToolAction( IToolActionEvents.ActionData e )
	{
		if ( e.Cancelled || e.Player == null || !Active() ) return;
		var target = AimedObject( e.Player, 5000 );
		if ( target == null || target.Root.Components.Get<RPPropGuard>() == null ) return;
		if ( !S().Toolgun && target.Root.Components.Get<RPEntity>() == null ) return;
		if ( CanTouch( e.Player, target, "toolgun" ) ) return;
		e.Cancelled = true;
		Deny( e.Player, target, "toolgun" );
	}

	public static void OnPostToolAction( IToolActionEvents.PostActionData e )
	{
		if ( e.CreatedObjects == null ) return;
		foreach ( var go in e.CreatedObjects ) Guard( go, e.Player );
	}

	public static void OnCarry( PlayerCarryEvent e )
	{
		if ( e.Cancelled || e.Player == null || e.Target == null || !Active() ) return;
		if ( !S().Pickup && e.Target.Root.Components.Get<RPEntity>() == null ) return;
		var c = e.Player.Network.Owner;
		if ( CanTouch( c, e.Target, "pickup" ) ) return;
		e.Cancelled = true;
		Deny( c, e.Target, "pickup" );
	}

	public static void OnDamaging( Component.IDamageable.DamageEvent e )
	{
		if ( e.Cancelled || !Active() || e.Victim == null ) return;
		var s = S();
		var victim = e.Victim.Root;
		if ( victim.Components.Get<Player>() != null )
		{
			// Anti prop-kill: props (thrown, pushed, dropped) don't hurt players.
			if ( !s.AntiPropKill || !e.DamageInfo.Tags.Contains( "impact" ) ) return;
			var attacker = e.DamageInfo.Attacker;
			if ( attacker == null || !attacker.IsValid() || attacker.Root.Components.Get<Player>() != null ) return;
			if ( attacker.Root.Components.Get<RPPropGuard>() == null && attacker.Root.Components.Get<Rigidbody>( true ) == null ) return;
			e.Cancelled = true;
			return;
		}
		if ( !s.PropDamage ) return;
		if ( victim.Components.Get<RPEntity>() != null ) return; // printers, labs... can be destroyed (DarkRP)
		if ( victim.Components.Get<RPPropGuard>() == null ) return;
		var inst = e.Instigator;
		var ply = inst != null && inst.IsValid() ? inst.Components.Get<Player>( FindMode.EverythingInSelfAndAncestors ) : null;
		if ( ply == null ) return;
		var c = ply.Network.Owner;
		if ( !CanTouch( c, victim, "damage" ) ) e.Cancelled = true;
	}

	/// <summary>A player's own cleanup only removes their own things (not map props or props of players who left,
	/// which the prop protection gives to the host to keep them protected).</summary>
	public static void OnCleaningUp( CleanupEvent e )
	{
		if ( e.Client == null || e.Objects == null ) return;
		var id = SteamOf( e.Client );
		e.Objects.RemoveAll( go => go.IsValid() && go.Root.Components.Get<RPPropGuard>() != null && go.Root.Components.Get<RPPropGuard>().OwnerSteamId != id );
	}

	// ---- guards ----

	public static GameObject AimedObject( Connection c, float range )
	{
		var ply = RP.Pawn( c );
		if ( ply == null ) return null;
		var eye = ply.EyeTransform;
		var tr = ply.Scene.Trace.Ray( eye.Position, eye.Position + eye.Forward * range ).IgnoreGameObjectHierarchy( ply.GameObject ).Run();
		return tr.Hit && tr.GameObject != null && tr.GameObject.IsValid() ? tr.GameObject : null;
	}

	/// <summary>Host: marks the root of <paramref name="go"/> as owned by <paramref name="owner"/> (null = map).</summary>
	public static RPPropGuard Guard( GameObject go, Connection owner )
	{
		if ( go == null || !go.IsValid() ) return null;
		var root = go.Root;
		if ( root.Components.Get<Player>() != null ) return null;
		var g = root.Components.Get<RPPropGuard>();
		if ( g == null )
		{
			g = root.Components.Create<RPPropGuard>();
			if ( g == null ) return null;
			g.OwnerSteamId = owner != null ? SteamOf( owner ) : OwnerFromComponents( root );
		}
		else if ( owner != null ) g.OwnerSteamId = SteamOf( owner );
		g.OwnerName = g.OwnerSteamId != 0 ? OwnerName( g.OwnerSteamId ) : "";
		if ( g.OwnerSteamId == 0 ) ProtectWorld( root );
		return g;
	}

	/// <summary>Sandbox's own checks (context menu, tool gun) let anyone change objects without an owner: map props are
	/// given to the host while "worldProps" is on, so only admins can change them there too.</summary>
	static void ProtectWorld( GameObject root )
	{
		if ( !Active() || !S().WorldProps || root.Components.Get<RPEntity>() != null ) return;
		var own = root.Components.Get<Ownable>();
		if ( own != null && own.Owner != null ) return;
		if ( Connection.Host != null ) Ownable.Set( root, Connection.Host );
	}

	/// <summary>Host, every second: guards new physics objects, removes props of players who left, keeps Sandbox's
	/// own ownership check in step.</summary>
	public static void Tick( RPWorld w )
	{
		if ( H.Host == null ) Load();
		if ( H.Dirty ) { H.Dirty = false; Save(); }
		var active = Active();
		// Sandbox's own check (tool gun, context menu, wiring): owner only, admins exempt. On while this is on.
		if ( (active && !Ownable.OwnershipChecks) || (!active && H.LastActive && Ownable.OwnershipChecks) )
		{
			try { ServerSettings.Set( "sandbox.ownership.checks", active ); }
			catch ( Exception ex ) { Log.Warning( "DarkRP: can't set sandbox.ownership.checks: " + ex.Message ); }
		}
		if ( !H.ConVarSet || active != H.LastActive )
		{
			H.ConVarSet = true;
			H.LastActive = active;
			if ( active ) foreach ( var g in w.Scene.GetAllComponents<RPPropGuard>().ToList() ) if ( g.OwnerSteamId == 0 ) ProtectWorld( g.GameObject );
		}

		foreach ( var rb in w.Scene.GetAllComponents<Rigidbody>().ToList() )
		{
			var root = rb.GameObject.Root;
			if ( root == null || root.Components.Get<RPPropGuard>() != null ) continue;
			Guard( root, null );
		}

		if ( H.CleanupAt.Count == 0 ) return;
		foreach ( var id in H.CleanupAt.Keys.ToList() )
		{
			if ( Time.Now < H.CleanupAt[id] ) continue;
			H.CleanupAt.Remove( id );
			if ( Connection.All.Any( c => SteamOf( c ) == id ) ) continue;
			var n = RemoveProps( w.Scene, id );
			if ( n > 0 ) RPLog.Write( "Removed " + n + " props of " + OwnerName( id ) + " (left " + S().CleanupDelay + "s ago)" );
		}
	}

	public static int RemoveProps( Scene scene, long owner )
	{
		int n = 0;
		foreach ( var g in scene.GetAllComponents<RPPropGuard>().ToList() )
		{
			if ( g.OwnerSteamId != owner || !g.GameObject.IsValid() ) continue;
			if ( g.GameObject.Components.Get<RPEntity>() != null && !DarkRP.Config().Bool( "removeondisconnect" ) && Connection.All.All( c => SteamOf( c ) != owner ) ) continue;
			g.GameObject.Destroy();
			n++;
		}
		return n;
	}

	public static void OnLeave( RPPlayer rp, Connection c )
	{
		if ( rp == null || rp.SteamId == 0 ) return;
		var id = rp.SteamId;
		H.LeftNames[id] = c != null ? RP.Name( c ) : rp.SteamIdText;
		if ( !Active() ) return;
		var w = RPWorld.Current();
		if ( w == null ) return;
		foreach ( var g in w.Scene.GetAllComponents<RPPropGuard>().ToList() )
		{
			if ( g.OwnerSteamId != id ) continue;
			// Sandbox would treat the props as ownerless (anyone could change them): the host keeps them for now.
			if ( Connection.Host != null && Connection.Host != c ) Ownable.Set( g.GameObject, Connection.Host );
		}
		var delay = S().CleanupDelay;
		if ( delay >= 0 ) H.CleanupAt[id] = Time.Now + delay;
	}

	public static void OnJoin( Connection c )
	{
		if ( c == null ) return;
		var id = SteamOf( c );
		H.CleanupAt.Remove( id );
		var w = RPWorld.Current();
		if ( w == null ) return;
		int n = 0;
		foreach ( var g in w.Scene.GetAllComponents<RPPropGuard>().ToList() )
		{
			if ( g.OwnerSteamId != id ) continue;
			Ownable.Set( g.GameObject, c );
			g.OwnerName = RP.Name( c );
			n++;
		}
		if ( n > 0 ) DarkRP.Notify( c, 0, 5, "Your " + n + " props are yours again." );
	}

	// ---- buddies and admin actions (from the menu page and chat commands) ----

	public static List<long> BuddiesOf( long owner )
	{
		if ( !H.Buddies.ContainsKey( owner ) ) H.Buddies[owner] = new List<long>();
		return H.Buddies[owner];
	}

	public static void ToggleBuddy( Connection c, Connection other )
	{
		if ( c == null ) return;
		if ( other == null ) { DarkRP.Notify( c, 1, 4, "Player not found." ); return; }
		if ( other == c ) { DarkRP.Notify( c, 1, 4, "You can always touch your own props." ); return; }
		var list = BuddiesOf( SteamOf( c ) );
		var oid = SteamOf( other );
		if ( list.Contains( oid ) )
		{
			list.Remove( oid );
			DarkRP.Notify( c, 0, 4, RP.Name( other ) + " can no longer touch your props." );
			DarkRP.Notify( other, 0, 4, RP.Name( c ) + " removed you as a prop buddy." );
		}
		else
		{
			list.Add( oid );
			DarkRP.Notify( c, 0, 4, RP.Name( other ) + " can now touch your props." );
			DarkRP.Notify( other, 0, 4, RP.Name( c ) + " added you as a prop buddy: you can touch their props." );
		}
		SaveBuddies();
	}

	public static void Action( Connection c, string action, string a, string b )
	{
		if ( c == null ) return;
		if ( action == "buddy" ) { ToggleBuddy( c, RP.Find( a ) ); return; }
		if ( !RP.IsAdmin( c ) ) { DarkRP.Notify( c, 1, 4, "Only admins can change the prop protection." ); return; }
		var s = S();
		var w = RPWorld.Current();
		if ( action == "set" )
		{
			if ( !s.Set( a, b ) ) { DarkRP.Notify( c, 1, 4, "Bad prop protection setting: " + a + " = " + b ); return; }
			Publish();
			H.Dirty = true;
			RPLog.Write( RP.Name( c ) + " set prop protection " + a + " to " + b );
		}
		else if ( action == "entity" )
		{
			var id = (a ?? "").ToLowerInvariant();
			if ( DarkRP.Registry().EntityType( id ) == null ) { DarkRP.Notify( c, 1, 4, "Unknown entity type: " + a ); return; }
			s.EntityTouch[id] = RPPPSettings.CleanPerms( b );
			Publish();
			H.Dirty = true;
			RPLog.Write( RP.Name( c ) + " set prop protection for " + id + " to \"" + s.EntityTouch[id] + "\"" );
		}
		else if ( action == "block" )
		{
			var model = NormalizeModel( a );
			if ( model == "" )
			{
				var target = AimedObject( c, 5000 );
				var r = target != null ? target.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndAncestors ) : null;
				if ( r == null ) r = target != null ? target.Root.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) : null;
				model = r != null && r.Model != null ? NormalizeModel( r.Model.ResourcePath ) : "";
			}
			if ( model == "" ) { DarkRP.Notify( c, 1, 4, "Look at a prop, or give a model path." ); return; }
			if ( s.BlockedModels.Any( x => NormalizeModel( x ) == model ) ) { DarkRP.Notify( c, 1, 4, model + " is already blocked." ); return; }
			s.BlockedModels.Add( model );
			Save();
			DarkRP.Notify( c, 0, 4, "Blocked " + model + "." );
			RPLog.Write( RP.Name( c ) + " blocked the model " + model );
		}
		else if ( action == "unblock" )
		{
			var model = NormalizeModel( a );
			var n = s.BlockedModels.RemoveAll( x => NormalizeModel( x ) == model );
			if ( n == 0 ) { DarkRP.Notify( c, 1, 4, model + " isn't blocked." ); return; }
			Save();
			DarkRP.Notify( c, 0, 4, "Unblocked " + model + "." );
			RPLog.Write( RP.Name( c ) + " unblocked the model " + model );
		}
		else if ( action == "cleanup" && w != null )
		{
			int n = 0;
			string who;
			if ( a == "disconnected" )
			{
				var connected = Connection.All.Select( x => SteamOf( x ) ).ToList();
				var owners = w.Scene.GetAllComponents<RPPropGuard>().Select( g => g.OwnerSteamId ).Where( id => id != 0 && !connected.Contains( id ) ).Distinct().ToList();
				foreach ( var id in owners ) { n += RemoveProps( w.Scene, id ); H.CleanupAt.Remove( id ); }
				who = "players who left";
			}
			else
			{
				var target = RP.Find( a );
				if ( target == null ) { DarkRP.Notify( c, 1, 4, "Player not found." ); return; }
				foreach ( var g in w.Scene.GetAllComponents<RPPropGuard>().ToList() )
					if ( g.OwnerSteamId == SteamOf( target ) && g.GameObject.Components.Get<RPEntity>() == null ) { g.GameObject.Destroy(); n++; }
				who = RP.Name( target );
			}
			DarkRP.Notify( c, 0, 4, "Removed " + n + " props of " + who + "." );
			RPLog.Write( RP.Name( c ) + " removed " + n + " props of " + who );
		}
	}

	public static void Register( RPRegistry r )
	{
		r.AddCommand( new RPCommandDef { Name = "buddy", Description = "Prop protection: let a player touch your props, or stop them: /buddy <player>.", Handler = cmd => ToggleBuddy( cmd.Caller, RP.Find( cmd.ArgString ) ) } );
		r.AddCommand( new RPCommandDef { Name = "buddies", Description = "Prop protection: who may touch your props.", Handler = cmd => ListBuddies( cmd.Caller ) } );
		r.AddCommand( new RPCommandDef { Name = "ppcleanup", Description = "Admin: remove a player's props: /ppcleanup <player|disconnected>.", Permission = "admin", Handler = cmd => Action( cmd.Caller, "cleanup", cmd.ArgString, "" ) } );
		r.AddCommand( new RPCommandDef { Name = "ppblock", Description = "Admin: block a model (the prop you look at, or /ppblock <model path>).", Permission = "admin", Handler = cmd => Action( cmd.Caller, "block", cmd.ArgString, "" ) } );
		r.AddCommand( new RPCommandDef { Name = "ppunblock", Description = "Admin: unblock a model: /ppunblock <model path>.", Permission = "admin", Handler = cmd => Action( cmd.Caller, "unblock", cmd.ArgString, "" ) } );
		r.AddCommand( new RPCommandDef { Name = "ppset", Description = "Admin: change a prop protection setting: /ppset <setting> <value> (see darkrp/README.txt).", Permission = "admin", Handler = cmd => Action( cmd.Caller, "set", cmd.Arg( 0 ), cmd.Arg( 1 ) ) } );
	}

	/// <summary>Every guarded object with its owner (for tests and the console).</summary>
	public static string Report( Scene scene )
	{
		var sb = new System.Text.StringBuilder();
		sb.Append( "active=" + Active() + " sandboxChecks=" + Ownable.OwnershipChecks + (char)10 );
		foreach ( var g in scene.GetAllComponents<RPPropGuard>() )
		{
			var own = g.GameObject.Components.Get<Ownable>();
			sb.Append( g.GameObject.Name + " owner=" + g.OwnerSteamId + " (" + g.OwnerName + ") ownable=" + (own != null && own.Owner != null ? own.Owner.DisplayName : "none") + (char)10 );
		}
		return sb.ToString();
	}

	static void ListBuddies( Connection c )
	{
		var list = BuddiesOf( SteamOf( c ) );
		DarkRP.Notify( c, 0, 6, list.Count == 0 ? "Nobody else can touch your props. Add someone with /buddy <player>." : "Your prop buddies: " + string.Join( ", ", list.Select( id => OwnerName( id ) ) ) );
	}
}
