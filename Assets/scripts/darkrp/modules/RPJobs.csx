// DarkRP for s&box - jobs: changing team (checks, votes, limits, demotions), loadouts, spawn points and outfits.
// Ported from DarkRP's modules/jobs and gamemode/player.lua (MIT, Falco Peijnenburg).

public sealed class RPPendingSpawn
{
	public Connection Conn;
	public float At;
	/// <summary>Replace whatever the player carries with the job loadout (first join, Sandbox's loadout came first).</summary>
	public bool Strip;
	/// <summary>Spawn a new body (the old one was destroyed a moment ago).</summary>
	public bool Spawn;
}

public sealed class RPJobsHolder
{
	public readonly List<RPPendingSpawn> Pending = new List<RPPendingSpawn>();
}

public static class RPJobs
{
	static readonly RPJobsHolder H = new RPJobsHolder();

	public static void Reset() => H.Pending.Clear();

	/// <summary>Weapons that can never be dropped (DarkRP's DisallowDrop).</summary>
	public static readonly string[] DisallowDrop = new[] { "arrest_stick", "door_ram", "camera", "toolgun", "keys", "lockpick", "med_kit", "pocket", "stunstick", "unarrest_stick", "weapon_keypadchecker", "physgun", "weaponchecker", "fists" };

	public static int CountJob( string command )
	{
		int n = 0;
		foreach ( var rp in RP.All() ) if ( rp.JobCommand == command ) n++;
		return n;
	}

	/// <summary>How many players may be in this job right now (0 = unlimited).</summary>
	public static int MaxFor( RPJob job )
	{
		if ( job.Max <= 0 ) return 0;
		if ( job.Max < 1 ) return Math.Max( 1, (int)Math.Ceiling( job.Max * RP.All().Count ) );
		return (int)job.Max;
	}

	/// <summary>Can this player become the job? Returns false with a reason. Ignores votes.</summary>
	public static string ChangeTeamBlocker( Connection c, RPJob job, bool force )
	{
		var rp = RP.Of( c );
		var cfg = DarkRP.Config();
		if ( rp == null || job == null ) { return "That job doesn't exist."; }
		if ( force ) return null;
		bool admin = RP.IsAdmin( c );
		bool bypass = admin && cfg.Bool( "adminBypassJobRestrictions" );
		if ( rp.JobCommand == job.Command ) { return "You're already a " + job.Name + "."; }
		if ( !cfg.Bool( "allowjobswitch" ) && !admin ) { return "Job switching is disabled."; }
		if ( cfg.Bool( "restrictallteams" ) && !admin ) { return "Only admins can change jobs."; }
		if ( rp.Arrested ) { return "You can't change jobs while arrested."; }
		if ( job.Cook && !cfg.Bool( "hungermod" ) ) { return "The hunger module is off."; }
		if ( job.Admin == 1 && !admin ) { return job.Name + " is for admins only."; }
		if ( job.Admin >= 2 && !c.IsHost ) { return job.Name + " is for the server owner only."; }
		if ( !bypass )
		{
			if ( job.NeedToChangeFrom != null && job.NeedToChangeFrom.Length > 0 && !RPUtil.ContainsIgnoreCase( job.NeedToChangeFrom, rp.JobCommand ) )
			{
				var names = job.NeedToChangeFrom.Select( x => DarkRP.Registry().Job( x ) != null ? DarkRP.Registry().Job( x ).Name : x );
				return "You need to be a " + string.Join( " or ", names ) + " first.";
			}
			var max = MaxFor( job );
			if ( max > 0 && CountJob( job.Command ) >= max ) { return job.Name + " is full (" + max + " max)."; }
			if ( rp.DemotedUntil.ContainsKey( job.Command ) && rp.DemotedUntil[job.Command] > Time.Now )
			{
				return "You were demoted from " + job.Name + ". Wait " + RPUtil.Duration( rp.DemotedUntil[job.Command] - Time.Now ) + ".";
			}
			var wait = cfg.Int( "changejobtime" ) - (Time.Now - rp.LastJobChange);
			if ( wait > 0 ) { return "Wait " + (int)Math.Ceiling( wait ) + "s before changing jobs again."; }
		}
		if ( job.CustomCheck != null )
		{
			bool ok = true;
			try { ok = job.CustomCheck( c ); } catch ( Exception e ) { Log.Warning( "DarkRP: customCheck of " + job.Command + " failed: " + e.Message ); ok = false; }
			if ( !ok ) { return string.IsNullOrEmpty( job.CustomCheckFailMsg ) ? "You can't become a " + job.Name + "." : job.CustomCheckFailMsg; }
		}
		var h = DarkRP.RunHook( new RPHook { Name = "canChangeJob", Player = c, Job = job, OldJob = RP.JobOf( rp ) } );
		if ( !h.Allowed ) { return string.IsNullOrEmpty( h.Reason ) ? "You can't become a " + job.Name + "." : h.Reason; }
		return null;
	}

	/// <summary>Request from a player (chat command / menu): checks, starts a vote for vote jobs, or changes.</summary>
	public static void Request( Connection c, RPJob job )
	{
		if ( job == null ) { DarkRP.Notify( c, 1, 4, "That job doesn't exist." ); return; }
		var reason = ChangeTeamBlocker( c, job, false );
		if ( reason != null ) { DarkRP.Notify( c, 1, 4, reason ); return; }
		if ( job.Vote && !(RP.IsAdmin( c ) && DarkRP.Config().Bool( "adminBypassJobRestrictions" )) )
		{
			RPVotes.StartJobVote( c, job );
			return;
		}
		ChangeTeam( c, job, false, false );
	}

	/// <summary>Changes the player's job. With <paramref name="force"/> no checks run (admin /setjob, demotions).</summary>
	public static bool ChangeTeam( Connection c, RPJob job, bool force, bool viaVote )
	{
		var rp = RP.Of( c );
		if ( rp == null || job == null ) return false;
		var reason = (!force && !viaVote) ? ChangeTeamBlocker( c, job, false ) : null;
		if ( reason != null ) { DarkRP.Notify( c, 1, 4, reason ); return false; }
		var cfg = DarkRP.Config();
		var old = RP.JobOf( rp );
		var w = RPWorld.Current();

		rp.JobCommand = job.Command;
		rp.JobTitle = "";
		rp.Salary = job.Salary;
		rp.LastJobChange = Time.Now;
		if ( job.HasLicense ) rp.License = true;
		else if ( cfg.Bool( "revokeLicenseOnJobChange" ) ) rp.License = false;

		if ( old != null && old.Mayor && !job.Mayor && w != null )
		{
			if ( w.Lockdown ) RPMayor.EndLockdown( w, null );
			if ( cfg.Bool( "shouldResetLaws" ) ) RPMayor.ResetLaws( w );
			RPEntities.RemoveLawBoards( rp );
		}
		if ( cfg.Bool( "removeclassitems" ) ) RemoveClassItems( rp, job );

		if ( cfg.Bool( "norespawn" ) || !RP.Alive( c ) )
		{
			if ( RP.Alive( c ) ) GiveLoadout( c, true );
		}
		else
		{
			Respawn( c );
		}

		DarkRP.NotifyAll( 0, 4, RP.Name( c ) + " has been made a " + job.Name + "!" );
		RPLog.Write( RP.Name( c ) + " (" + rp.SteamIdText + ") changed job from " + (old != null ? old.Name : "?") + " to " + job.Name );
		DarkRP.RunHook( new RPHook { Name = "OnPlayerChangedTeam", Player = c, Job = job, OldJob = old } );
		return true;
	}

	/// <summary>Back to the default job, banned from the old one for "demotetime" seconds.</summary>
	public static void Demote( Connection c, string reason )
	{
		var rp = RP.Of( c );
		if ( rp == null ) return;
		var old = RP.JobOf( rp );
		var def = DarkRP.Registry().DefaultJob();
		if ( old == null || def == null || old.Command == def.Command ) return;
		var until = Time.Now + DarkRP.Config().Int( "demotetime" );
		rp.DemotedUntil[old.Command] = until;
		foreach ( var g in DarkRP.Registry().DemoteGroups )
			if ( RPUtil.ContainsIgnoreCase( g.Jobs, old.Command ) ) foreach ( var j in g.Jobs ) rp.DemotedUntil[j.ToLowerInvariant()] = until;
		ChangeTeam( c, def, true, false );
		DarkRP.NotifyAll( 0, 5, RP.Name( c ) + " has been demoted from " + old.Name + (string.IsNullOrEmpty( reason ) ? "." : ": " + reason) );
		DarkRP.RunHook( new RPHook { Name = "onPlayerDemoted", Player = c, Job = old, Reason = reason ?? "" } );
	}

	static void RemoveClassItems( RPPlayer rp, RPJob job )
	{
		var reg = DarkRP.Registry();
		foreach ( var go in rp.OwnedEntities.ToList() )
		{
			if ( !go.IsValid() ) continue;
			var ent = go.Components.Get<RPEntity>();
			if ( ent == null || string.IsNullOrEmpty( ent.DefCmd ) ) continue;
			var def = reg.EntityDef( ent.DefCmd );
			if ( def != null && def.Allowed != null && def.Allowed.Length > 0 && !RPUtil.ContainsIgnoreCase( def.Allowed, job.Command ) )
			{
				rp.OwnedEntities.Remove( go );
				go.Destroy();
			}
		}
	}

	// ---- loadout ----

	/// <summary>Weapon prefab paths a player of this job gets (defaults + job).</summary>
	public static List<string> LoadoutPrefabs( RPJob job )
	{
		var reg = DarkRP.Registry();
		var list = new List<string>();
		foreach ( var id in reg.DefaultWeapons ) { var p = reg.WeaponPrefab( id ); if ( !list.Contains( p ) ) list.Add( p ); }
		if ( job != null && job.Weapons != null ) foreach ( var id in job.Weapons ) { var p = reg.WeaponPrefab( id ); if ( !list.Contains( p ) ) list.Add( p ); }
		return list;
	}

	/// <summary>Host: strips (optionally) and gives the job's weapons and ammo.</summary>
	public static void GiveLoadout( Connection c, bool strip )
	{
		var ply = RP.Pawn( c );
		var rp = RP.Of( c );
		if ( ply == null || rp == null ) return;
		var inv = ply.GetComponent<PlayerInventory>();
		if ( inv == null ) return;
		if ( strip ) foreach ( var w in inv.Weapons.ToList() ) inv.Remove( w );
		var job = RP.JobOf( rp );
		foreach ( var prefab in LoadoutPrefabs( job ) )
		{
			try { inv.Pickup( prefab, false ); }
			catch ( Exception e ) { Log.Warning( "DarkRP: can't give " + prefab + ": " + e.Message ); }
		}
		GiveJobAmmo( c, job );
		DarkRP.RunHook( new RPHook { Name = "PlayerLoadout", Player = c, Job = job } );
	}

	public static void GiveJobAmmo( Connection c, RPJob job )
	{
		if ( job == null || job.Ammo == null ) return;
		foreach ( var entry in job.Ammo )
		{
			var parts = entry.Split( ':' );
			int amount = parts.Length > 1 ? (int)RPUtil.ParseFloat( parts[1], 0 ) : 30;
			RPShop.GiveAmmo( c, parts[0], amount );
		}
	}

	public static bool IsDisallowedDrop( string prefabPath )
	{
		var reg = DarkRP.Registry();
		foreach ( var id in DisallowDrop )
			if ( string.Equals( reg.WeaponPrefab( id ), prefabPath, StringComparison.OrdinalIgnoreCase ) ) return true;
		return false;
	}

	// ---- events ----

	public static void OnLoadout( RPWorld w, PlayerLoadoutEvent e )
	{
		if ( e.Player == null ) return;
		var c = e.Player.Network.Owner;
		var rp = RP.Of( c );
		var job = RP.JobOf( rp );
		var items = new List<GameObject>();
		if ( rp != null && rp.Arrested ) { e.Items = items; return; }
		foreach ( var path in LoadoutPrefabs( job ) )
		{
			var prefab = GameObject.GetPrefab( path );
			if ( prefab != null ) items.Add( prefab );
			else Log.Warning( "DarkRP: weapon prefab not found: " + path );
		}
		e.Items = items;
	}

	public static void OnSpawned( RPWorld w, Player player )
	{
		var c = player.Network.Owner;
		var rp = RP.Of( c );
		if ( rp == null ) { RPPlayers.EnsureAll( w ); rp = RP.Of( c ); }
		if ( rp == null ) return;
		rp.SpawnedAt = Time.Now;
		H.Pending.Add( new RPPendingSpawn { Conn = c, At = Time.Now + 0.6f } );
	}

	/// <summary>Host: give the job loadout shortly (used when a player is already alive as DarkRP starts).</summary>
	/// <summary>
	/// Host: respawns a player. Sandbox's GameManager.Respawn refuses to spawn while the old body still exists (it's
	/// only destroyed at the end of the frame), so the new body comes a moment later.
	/// </summary>
	public static void Respawn( Connection c )
	{
		var ply = RP.Pawn( c );
		if ( ply != null ) ply.GameObject.Destroy();
		H.Pending.Add( new RPPendingSpawn { Conn = c, At = Time.Now + 0.1f, Spawn = true } );
	}

	public static void QueueLoadout( Connection c, bool strip )
	{
		H.Pending.Add( new RPPendingSpawn { Conn = c, At = Time.Now + 0.3f, Strip = strip } );
	}

	/// <summary>Runs shortly after spawning, when the loadout has arrived: health, ammo, jail, hooks.</summary>
	static void AfterSpawn( Connection c, bool strip )
	{
		if ( strip ) GiveLoadout( c, true );
		var rp = RP.Of( c );
		var ply = RP.Pawn( c );
		if ( rp == null || ply == null ) return;
		var job = RP.JobOf( rp );
		var cfg = DarkRP.Config();
		float hp = job != null && job.StartHealth > 0 ? job.StartHealth : cfg.Int( "startinghealth" );
		ply.MaxHealth = Math.Max( 100, hp );
		ply.Health = hp;
		if ( job != null && job.StartArmor > 0 ) ply.Armour = job.StartArmor;
		if ( rp.Arrested )
		{
			RPPolice.StripForJail( c );
			return;
		}
		GiveJobAmmo( c, job );
		if ( cfg.Bool( "hungermod" ) ) rp.Energy = 100;
		if ( job != null && job.OnSpawn != null )
		{
			try { job.OnSpawn( c ); } catch ( Exception e ) { Log.Warning( "DarkRP: " + job.Command + " OnSpawn failed: " + e.Message ); }
		}
		DarkRP.RunHook( new RPHook { Name = "PlayerSpawn", Player = c, Job = job } );
	}

	public static void Tick( RPWorld w )
	{
		for ( int i = H.Pending.Count - 1; i >= 0; i-- )
		{
			var p = H.Pending[i];
			if ( Time.Now < p.At ) continue;
			H.Pending.RemoveAt( i );
			if ( p.Spawn )
			{
				var gm = RPUtil.GM();
				var pd = PlayerData.For( p.Conn );
				if ( gm != null && pd != null && RP.Pawn( p.Conn ) == null ) gm.SpawnPlayer( pd, false );
				continue;
			}
			try { AfterSpawn( p.Conn, p.Strip ); } catch ( Exception e ) { Log.Warning( "DarkRP spawn: " + e.Message ); }
		}
	}

	public static void OnRespawning( RPWorld w, PlayerRespawnEvent e )
	{
		if ( e.PlayerData == null ) return;
		var c = e.PlayerData.Network.Owner;
		var rp = RP.Of( c );
		if ( rp == null ) return;
		if ( rp.Arrested && DarkRP.Config().Bool( "respawninjail" ) )
		{
			var jail = RPMap.RandomJail();
			if ( jail.HasValue ) { e.SpawnLocation = new Transform( jail.Value + Vector3.Up * 8, e.SpawnLocation.Rotation ); return; }
		}
		var pos = RPMap.RandomSpawn( rp.JobCommand );
		if ( pos.HasValue ) e.SpawnLocation = new Transform( pos.Value + Vector3.Up * 8, e.SpawnLocation.Rotation );
	}

	public static void OnDied( RPWorld w, Player player, PlayerDiedParams args )
	{
		var c = player.Network.Owner;
		var rp = RP.Of( c );
		if ( rp == null ) return;
		var cfg = DarkRP.Config();
		var job = RP.JobOf( rp );

		var fee = cfg.Int( "deathfee" );
		if ( fee > 0 && rp.Money > 0 && !rp.Arrested )
		{
			var lost = Math.Min( fee, rp.Money );
			RP.AddMoney( c, -lost );
			if ( cfg.Bool( "dropmoneyondeath" ) ) RPEntities.SpawnMoney( player.WorldPosition + Vector3.Up * 20, lost );
			DarkRP.Notify( c, 0, 4, "You lost " + RPUtil.Money( lost ) + " because you died." );
		}
		if ( cfg.Bool( "hungermod" ) ) rp.Energy = 100;
		RPPocket.DropAll( c, player.WorldPosition );
		if ( job != null && job.OnDeath != null )
		{
			try { job.OnDeath( c ); } catch ( Exception e ) { Log.Warning( "DarkRP: " + job.Command + " OnDeath failed: " + e.Message ); }
		}
		var attacker = RP.ConnOfPawn( args.Instigator );
		RPHits.OnDeath( c, attacker );
		DarkRP.RunHook( new RPHook { Name = "PlayerDeath", Player = c, Target = attacker, Job = job } );
		RPLog.Write( RP.Name( c ) + " died" + (attacker != null && attacker != c ? " (killed by " + RP.Name( attacker ) + ")" : "") );
	}

	public static void OnDrop( PlayerDropEvent e )
	{
		if ( e.Weapon == null || e.Player == null ) return;
		var c = e.Player.Network.Owner;
		var path = e.Weapon.GameObject.PrefabInstanceSource ?? "";
		if ( IsDisallowedDrop( path ) ) { e.Cancelled = true; DarkRP.Notify( c, 1, 3, "You can't drop this weapon." ); return; }
		var rp = RP.Of( c );
		if ( rp == null ) return;
		if ( rp.Arrested ) { e.Cancelled = true; return; }
		var job = RP.JobOf( rp );
		if ( DarkRP.Config().Bool( "restrictdrop" ) && !rp.GivenWeapons.Contains( path.ToLowerInvariant() ) ) { e.Cancelled = true; DarkRP.Notify( c, 1, 3, "You can only drop weapons you bought." ); return; }
		var h = DarkRP.RunHook( new RPHook { Name = "canDropWeapon", Player = c, Text = path, Entity = e.Weapon.GameObject } );
		if ( !h.Allowed ) { e.Cancelled = true; if ( !string.IsNullOrEmpty( h.Reason ) ) DarkRP.Notify( c, 1, 3, h.Reason ); }
	}
}

// ---- outfits (every machine applies what the host decided) ----

public sealed class RPLookState
{
	public string Key = "";
	public bool Forced;
	/// <summary>The dresser as it was before DarkRP dressed it (serialized), to give the avatar back.</summary>
	public string OrigJson;
	public Model OrigModel;
	public int Tries;
}

public sealed class RPLooksHolder
{
	public readonly Dictionary<Guid, RPLookState> States = new Dictionary<Guid, RPLookState>();
	public int Errors;
}

/// <summary>
/// Dresses every player body in its job's outfit or model (runs on every machine, from synced job data).
/// Scripts can't use ClothingContainer, so the Dresser is written through its own serialized form.
/// </summary>
public static class RPLooks
{
	static readonly RPLooksHolder H = new RPLooksHolder();

	public static void Reset() { H.States.Clear(); H.Errors = 0; }

	public static void Update( Scene scene )
	{
		if ( H.Errors > 20 ) return;
		try { UpdateAll( scene ); }
		catch ( Exception e )
		{
			H.Errors++;
			Log.Warning( "DarkRP outfits: " + e.Message + (H.Errors > 20 ? " (outfits disabled after repeated errors)" : "") );
		}
	}

	static void UpdateAll( Scene scene )
	{
		var reg = RPRegistries.For( scene );
		if ( reg == null || !reg.Built ) return;
		if ( !reg.Config.Bool( "enforceplayermodel" ) ) return;
		foreach ( var ply in scene.GetAllComponents<Player>() )
		{
			var rp = RP.Of( ply.Network.Owner );
			if ( rp == null || string.IsNullOrEmpty( rp.JobCommand ) ) continue;
			var job = reg.Job( rp.JobCommand );
			if ( job == null ) continue;
			var key = job.Command + "|" + (job.Outfit == null ? "~" : string.Join( ";", job.Outfit )) + "|" + string.Join( ";", job.Models ?? new string[0] );
			var id = ply.GameObject.Id;
			if ( !H.States.ContainsKey( id ) ) H.States[id] = new RPLookState();
			var st = H.States[id];
			if ( st.Key == key ) continue;
			if ( Apply( ply, job, st ) ) st.Key = key;
		}
		if ( H.States.Count > 128 ) Prune( scene );
	}

	static void Prune( Scene scene )
	{
		var alive = scene.GetAllComponents<Player>().Select( p => p.GameObject.Id ).ToList();
		foreach ( var id in H.States.Keys.ToList() ) if ( !alive.Contains( id ) ) H.States.Remove( id );
	}

	static void SetNode( System.Text.Json.Nodes.JsonObject o, string key, string json )
	{
		if ( o.ContainsKey( key ) ) o.Remove( key );
		var tmp = Json.ParseToJsonObject( "{\"v\":" + json + "}" );
		var node = tmp["v"];
		tmp.Remove( "v" );
		o.Add( key, node );
	}

	static bool Apply( Player ply, RPJob job, RPLookState st )
	{
		var body = ply.Body;
		if ( !body.IsValid() ) return false;
		var dresser = body.GetComponentInChildren<Dresser>( true );
		var renderer = body.GetComponentInChildren<SkinnedModelRenderer>( true );
		if ( renderer == null ) return false;
		if ( st.OrigModel == null ) st.OrigModel = renderer.Model;

		// Player model jobs.
		if ( job.Models != null && job.Models.Length > 0 )
		{
			var path = job.Models[Math.Abs( ply.Network.Owner != null ? ply.Network.Owner.Id.GetHashCode() : 0 ) % job.Models.Length];
			var m = Model.Load( path );
			if ( m == null || m.IsError ) { st.Tries++; return st.Tries > 20; }
			if ( dresser != null && dresser.Enabled ) { dresser.Clear(); dresser.Enabled = false; }
			renderer.Model = m;
			return true;
		}
		if ( st.OrigModel != null && renderer.Model != st.OrigModel ) renderer.Model = st.OrigModel;
		if ( dresser == null ) return true;
		if ( !dresser.Enabled ) dresser.Enabled = true;

		if ( job.Outfit == null )
		{
			// The player's own avatar.
			if ( st.Forced && !string.IsNullOrEmpty( st.OrigJson ) )
			{
				dresser.DeserializeImmediately( Json.ParseToJsonObject( st.OrigJson ) );
				var t = dresser.Apply();
				st.Forced = false;
			}
			return true;
		}

		if ( !st.Forced ) st.OrigJson = dresser.Serialize().ToJsonString();
		var items = new List<string>();
		foreach ( var item in job.Outfit )
		{
			var path = item;
			string tint = "";
			var at = item.IndexOf( '@' );
			if ( at > 0 ) { path = item.Substring( 0, at ); tint = RPUtil.FloatStr( Math.Clamp( RPUtil.ParseFloat( item.Substring( at + 1 ), 0 ), 0f, 1f ) ); }
			items.Add( "{\"Clothing\":" + RPJsonWriter.Quote( path ) + (tint != "" ? ",\"Tint\":" + tint : "") + "}" );
		}
		var o = dresser.Serialize();
		SetNode( o, "Source", "\"Manual\"" );
		SetNode( o, "Clothing", "[" + string.Join( ",", items ) + "]" );
		dresser.DeserializeImmediately( o );
		var task = dresser.Apply();
		st.Forced = true;
		return true;
	}
}
