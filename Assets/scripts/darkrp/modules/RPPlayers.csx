// DarkRP for s&box - joining, leaving and saving players (DarkRP's MySQLite player data: wallet and RP name).
using System.Text.Json.Nodes;

public static class RPPlayers
{
	public static void EnsureAll( RPWorld w )
	{
		foreach ( var pd in w.Scene.GetAllComponents<PlayerData>().ToList() )
		{
			var rp = pd.GameObject.Components.Get<RPPlayer>();
			if ( rp == null || !rp.Loaded ) Join( w, pd );
		}
	}

	/// <summary>Host: gives a joining player their DarkRP state (saved wallet and name, default job).</summary>
	public static void Join( RPWorld w, PlayerData pd )
	{
		if ( pd == null ) return;
		var conn = pd.Network.Owner;
		var rp = pd.GameObject.Components.Get<RPPlayer>();
		bool added = false;
		if ( rp == null )
		{
			rp = pd.GameObject.Components.Create<RPPlayer>();
			added = true;
		}
		if ( rp.Loaded ) return;
		var reg = DarkRP.Registry();
		rp.Conn = conn;
		rp.SteamIdText = conn != null ? conn.SteamId.ToString() : "0";
		long sid = 0;
		long.TryParse( rp.SteamIdText, out sid );
		rp.SteamId = sid;
		rp.Money = reg.Config.Int( "startingmoney" );
		rp.RPName = "";
		var data = sid != 0 ? RPDataFiles.ReadPlayer( sid ) : null;
		if ( data != null )
		{
			rp.Money = RPJson.Long( data, "money", rp.Money );
			rp.RPName = RPJson.Str( data, "rpname", "" );
		}
		var job = reg.DefaultJob();
		rp.JobCommand = job != null ? job.Command : "";
		rp.Salary = job != null ? job.Salary : 0;
		rp.License = job != null && job.HasLicense;
		rp.Energy = 100;
		rp.Loaded = true;
		rp.NextSave = Time.Now + 60;
		if ( added && pd.GameObject.Network.Active ) pd.GameObject.Network.Refresh();
		RPLog.Write( (conn != null ? conn.DisplayName : "?") + " (" + rp.SteamIdText + ") joined with " + RPUtil.Money( rp.Money ) );
		DarkRP.RunHook( new RPHook { Name = "playerInitialSpawn", Player = conn } );
		RPPropProtection.OnJoin( conn );
		if ( data == null ) Save( rp );
		if ( RP.Alive( conn ) ) RPJobs.QueueLoadout( conn, true );
		RPNet.ChatAll( RP.Name( conn ) + " joined the city.", "#9aa4ae" );
	}

	public static void Leave( RPWorld w, PlayerData pd )
	{
		var rp = pd != null ? pd.GameObject.Components.Get<RPPlayer>() : null;
		if ( rp == null ) return;
		var conn = RP.ConnOf( rp );
		Save( rp );
		DarkRP.RunHook( new RPHook { Name = "playerDisconnected", Player = conn } );
		var job = RP.JobOf( rp );
		if ( job != null && job.Mayor && DarkRP.Config().Bool( "shouldResetLaws" ) ) RPMayor.ResetLaws( w );
		if ( job != null && job.Mayor ) RPEntities.RemoveLawBoards( rp );
		RPDoors.UnownAll( w, rp );
		RPVotes.PlayerLeft( conn );
		RPPropProtection.OnLeave( rp, conn );
		if ( DarkRP.Config().Bool( "removeondisconnect" ) )
		{
			foreach ( var go in rp.OwnedEntities.ToList() ) if ( go.IsValid() ) go.Destroy();
			rp.OwnedEntities.Clear();
		}
		RPLog.Write( (conn != null ? conn.DisplayName : "?") + " left with " + RPUtil.Money( rp.Money ) );
	}

	public static void Save( RPPlayer rp )
	{
		if ( rp == null || rp.SteamId == 0 || !rp.Loaded ) return;
		var c = RP.ConnOf( rp );
		var wr = new RPJsonWriter();
		wr.BeginObject();
		wr.Num( "money", rp.Money ).Str( "rpname", rp.RPName ?? "" ).Str( "name", c != null ? c.DisplayName : "" ).Str( "lastSeen", DateTime.UtcNow.ToString( "yyyy-MM-dd HH:mm" ) );
		wr.EndObject();
		RPDataFiles.WritePlayer( rp.SteamId, wr.ToString() );
		rp.Dirty = false;
	}

	public static void SaveAll()
	{
		foreach ( var rp in RP.All() ) Save( rp );
		RPLog.Flush();
	}

	public static void AutoSave( RPWorld w )
	{
		foreach ( var rp in RP.All() )
		{
			if ( rp.Dirty && Time.Now >= rp.NextSave )
			{
				Save( rp );
				rp.NextSave = Time.Now + 30;
			}
		}
		if ( ((int)Time.Now) % 10 == 0 ) RPLog.Flush();
	}

	/// <summary>Sets a player's money even while they're offline (admin /setmoney with a steam id).</summary>
	public static bool SetOfflineMoney( long steamId, long money )
	{
		var data = RPDataFiles.ReadPlayer( steamId );
		if ( data == null ) return false;
		var wr = new RPJsonWriter();
		wr.BeginObject();
		wr.Num( "money", money ).Str( "rpname", RPJson.Str( data, "rpname", "" ) ).Str( "name", RPJson.Str( data, "name", "" ) ).Str( "lastSeen", RPJson.Str( data, "lastSeen", "" ) );
		wr.EndObject();
		RPDataFiles.WritePlayer( steamId, wr.ToString() );
		return true;
	}
}

/// <summary>Per-map data: jail positions, job spawn points, door settings (data/darkrp/maps/&lt;map&gt;.json).</summary>
public sealed class RPMapData
{
	public readonly List<Vector3> JailPositions = new List<Vector3>();
	/// <summary>Job command → spawn positions.</summary>
	public readonly Dictionary<string, List<Vector3>> Spawns = new Dictionary<string, List<Vector3>>();
	/// <summary>Door key → "ownable|group|teams|title" settings set by admins.</summary>
	public readonly Dictionary<string, string> DoorSettings = new Dictionary<string, string>();
	public string Name = "";
}

public sealed class RPMapHolder
{
	public RPMapData Data = new RPMapData();
}

public sealed class RPMap
{
	static readonly RPMapHolder H = new RPMapHolder();
	public static RPMapData Data() => H.Data;

	public static void Load( RPWorld w )
	{
		var d = new RPMapData { Name = RPUtil.MapName( w.Scene ) };
		var o = RPDataFiles.ReadMap( d.Name );
		if ( o != null )
		{
			var jail = RPJson.Arr( o, "jail" );
			if ( jail != null ) foreach ( var n in jail ) d.JailPositions.Add( ParseVec( n.ToString() ) );
			var spawns = RPJson.Arr( o, "spawns" );
			if ( spawns != null )
			{
				foreach ( var n in spawns )
				{
					var x = n.AsObject();
					var list = new List<Vector3>();
					foreach ( var s in RPJson.StrArray( x, "positions", new string[0] ) ) list.Add( ParseVec( s ) );
					d.Spawns[RPJson.Str( x, "job", "" )] = list;
				}
			}
			var doors = RPJson.Arr( o, "doors" );
			if ( doors != null ) foreach ( var n in doors ) { var x = n.AsObject(); d.DoorSettings[RPJson.Str( x, "door", "" )] = RPJson.Str( x, "settings", "" ); }
		}
		H.Data = d;
		w.MapLoaded = true;
		RPDoors.ApplyMapSettings( w );
	}

	public static void Save( RPWorld w )
	{
		var d = H.Data;
		if ( string.IsNullOrEmpty( d.Name ) ) return;
		var wr = new RPJsonWriter();
		wr.BeginObject();
		wr.Str( "_readme", "DarkRP data for this map: jail positions (/jailpos, /addjailpos), job spawns (/setspawn, /addspawn) and door settings (door menu, admin)." );
		wr.StrArray( "jail", d.JailPositions.Select( v => VecStr( v ) ).ToArray() );
		wr.BeginArray( "spawns" );
		foreach ( var key in d.Spawns.Keys ) { wr.BeginObject(); wr.Str( "job", key ).StrArray( "positions", d.Spawns[key].Select( v => VecStr( v ) ).ToArray() ); wr.EndObject(); }
		wr.EndArray();
		wr.BeginArray( "doors" );
		foreach ( var key in d.DoorSettings.Keys ) { wr.BeginObject(); wr.Str( "door", key ).Str( "settings", d.DoorSettings[key] ); wr.EndObject(); }
		wr.EndArray();
		wr.EndObject();
		RPDataFiles.WriteMap( d.Name, wr.ToString() );
	}

	public static string VecStr( Vector3 v ) => RPUtil.FloatStr( v.x ) + " " + RPUtil.FloatStr( v.y ) + " " + RPUtil.FloatStr( v.z );

	public static Vector3 ParseVec( string s )
	{
		var p = (s ?? "").Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		if ( p.Length < 3 ) return Vector3.Zero;
		return new Vector3( RPUtil.ParseFloat( p[0], 0 ), RPUtil.ParseFloat( p[1], 0 ), RPUtil.ParseFloat( p[2], 0 ) );
	}

	public static Vector3? RandomJail()
	{
		var j = H.Data.JailPositions;
		if ( j.Count == 0 ) return null;
		return j[Game.Random.Int( 0, j.Count - 1 )];
	}

	public static Vector3? RandomSpawn( string job )
	{
		if ( string.IsNullOrEmpty( job ) || !H.Data.Spawns.ContainsKey( job ) ) return null;
		var l = H.Data.Spawns[job];
		if ( l.Count == 0 ) return null;
		return l[Game.Random.Int( 0, l.Count - 1 )];
	}
}
