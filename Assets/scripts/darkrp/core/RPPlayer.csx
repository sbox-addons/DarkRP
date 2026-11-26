// DarkRP for s&box - per-player state (DarkRP's DarkRPVars: money, job, rpname, wanted, arrested, energy...).
// Lives on the player's PlayerData object (it survives deaths); the host writes, everyone reads.

[Title( "DarkRP Player" ), Hide]
public sealed class RPPlayer : Component
{
	[Sync( SyncFlags.FromHost )] public long Money { get; set; }
	/// <summary>Job command ("citizen", "cp"...).</summary>
	[Sync( SyncFlags.FromHost )] public string JobCommand { get; set; } = "";
	/// <summary>Custom job title set with /job (empty: the job's name).</summary>
	[Sync( SyncFlags.FromHost )] public string JobTitle { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string RPName { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public bool Wanted { get; set; }
	[Sync( SyncFlags.FromHost )] public string WantedReason { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public bool Warranted { get; set; }
	[Sync( SyncFlags.FromHost )] public string WarrantReason { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public bool Arrested { get; set; }
	/// <summary>Seconds of jail left (updated every second).</summary>
	[Sync( SyncFlags.FromHost )] public int ArrestLeft { get; set; }
	[Sync( SyncFlags.FromHost )] public bool License { get; set; }
	/// <summary>Hunger energy 0-100 (hunger module).</summary>
	[Sync( SyncFlags.FromHost )] public float Energy { get; set; } = 100;
	[Sync( SyncFlags.FromHost )] public int Salary { get; set; }
	/// <summary>Items in the pocket, for the pocket menu ("name|kind;name|kind").</summary>
	[Sync( SyncFlags.FromHost )] public string PocketList { get; set; } = "";
	/// <summary>The steam id this state belongs to (string, for clients).</summary>
	[Sync( SyncFlags.FromHost )] public string SteamIdText { get; set; } = "";

	// ---- host only ----
	public Connection Conn;
	public long SteamId;
	public bool Loaded;
	public float LastJobChange = -9999;
	public float WantedUntil;
	public float WarrantUntil;
	public float ArrestUntil;
	public string WantedBy = "";
	public float LastSalary;
	public float SpawnedAt;
	public bool PendingRespawnJob;
	public readonly Dictionary<string, float> DemotedUntil = new Dictionary<string, float>();
	public readonly Dictionary<string, float> Cooldowns = new Dictionary<string, float>();
	public readonly List<GameObject> OwnedEntities = RPPlayerLists.Objects();
	public readonly List<RPPocketItem> Pocket = new List<RPPocketItem>();
	/// <summary>Weapons bought or given by DarkRP (those can be dropped when restrictdrop is on).</summary>
	public readonly List<string> GivenWeapons = new List<string>();
	public float NextSave;
	public bool Dirty;

	protected override void OnDestroy()
	{
		// Save here: when play stops or the player leaves, this goes before anything else can.
		if ( !IsProxy && Loaded ) RPPlayers.Save( this );
	}

	/// <summary>True on the machine that controls this player.</summary>
	public bool IsLocal => Network.Owner == Connection.Local || (!Networking.IsActive);
}

/// <summary>Something carried in the pocket (the entity is stored as its type + json state).</summary>
public sealed class RPPocketItem
{
	public string Name = "";
	public string TypeId = "";
	public string Model = "";
	public string State = "";
	public long Owner;
}

public static class RPPlayerLists
{
	public static List<GameObject> Objects() => new List<GameObject>();
}

/// <summary>Player helpers (host unless noted). DarkRP's player meta functions.</summary>
public static class RP
{
	/// <summary>The DarkRP state of a connection (host and clients).</summary>
	public static RPPlayer Of( Connection c )
	{
		if ( c == null ) return null;
		var scene = Game.ActiveScene;
		if ( scene == null ) return null;
		foreach ( var p in scene.GetAllComponents<RPPlayer>() )
		{
			if ( p.Conn == c ) return p;
			if ( p.Network.Owner == c ) return p;
		}
		return null;
	}

	/// <summary>The DarkRP state of the local player (clients).</summary>
	public static RPPlayer Local()
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return null;
		foreach ( var p in scene.GetAllComponents<RPPlayer>() )
			if ( p.Network.Owner == Connection.Local ) return p;
		if ( !Networking.IsActive ) return scene.GetAllComponents<RPPlayer>().FirstOrDefault();
		return null;
	}

	public static List<RPPlayer> All()
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return new List<RPPlayer>();
		return scene.GetAllComponents<RPPlayer>().ToList();
	}

	public static Connection ConnOf( RPPlayer p )
	{
		if ( p == null ) return null;
		if ( p.Conn != null ) return p.Conn;
		return p.Network.Owner;
	}

	/// <summary>The player's body in the world (null while dead).</summary>
	public static Player Pawn( Connection c ) => c == null ? null : Player.FindForConnection( c );

	public static Connection ConnOfPawn( GameObject go )
	{
		if ( !go.IsValid() ) return null;
		var ply = go.Components.Get<Player>( FindMode.EverythingInSelfAndAncestors );
		if ( ply == null ) return null;
		return ply.Network.Owner;
	}

	public static bool Alive( Connection c )
	{
		var p = Pawn( c );
		return p != null && p.Health > 0;
	}

	public static string Name( Connection c )
	{
		var rp = Of( c );
		if ( rp != null && !string.IsNullOrEmpty( rp.RPName ) ) return rp.RPName;
		return c != null ? c.DisplayName : "Someone";
	}

	public static string NameOf( RPPlayer rp )
	{
		if ( rp == null ) return "Someone";
		if ( !string.IsNullOrEmpty( rp.RPName ) ) return rp.RPName;
		var c = ConnOf( rp );
		return c != null ? c.DisplayName : "Someone";
	}

	public static RPJob Job( Connection c ) => JobOf( Of( c ) );

	public static RPJob JobOf( RPPlayer rp )
	{
		var reg = DarkRP.Registry();
		if ( reg == null ) return null;
		if ( rp == null ) return reg.DefaultJob();
		return reg.Job( rp.JobCommand ) ?? reg.DefaultJob();
	}

	public static string JobName( RPPlayer rp )
	{
		if ( rp == null ) return "";
		if ( !string.IsNullOrEmpty( rp.JobTitle ) ) return rp.JobTitle;
		var j = JobOf( rp );
		return j != null ? j.Name : rp.JobCommand;
	}

	public static Color JobColor( RPPlayer rp )
	{
		var j = JobOf( rp );
		return j != null ? RPUtil.ParseColor( j.Color ) : Color.White;
	}

	public static bool IsCP( Connection c ) { var j = Job( c ); return j != null && j.Police; }
	public static bool IsMayor( Connection c ) { var j = Job( c ); return j != null && j.Mayor; }
	public static bool IsChief( Connection c ) { var j = Job( c ); return j != null && j.Chief; }

	/// <summary>Admins: Sandbox admins, the host, or the darkrp.admin permission.</summary>
	public static bool IsAdmin( Connection c )
	{
		if ( c == null ) return false;
		if ( c.IsHost ) return true;
		return Permissions.Has( c, DarkRP.AdminPermission ) || Permissions.Has( c, Permissions.Admin );
	}

	public static bool HasPermission( Connection c, string permission )
	{
		if ( string.IsNullOrEmpty( permission ) ) return true;
		if ( permission == "admin" ) return IsAdmin( c );
		if ( IsAdmin( c ) ) return true;
		return Permissions.Has( c, permission );
	}

	// ---- money ----

	public static long Money( Connection c ) { var rp = Of( c ); return rp != null ? rp.Money : 0; }

	public static bool CanAfford( Connection c, long amount ) => amount <= 0 || Money( c ) >= amount;

	/// <summary>Adds (or with a negative amount takes) money. Host only.</summary>
	public static void AddMoney( Connection c, long amount )
	{
		var rp = Of( c );
		if ( rp == null || amount == 0 ) return;
		var old = rp.Money;
		rp.Money = Math.Max( 0, rp.Money + amount );
		rp.Dirty = true;
		DarkRP.RunHook( new RPHook { Name = "playerWalletChanged", Player = c, Amount = amount, Data = old } );
	}

	/// <summary>Moves money between two players if the payer can afford it.</summary>
	public static bool Pay( Connection from, Connection to, long amount )
	{
		if ( amount <= 0 || !CanAfford( from, amount ) ) return false;
		AddMoney( from, -amount );
		AddMoney( to, amount );
		return true;
	}

	public static bool ChangeTeam( Connection c, RPJob job, bool force, bool viaVote ) => RPJobs.ChangeTeam( c, job, force, viaVote );

	/// <summary>Finds a player by part of their RP name, Steam name, or steam id.</summary>
	public static Connection Find( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) ) return null;
		text = text.Trim();
		Connection partial = null;
		foreach ( var rp in All() )
		{
			var c = ConnOf( rp );
			if ( c == null ) continue;
			var rpName = rp.RPName ?? "";
			if ( rp.SteamIdText == text ) return c;
			if ( string.Equals( rpName, text, StringComparison.OrdinalIgnoreCase ) || string.Equals( c.DisplayName, text, StringComparison.OrdinalIgnoreCase ) ) return c;
			if ( partial == null && (rpName.IndexOf( text, StringComparison.OrdinalIgnoreCase ) >= 0 || c.DisplayName.IndexOf( text, StringComparison.OrdinalIgnoreCase ) >= 0) ) partial = c;
		}
		return partial;
	}

	/// <summary>The player the given one is looking at within range (host and clients).</summary>
	public static Connection AimTarget( Connection c, float range )
	{
		var ply = Pawn( c );
		if ( ply == null ) return null;
		var go = AimObject( ply, range );
		return go != null ? ConnOfPawn( go ) : null;
	}

	/// <summary>What a player's eyes point at, within range.</summary>
	public static GameObject AimObject( Player ply, float range )
	{
		if ( ply == null ) return null;
		var eye = ply.EyeTransform;
		var tr = ply.Scene.Trace.Ray( eye.Position, eye.Position + eye.Forward * range )
			.IgnoreGameObjectHierarchy( ply.GameObject )
			.WithoutTags( "trigger" )
			.UseHitboxes()
			.Run();
		return tr.Hit ? tr.GameObject : null;
	}

	public static float Distance( Connection a, Connection b )
	{
		var pa = Pawn( a );
		var pb = Pawn( b );
		if ( pa == null || pb == null ) return float.MaxValue;
		return pa.WorldPosition.Distance( pb.WorldPosition );
	}

	/// <summary>Per-player cooldown: 0 if allowed (and starts it), else the seconds left.</summary>
	public static float CooldownLeft( RPPlayer rp, string key, float seconds )
	{
		if ( rp == null ) return 0;
		var now = Time.Now;
		if ( rp.Cooldowns.ContainsKey( key ) && rp.Cooldowns[key] > now ) return rp.Cooldowns[key] - now;
		rp.Cooldowns[key] = now + seconds;
		return 0;
	}
}
