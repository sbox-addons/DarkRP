// DarkRP for s&box - door ownership: buying, selling, co-owners, titles, locking, door groups and job doors.
// Works on every Sandbox.Mapping.Door (map doors and DarkRP's spawnable door). Ported from DarkRP's modules/doorsystem
// (MIT, Falco Peijnenburg).

/// <summary>A door's DarkRP state (packed into RPWorld.Doors).</summary>
public sealed class RPDoorData
{
	public string Owner = "";
	public List<string> CoOwners = new List<string>();
	public string Title = "";
	public bool Locked;
	public string Group = "";
	public string[] Teams = new string[0];
	public bool NonOwnable;

	public bool IsOwned => !string.IsNullOrEmpty( Owner );
	public bool IsJobDoor => !string.IsNullOrEmpty( Group ) || (Teams != null && Teams.Length > 0);

	public string Pack() => Owner + "\u0001" + string.Join( ",", CoOwners ) + "\u0001" + (Title ?? "").Replace( "\u0001", "" ) + "\u0001" + (Locked ? "1" : "0") + "\u0001" + Group + "\u0001" + string.Join( ",", Teams ?? new string[0] ) + "\u0001" + (NonOwnable ? "1" : "0");

	public static RPDoorData Unpack( string s )
	{
		var d = new RPDoorData();
		if ( string.IsNullOrEmpty( s ) ) return d;
		var p = s.Split( '\u0001' );
		if ( p.Length > 0 ) d.Owner = p[0];
		if ( p.Length > 1 ) d.CoOwners = p[1].Split( ',', StringSplitOptions.RemoveEmptyEntries ).ToList();
		if ( p.Length > 2 ) d.Title = p[2];
		if ( p.Length > 3 ) d.Locked = p[3] == "1";
		if ( p.Length > 4 ) d.Group = p[4];
		if ( p.Length > 5 ) d.Teams = p[5].Split( ',', StringSplitOptions.RemoveEmptyEntries );
		if ( p.Length > 6 ) d.NonOwnable = p[6] == "1";
		return d;
	}
}

public static class RPDoors
{
	/// <summary>The door component of an object (or one of its parents).</summary>
	public static Sandbox.Mapping.Door DoorOf( GameObject go )
	{
		if ( !go.IsValid() ) return null;
		return go.Components.Get<Sandbox.Mapping.Door>( FindMode.EverythingInSelfAndAncestors );
	}

	public static string IdOf( Sandbox.Mapping.Door door ) => door != null ? door.GameObject.Id.ToString() : "";

	public static Sandbox.Mapping.Door Find( Scene scene, string id )
	{
		if ( scene == null || string.IsNullOrEmpty( id ) ) return null;
		foreach ( var d in scene.GetAllComponents<Sandbox.Mapping.Door>() ) if ( d.GameObject.Id.ToString() == id ) return d;
		return null;
	}

	public static RPDoorData Get( RPWorld w, string id )
	{
		if ( w == null || string.IsNullOrEmpty( id ) ) return new RPDoorData();
		return w.Doors.ContainsKey( id ) ? RPDoorData.Unpack( w.Doors[id] ) : new RPDoorData();
	}

	public static void Set( RPWorld w, string id, RPDoorData d )
	{
		w.Doors[id] = d.Pack();
		var door = Find( w.Scene, id );
		if ( door != null ) door.IsLocked = d.Locked;
	}

	public static bool IsOwner( RPDoorData d, RPPlayer rp ) => rp != null && d.Owner == rp.SteamIdText;
	public static bool IsCoOwner( RPDoorData d, RPPlayer rp ) => rp != null && d.CoOwners.Contains( rp.SteamIdText );

	/// <summary>Can this player use keys on the door (owner, co-owner, the door's group or jobs)?</summary>
	public static bool HasAccess( RPDoorData d, RPPlayer rp )
	{
		if ( rp == null ) return false;
		if ( IsOwner( d, rp ) || IsCoOwner( d, rp ) ) return true;
		if ( d.Teams != null && RPUtil.ContainsIgnoreCase( d.Teams, rp.JobCommand ) ) return true;
		if ( !string.IsNullOrEmpty( d.Group ) )
		{
			foreach ( var g in DarkRP.Registry().DoorGroups )
				if ( g.Name == d.Group && RPUtil.ContainsIgnoreCase( g.Jobs, rp.JobCommand ) ) return true;
		}
		return false;
	}

	public static int CountOwned( RPWorld w, RPPlayer rp )
	{
		int n = 0;
		foreach ( var key in w.Doors.Keys ) if ( RPDoorData.Unpack( w.Doors[key] ).Owner == rp.SteamIdText ) n++;
		return n;
	}

	static bool Near( Connection c, Sandbox.Mapping.Door door, float range )
	{
		var ply = RP.Pawn( c );
		return ply != null && door != null && ply.WorldPosition.Distance( door.WorldPosition ) < range;
	}

	public static void Action( Connection c, string doorId, string action, string arg )
	{
		var w = RPWorld.Current();
		var door = w != null ? Find( w.Scene, doorId ) : null;
		var rp = RP.Of( c );
		if ( door == null || rp == null ) return;
		bool admin = RP.IsAdmin( c );
		if ( !Near( c, door, 250 ) && !admin ) { DarkRP.Notify( c, 1, 3, "You're too far away from the door." ); return; }
		var d = Get( w, doorId );
		var cfg = DarkRP.Config();

		switch ( action )
		{
			case "buy":
				{
					if ( d.NonOwnable || d.IsJobDoor ) { DarkRP.Notify( c, 1, 4, "This door can't be owned." ); return; }
					if ( d.IsOwned ) { DarkRP.Notify( c, 1, 4, "This door is already owned." ); return; }
					if ( CountOwned( w, rp ) >= cfg.Int( "maxdoors" ) ) { DarkRP.Notify( c, 1, 4, "You own too many doors." ); return; }
					var cost = cfg.Int( "doorcost" );
					var h = DarkRP.RunHook( new RPHook { Name = "playerBuyDoor", Player = c, Entity = door.GameObject, Amount = cost } );
					if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't buy this door." : h.Reason ); return; }
					cost = (int)h.Amount;
					if ( !RP.CanAfford( c, cost ) ) { DarkRP.Notify( c, 1, 4, "You can't afford this door! (" + RPUtil.Money( cost ) + ")" ); return; }
					RP.AddMoney( c, -cost );
					d.Owner = rp.SteamIdText;
					d.CoOwners.Clear();
					d.Title = "";
					Set( w, doorId, d );
					LinkedToo( w, door, d );
					DarkRP.Notify( c, 0, 4, "You've bought this door for " + RPUtil.Money( cost ) + "." );
					DarkRP.RunHook( new RPHook { Name = "playerBoughtDoor", Player = c, Entity = door.GameObject, Amount = cost } );
					return;
				}
			case "sell":
				{
					if ( !IsOwner( d, rp ) && !admin ) { DarkRP.Notify( c, 1, 4, "You don't own this door." ); return; }
					var refund = (int)Math.Floor( cfg.Int( "doorcost" ) * 2f / 3f + 0.5f );
					var wasOwner = IsOwner( d, rp );
					if ( wasOwner ) RP.AddMoney( c, refund );
					Unown( w, doorId, d );
					LinkedToo( w, door, d );
					DarkRP.Notify( c, 0, 4, "You've sold this door" + (wasOwner ? " for " + RPUtil.Money( refund ) : "") + "." );
					DarkRP.RunHook( new RPHook { Name = "playerSoldDoor", Player = c, Entity = door.GameObject, Amount = refund } );
					return;
				}
			case "addowner":
				{
					if ( !IsOwner( d, rp ) ) { DarkRP.Notify( c, 1, 4, "You don't own this door." ); return; }
					var target = RP.Find( arg );
					var trp = RP.Of( target );
					if ( trp == null || trp == rp ) { DarkRP.Notify( c, 1, 4, "Player not found." ); return; }
					if ( !d.CoOwners.Contains( trp.SteamIdText ) ) d.CoOwners.Add( trp.SteamIdText );
					Set( w, doorId, d );
					LinkedToo( w, door, d );
					DarkRP.Notify( c, 0, 4, RP.Name( target ) + " is now a co-owner of this door." );
					DarkRP.Notify( target, 0, 4, RP.Name( c ) + " made you a co-owner of a door." );
					return;
				}
			case "removeowner":
				{
					if ( !IsOwner( d, rp ) ) { DarkRP.Notify( c, 1, 4, "You don't own this door." ); return; }
					var target = RP.Find( arg );
					var trp = RP.Of( target );
					var sid = trp != null ? trp.SteamIdText : arg;
					d.CoOwners.Remove( sid );
					Set( w, doorId, d );
					LinkedToo( w, door, d );
					DarkRP.Notify( c, 0, 4, "Co-owner removed." );
					return;
				}
			case "title":
				{
					bool canTitle = IsOwner( d, rp ) || ((!d.IsOwned) && admin);
					if ( !canTitle ) { DarkRP.Notify( c, 1, 4, "You can't change this door's title." ); return; }
					d.Title = (arg ?? "").Trim();
					if ( d.Title.Length > 60 ) d.Title = d.Title.Substring( 0, 60 );
					Set( w, doorId, d );
					if ( !d.IsOwned ) SaveSettings( w, doorId, d );
					return;
				}
			case "lock":
			case "unlock":
				{
					if ( !HasAccess( d, rp ) ) { DarkRP.Notify( c, 1, 3, "You don't have the keys to this door." ); return; }
					d.Locked = action == "lock";
					Set( w, doorId, d );
					LinkedToo( w, door, d );
					RPNet.Sound( d.Locked ? "sounds/ui/ui.lock.sound" : "sounds/ui/ui.unlock.sound", door.WorldPosition );
					return;
				}
			case "knock":
				RPNet.Sound( "sounds/impacts/melee/impact-melee-wood.sound", door.WorldPosition );
				return;
			case "ownable":
				{
					if ( !admin ) return;
					d.NonOwnable = !d.NonOwnable;
					if ( d.NonOwnable && d.IsOwned ) { d.Owner = ""; d.CoOwners.Clear(); }
					Set( w, doorId, d );
					SaveSettings( w, doorId, d );
					DarkRP.Notify( c, 0, 4, d.NonOwnable ? "This door can no longer be owned." : "This door can be owned again." );
					return;
				}
			case "group":
				{
					if ( !admin ) return;
					d.Group = (arg ?? "").Trim();
					if ( d.IsJobDoor ) { d.Owner = ""; d.CoOwners.Clear(); }
					Set( w, doorId, d );
					SaveSettings( w, doorId, d );
					DarkRP.Notify( c, 0, 4, string.IsNullOrEmpty( d.Group ) ? "Door group removed." : "Door group set to " + d.Group + "." );
					return;
				}
			case "teams":
				{
					if ( !admin ) return;
					d.Teams = (arg ?? "").Split( ',', StringSplitOptions.RemoveEmptyEntries ).Select( x => x.Trim().ToLowerInvariant() ).ToArray();
					if ( d.IsJobDoor ) { d.Owner = ""; d.CoOwners.Clear(); }
					Set( w, doorId, d );
					SaveSettings( w, doorId, d );
					DarkRP.Notify( c, 0, 4, d.Teams.Length == 0 ? "Door jobs removed." : "Only " + string.Join( ", ", d.Teams ) + " can use this door now." );
					return;
				}
		}
	}

	/// <summary>Double doors share their state.</summary>
	static void LinkedToo( RPWorld w, Sandbox.Mapping.Door door, RPDoorData d )
	{
		var linked = door.LinkedDoor;
		if ( linked == null || linked == door ) return;
		Set( w, IdOf( linked ), d );
	}

	public static void Unown( RPWorld w, string id, RPDoorData d )
	{
		d.Owner = "";
		d.CoOwners.Clear();
		d.Title = "";
		d.Locked = false;
		Set( w, id, d );
	}

	public static void UnownAll( RPWorld w, RPPlayer rp )
	{
		if ( w == null || rp == null ) return;
		foreach ( var key in w.Doors.Keys.ToList() )
		{
			var d = RPDoorData.Unpack( w.Doors[key] );
			if ( d.Owner == rp.SteamIdText ) Unown( w, key, d );
			else if ( d.CoOwners.Remove( rp.SteamIdText ) ) Set( w, key, d );
		}
	}

	/// <summary>/unownalldoors</summary>
	public static void SellAll( Connection c )
	{
		var w = RPWorld.Current();
		var rp = RP.Of( c );
		if ( w == null || rp == null ) return;
		int n = 0;
		foreach ( var key in w.Doors.Keys.ToList() )
		{
			var d = RPDoorData.Unpack( w.Doors[key] );
			if ( d.Owner != rp.SteamIdText ) continue;
			Unown( w, key, d );
			n++;
		}
		var refund = (long)Math.Floor( DarkRP.Config().Int( "doorcost" ) * 2f / 3f + 0.5f ) * n;
		RP.AddMoney( c, refund );
		DarkRP.Notify( c, 0, 4, "You sold " + n + " door(s) for " + RPUtil.Money( refund ) + "." );
	}

	/// <summary>Battering ram / lockpick / admin: unlock and open a door.</summary>
	public static void ForceOpen( RPWorld w, Sandbox.Mapping.Door door, GameObject by )
	{
		var id = IdOf( door );
		var d = Get( w, id );
		d.Locked = false;
		Set( w, id, d );
		try { door.Open( by ); } catch { }
	}

	/// <summary>Can the police ram this door? (warrant against the owner, or an unowned door).</summary>
	public static string RamBlocker( RPWorld w, Connection cop, Sandbox.Mapping.Door door )
	{
		var d = Get( w, IdOf( door ) );
		var cfg = DarkRP.Config();
		var h = DarkRP.RunHook( new RPHook { Name = "canDoorRam", Player = cop, Entity = door.GameObject } );
		if ( !h.Allowed ) return string.IsNullOrEmpty( h.Reason ) ? "You can't ram this door." : h.Reason;
		if ( h.Handled ) return null;
		if ( !d.IsOwned )
		{
			if ( d.IsJobDoor || cfg.Bool( "canforcedooropen" ) ) return null;
			return "You can't ram unowned doors.";
		}
		if ( !cfg.Bool( "doorwarrants" ) ) return null;
		foreach ( var rp in RP.All() )
		{
			if ( (rp.SteamIdText == d.Owner || d.CoOwners.Contains( rp.SteamIdText )) && (rp.Warranted || rp.Wanted) ) return null;
		}
		return "You need a warrant against the owner to ram this door.";
	}

	static void SaveSettings( RPWorld w, string id, RPDoorData d )
	{
		var map = RPMap.Data();
		if ( !d.NonOwnable && string.IsNullOrEmpty( d.Group ) && (d.Teams == null || d.Teams.Length == 0) && (d.IsOwned || string.IsNullOrEmpty( d.Title )) )
		{
			map.DoorSettings.Remove( id );
		}
		else
		{
			map.DoorSettings[id] = (d.NonOwnable ? "1" : "0") + "\u0001" + d.Group + "\u0001" + string.Join( ",", d.Teams ?? new string[0] ) + "\u0001" + (d.IsOwned ? "" : d.Title);
		}
		RPMap.Save( w );
	}

	public static void ApplyMapSettings( RPWorld w )
	{
		var map = RPMap.Data();
		var unlock = DarkRP.Config().Bool( "unlockdoorsonstart" );
		foreach ( var door in w.Scene.GetAllComponents<Sandbox.Mapping.Door>() )
		{
			var id = IdOf( door );
			var d = Get( w, id );
			if ( map.DoorSettings.ContainsKey( id ) )
			{
				var p = map.DoorSettings[id].Split( '\u0001' );
				d.NonOwnable = p.Length > 0 && p[0] == "1";
				d.Group = p.Length > 1 ? p[1] : "";
				d.Teams = p.Length > 2 ? p[2].Split( ',', StringSplitOptions.RemoveEmptyEntries ) : new string[0];
				if ( p.Length > 3 && !d.IsOwned ) d.Title = p[3];
			}
			if ( unlock ) { d.Locked = false; door.IsLocked = false; }
			else d.Locked = door.IsLocked;
			w.Doors[id] = d.Pack();
		}
	}

	/// <summary>Host: new doors (spawned or streamed in) get an entry; lockdown doesn't change locks.</summary>
	public static void TickLocks( RPWorld w ) { }
}
