// DarkRP for s&box - hits: pay a hitman job (hitman = true, the Mob boss by default) to kill someone.
// Ported from DarkRP's modules/hitmenu (MIT, Falco Peijnenburg). Request with /hit <target> <price> while looking at a
// hitman; the hitman accepts or declines; the money is held until the target dies by the hitman's hand.

public sealed class RPHit
{
	public Connection Hitman;
	public Connection Customer;
	public Connection Target;
	public long Price;
	public float Expires;
}

public sealed class RPHitsHolder
{
	public readonly List<RPHit> Hits = new List<RPHit>();
}

public static class RPHits
{
	static readonly RPHitsHolder H = new RPHitsHolder();

	public static void Reset() => H.Hits.Clear();

	static bool IsHitman( Connection c ) { var j = RP.Job( c ); return j != null && j.Hitman; }

	public static void Request( Connection customer, Connection target, long price )
	{
		var cfg = DarkRP.Config();
		var hitman = RP.AimTarget( customer, 200 );
		if ( hitman == null || !IsHitman( hitman ) ) { DarkRP.Notify( customer, 1, 4, "Look at a hitman (the Mob boss) to request a hit." ); return; }
		if ( target == null ) { DarkRP.Notify( customer, 1, 4, "Player not found." ); return; }
		if ( target == hitman ) { DarkRP.Notify( customer, 1, 4, "The hitman won't kill themselves." ); return; }
		if ( price < cfg.Int( "minHitPrice" ) || price > cfg.Int( "maxHitPrice" ) ) { DarkRP.Notify( customer, 1, 4, "The price must be between " + RPUtil.Money( cfg.Int( "minHitPrice" ) ) + " and " + RPUtil.Money( cfg.Int( "maxHitPrice" ) ) + "." ); return; }
		if ( !RP.CanAfford( customer, price ) ) { DarkRP.Notify( customer, 1, 4, "You can't afford that." ); return; }
		foreach ( var x in H.Hits ) if ( x.Hitman == hitman ) { DarkRP.Notify( customer, 1, 4, RP.Name( hitman ) + " already has a hit." ); return; }
		var h = DarkRP.RunHook( new RPHook { Name = "canRequestHit", Player = customer, Target = target, Amount = price, Data = hitman } );
		if ( !h.Allowed ) { DarkRP.Notify( customer, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't request this hit." : h.Reason ); return; }
		DarkRP.Notify( customer, 0, 4, "Hit request sent to " + RP.Name( hitman ) + "." );
		RPVotes.Question( hitman, RP.Name( customer ) + " offers " + RPUtil.Money( price ) + " to kill " + RP.Name( target ) + ". Accept?", "hit", yes =>
		{
			if ( !yes ) { DarkRP.Notify( customer, 1, 4, RP.Name( hitman ) + " declined the hit." ); return; }
			if ( !RP.CanAfford( customer, price ) ) { DarkRP.Notify( hitman, 1, 4, "The customer can't pay anymore." ); return; }
			RP.AddMoney( customer, -price );
			H.Hits.Add( new RPHit { Hitman = hitman, Customer = customer, Target = target, Price = price, Expires = Time.Now + 600 } );
			DarkRP.Notify( customer, 0, 5, RP.Name( hitman ) + " accepted the hit." );
			DarkRP.Notify( hitman, 0, 6, "Hit accepted: kill " + RP.Name( target ) + " for " + RPUtil.Money( price ) + "." );
			DarkRP.RunHook( new RPHook { Name = "onHitAccepted", Player = hitman, Target = target, Amount = price, Data = customer } );
			Sync();
		} );
	}

	/// <summary>Host: a player died - completes hits on them, cancels hits by them.</summary>
	public static void OnDeath( Connection victim, Connection attacker )
	{
		for ( int i = H.Hits.Count - 1; i >= 0; i-- )
		{
			var hit = H.Hits[i];
			if ( hit.Target == victim && attacker == hit.Hitman )
			{
				H.Hits.RemoveAt( i );
				RP.AddMoney( hit.Hitman, hit.Price );
				DarkRP.Notify( hit.Hitman, 0, 5, "Hit completed! You earned " + RPUtil.Money( hit.Price ) + "." );
				DarkRP.Notify( hit.Customer, 0, 5, "Your hit on " + RP.Name( victim ) + " was completed." );
				DarkRP.RunHook( new RPHook { Name = "onHitCompleted", Player = hit.Hitman, Target = victim, Amount = hit.Price, Data = hit.Customer } );
			}
			else if ( hit.Hitman == victim ) Fail( i, "the hitman died" );
		}
		Sync();
	}

	static void Fail( int index, string why )
	{
		var hit = H.Hits[index];
		H.Hits.RemoveAt( index );
		if ( RP.Of( hit.Customer ) != null ) { RP.AddMoney( hit.Customer, hit.Price ); DarkRP.Notify( hit.Customer, 1, 5, "Your hit failed (" + why + "). You got your money back." ); }
		if ( RP.Of( hit.Hitman ) != null ) DarkRP.Notify( hit.Hitman, 1, 5, "Your hit failed: " + why + "." );
		DarkRP.RunHook( new RPHook { Name = "onHitFailed", Player = hit.Hitman, Target = hit.Target, Reason = why } );
	}

	public static void Tick( RPWorld w )
	{
		bool changed = false;
		for ( int i = H.Hits.Count - 1; i >= 0; i-- )
		{
			var hit = H.Hits[i];
			if ( RP.Of( hit.Target ) == null ) { Fail( i, "the target left" ); changed = true; }
			else if ( RP.Of( hit.Hitman ) == null || !IsHitman( hit.Hitman ) ) { Fail( i, "the hitman is gone" ); changed = true; }
			else if ( Time.Now > hit.Expires ) { Fail( i, "time ran out" ); changed = true; }
		}
		if ( changed ) Sync();
	}

	/// <summary>RPWorld.Hits: "hitman steam id|target name|price;..." (the hitman's HUD shows theirs).</summary>
	static void Sync()
	{
		var w = RPWorld.Current();
		if ( w == null ) return;
		var parts = new List<string>();
		foreach ( var hit in H.Hits )
		{
			var hrp = RP.Of( hit.Hitman );
			if ( hrp != null ) parts.Add( hrp.SteamIdText + "|" + RP.Name( hit.Target ) + "|" + hit.Price );
		}
		w.Hits = string.Join( ";", parts );
	}
}
