// DarkRP for s&box - police: wanted, warrants, arrests and jail. Ported from DarkRP's modules/police (MIT, Falco Peijnenburg).

public static class RPPolice
{
	public static string ArrestBlocker( Connection cop, Connection target )
	{
		var cfg = DarkRP.Config();
		var t = RP.Of( target );
		if ( t == null ) return "Invalid target.";
		if ( t.Arrested ) return RP.Name( target ) + " is already arrested.";
		if ( cop != null && !RP.IsCP( cop ) && !RP.IsAdmin( cop ) ) return "You're not the police.";
		if ( cop != null && RP.IsCP( target ) && !cfg.Bool( "cpcanarrestcp" ) ) return "You can't arrest other police.";
		if ( cfg.Bool( "needwantedforarrest" ) && !t.Wanted ) return RP.Name( target ) + " isn't wanted.";
		var h = DarkRP.RunHook( new RPHook { Name = "canArrest", Player = cop, Target = target } );
		if ( !h.Allowed ) return string.IsNullOrEmpty( h.Reason ) ? "You can't arrest this player." : h.Reason;
		return null;
	}

	/// <summary>Jails the player for <paramref name="seconds"/> (0 = jailtimer).</summary>
	public static void Arrest( Connection target, float seconds, Connection cop )
	{
		var t = RP.Of( target );
		if ( t == null ) return;
		var cfg = DarkRP.Config();
		if ( seconds <= 0 ) seconds = cfg.Int( "jailtimer" );
		t.Arrested = true;
		t.ArrestUntil = Time.Now + seconds;
		t.ArrestLeft = (int)Math.Ceiling( seconds );
		SetUnwanted( target, null, true );
		t.Warranted = false;
		StripForJail( target );
		if ( cfg.Bool( "teletojail" ) )
		{
			var jail = RPMap.RandomJail();
			if ( jail.HasValue ) Teleport( target, jail.Value );
			else if ( cop != null ) DarkRP.Notify( cop, 1, 6, "There is no jail position: the prisoner stays here. Set one with /jailpos." );
		}
		DarkRP.NotifyAll( 0, 6, RP.Name( target ) + " has been arrested for " + (int)seconds + " seconds" + (cop != null ? " by " + RP.Name( cop ) : "") + "!" );
		RPLog.Write( RP.Name( target ) + " was arrested by " + (cop != null ? RP.Name( cop ) : "the server") );
		DarkRP.RunHook( new RPHook { Name = "playerArrested", Player = target, Target = cop, Amount = (long)seconds } );
	}

	public static void Unarrest( Connection target, Connection actor )
	{
		var t = RP.Of( target );
		if ( t == null || !t.Arrested ) return;
		t.Arrested = false;
		t.ArrestLeft = 0;
		if ( RP.Alive( target ) )
		{
			if ( DarkRP.Config().Bool( "telefromjail" ) ) RPJobs.Respawn( target );
			else RPJobs.GiveLoadout( target, true );
		}
		DarkRP.NotifyAll( 0, 4, RP.Name( target ) + " has been released from jail" + (actor != null ? " by " + RP.Name( actor ) : "") + "." );
		DarkRP.RunHook( new RPHook { Name = "playerUnArrested", Player = target, Target = actor } );
	}

	public static void StripForJail( Connection c )
	{
		var ply = RP.Pawn( c );
		if ( ply == null ) return;
		var inv = ply.GetComponent<PlayerInventory>();
		if ( inv == null ) return;
		foreach ( var w in inv.Weapons.ToList() ) inv.Remove( w );
	}

	/// <summary>Moves a player (their own machine moves them: players are simulated by their owner).</summary>
	public static void Teleport( Connection c, Vector3 pos )
	{
		var w = RPWorld.Current();
		if ( w == null ) return;
		using ( Rpc.FilterInclude( c ) ) w.TeleportRpc( pos + Vector3.Up * 8 );
	}

	public static bool SetWanted( Connection target, Connection actor, string reason )
	{
		var t = RP.Of( target );
		if ( t == null ) return false;
		if ( string.IsNullOrWhiteSpace( reason ) ) reason = "No reason";
		var h = DarkRP.RunHook( new RPHook { Name = "canWanted", Player = actor, Target = target, Reason = reason } );
		if ( !h.Allowed ) { if ( actor != null ) DarkRP.Notify( actor, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't do that." : h.Reason ); return false; }
		t.Wanted = true;
		t.WantedReason = reason;
		t.WantedBy = actor != null ? RP.Name( actor ) : "";
		t.WantedUntil = Time.Now + DarkRP.Config().Int( "wantedtime" );
		DarkRP.NotifyAll( 0, 8, RP.Name( target ) + " is wanted by the Police! Reason: " + reason + (actor != null ? " (ordered by " + RP.Name( actor ) + ")" : "") );
		RPLog.Write( RP.Name( target ) + " was made wanted by " + (actor != null ? RP.Name( actor ) : "?") + ": " + reason );
		DarkRP.RunHook( new RPHook { Name = "playerWanted", Player = target, Target = actor, Reason = reason } );
		return true;
	}

	public static void SetUnwanted( Connection target, Connection actor, bool silent )
	{
		var t = RP.Of( target );
		if ( t == null || !t.Wanted ) return;
		t.Wanted = false;
		t.WantedReason = "";
		if ( !silent ) DarkRP.NotifyAll( 0, 4, RP.Name( target ) + " is no longer wanted by the Police." );
		DarkRP.RunHook( new RPHook { Name = "playerUnWanted", Player = target, Target = actor } );
	}

	/// <summary>A search warrant (the battering ram can open the target's doors). The mayor approves it if there is one.</summary>
	public static void RequestWarrant( Connection cop, Connection target, string reason )
	{
		if ( string.IsNullOrWhiteSpace( reason ) ) { DarkRP.Notify( cop, 1, 4, "You need a reason for the warrant." ); return; }
		Connection mayor = null;
		foreach ( var rp in RP.All() ) { var j = RP.JobOf( rp ); if ( j != null && j.Mayor ) mayor = RP.ConnOf( rp ); }
		if ( mayor == null || mayor == cop )
		{
			SetWarrant( target, cop, reason );
			return;
		}
		DarkRP.Notify( cop, 0, 4, "The mayor has to approve the warrant." );
		RPVotes.Question( mayor, RP.Name( cop ) + " wants a search warrant for " + RP.Name( target ) + ". Reason: " + reason, "warrant", yes =>
		{
			if ( yes ) SetWarrant( target, cop, reason );
			else DarkRP.Notify( cop, 1, 4, "The mayor denied the warrant." );
		} );
	}

	public static void SetWarrant( Connection target, Connection actor, string reason )
	{
		var t = RP.Of( target );
		if ( t == null ) return;
		t.Warranted = true;
		t.WarrantReason = reason ?? "";
		t.WarrantUntil = Time.Now + DarkRP.Config().Int( "searchtime" );
		DarkRP.NotifyAll( 0, 6, "Search warrant approved for " + RP.Name( target ) + "! Reason: " + reason );
		DarkRP.RunHook( new RPHook { Name = "playerWarranted", Player = target, Target = actor, Reason = reason ?? "" } );
	}

	public static void RemoveWarrant( Connection target, Connection actor )
	{
		var t = RP.Of( target );
		if ( t == null || !t.Warranted ) return;
		t.Warranted = false;
		DarkRP.NotifyAll( 0, 4, "The search warrant for " + RP.Name( target ) + " has expired." );
		DarkRP.RunHook( new RPHook { Name = "playerUnWarranted", Player = target, Target = actor } );
	}

	public static void Tick( RPWorld w )
	{
		var now = Time.Now;
		foreach ( var rp in RP.All() )
		{
			var c = RP.ConnOf( rp );
			if ( rp.Wanted && now >= rp.WantedUntil ) SetUnwanted( c, null, false );
			if ( rp.Warranted && now >= rp.WarrantUntil ) RemoveWarrant( c, null );
			if ( rp.Arrested )
			{
				var left = (int)Math.Ceiling( rp.ArrestUntil - now );
				if ( left <= 0 ) Unarrest( c, null );
				else rp.ArrestLeft = left;
			}
		}
	}

	/// <summary>Arrested players can't hurt anyone.</summary>
	public static void OnDamaging( Component.IDamageable.DamageEvent e )
	{
		var attacker = RP.ConnOfPawn( e.Instigator );
		if ( attacker == null ) return;
		var rp = RP.Of( attacker );
		if ( rp != null && rp.Arrested && e.Victim != e.Instigator ) e.Cancelled = true;
	}
}
