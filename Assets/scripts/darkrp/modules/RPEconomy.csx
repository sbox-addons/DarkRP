// DarkRP for s&box - money (salaries, giving, dropping, cheques), the shop (shipments, weapons, entities, ammo, food)
// and the hunger module. Ported from DarkRP's modules/money, base/sv_purchasing.lua and hungermod (MIT, Falco Peijnenburg).

public static class RPMoney
{
	public static void Tick( RPWorld w )
	{
		var cfg = DarkRP.Config();
		var left = (int)Math.Ceiling( w.NextPayday - Time.Now );
		if ( left <= 0 )
		{
			Payday( w );
			w.NextPayday = Time.Now + Math.Max( 10, cfg.Int( "paydelay" ) );
			left = cfg.Int( "paydelay" );
		}
		w.PaydayIn = left;

		if ( cfg.Bool( "wallettax" ) && Time.Now >= w.NextWalletTax )
		{
			w.NextWalletTax = Time.Now + Math.Max( 30, cfg.Int( "wallettaxtime" ) );
			foreach ( var rp in RP.All() )
			{
				var pct = Game.Random.Float( cfg.Float( "wallettaxmin" ), cfg.Float( "wallettaxmax" ) );
				var tax = (long)(rp.Money * pct / 100f);
				if ( tax <= 0 ) continue;
				var c = RP.ConnOf( rp );
				RP.AddMoney( c, -tax );
				DarkRP.Notify( c, 3, 5, "Tax day! " + RPUtil.Money( tax ) + " (" + RPUtil.FloatStr( (float)Math.Round( pct, 1 ) ) + "%) was taken from your wallet." );
			}
		}
	}

	public static void Payday( RPWorld w )
	{
		var cfg = DarkRP.Config();
		foreach ( var rp in RP.All() )
		{
			var c = RP.ConnOf( rp );
			if ( c == null ) continue;
			var job = RP.JobOf( rp );
			long amount = rp.Salary;
			string msg = "";
			if ( rp.Arrested ) { amount = 0; msg = "You received no salary because you are arrested!"; }
			else if ( !RP.Alive( c ) ) { amount = 0; msg = "You received no salary because you are dead!"; }
			var h = DarkRP.RunHook( new RPHook { Name = "playerGetSalary", Player = c, Job = job, Amount = amount, Text = msg } );
			if ( !h.Allowed ) continue;
			amount = h.Amount;
			msg = h.Text;
			if ( cfg.Bool( "propertytax" ) )
			{
				var doors = RPDoors.CountOwned( w, rp );
				if ( doors > 0 )
				{
					var tax = doors * Math.Max( 1, cfg.Int( "doorcost" ) / 10 );
					amount -= tax;
					if ( string.IsNullOrEmpty( msg ) ) msg = "Property tax: " + RPUtil.Money( tax ) + ".";
				}
			}
			if ( amount > 0 )
			{
				RP.AddMoney( c, amount );
				DarkRP.Notify( c, 4, 4, string.IsNullOrEmpty( msg ) ? "Payday! You received " + RPUtil.Money( amount ) + "!" : msg );
			}
			else
			{
				if ( amount < 0 ) RP.AddMoney( c, amount );
				DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( msg ) ? "You received no salary." : msg );
			}
		}
	}

	public static void OnKill( PlayerKillEvent e )
	{
		if ( e.Player == null || !e.Victim.IsValid() ) return;
		if ( e.Victim.Components.Get<Player>( FindMode.EverythingInSelfAndAncestors ) != null ) return;
		var pay = DarkRP.Config().Int( "npckillpay" );
		if ( pay <= 0 ) return;
		var c = e.Player.Network.Owner;
		RP.AddMoney( c, pay );
		DarkRP.Notify( c, 0, 3, "You got " + RPUtil.Money( pay ) + " for killing an NPC." );
	}

	/// <summary>/give: money to the player you look at.</summary>
	public static void Give( Connection c, long amount )
	{
		if ( amount <= 0 ) { DarkRP.Notify( c, 1, 4, "Invalid amount." ); return; }
		var target = RP.AimTarget( c, 150 );
		if ( target == null ) { DarkRP.Notify( c, 1, 4, "You must look at a player (close by)." ); return; }
		if ( !RP.CanAfford( c, amount ) ) { DarkRP.Notify( c, 1, 4, "You can't afford that." ); return; }
		var h = DarkRP.RunHook( new RPHook { Name = "canGiveMoney", Player = c, Target = target, Amount = amount } );
		if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't give money now." : h.Reason ); return; }
		RP.Pay( c, target, amount );
		DarkRP.Notify( target, 0, 4, RP.Name( c ) + " has given you " + RPUtil.Money( amount ) + "." );
		DarkRP.Notify( c, 0, 4, "You gave " + RP.Name( target ) + " " + RPUtil.Money( amount ) + "." );
		RPLog.Write( RP.Name( c ) + " gave " + RPUtil.Money( amount ) + " to " + RP.Name( target ) );
	}

	/// <summary>/dropmoney: a money pile in front of the player.</summary>
	public static void Drop( Connection c, long amount )
	{
		if ( amount <= 1 ) { DarkRP.Notify( c, 1, 4, "Invalid amount." ); return; }
		if ( !RP.CanAfford( c, amount ) ) { DarkRP.Notify( c, 1, 4, "You can't afford that." ); return; }
		var rp = RP.Of( c );
		var piles = rp.OwnedEntities.Count( go => go.IsValid() && go.Components.Get<RPMoneyEntity>() != null );
		if ( piles >= DarkRP.Config().Int( "maxMoneyPiles" ) ) { DarkRP.Notify( c, 1, 4, "You have dropped too much money already." ); return; }
		var h = DarkRP.RunHook( new RPHook { Name = "canDropMoney", Player = c, Amount = amount } );
		if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't drop money now." : h.Reason ); return; }
		RP.AddMoney( c, -amount );
		var go2 = RPEntities.SpawnMoney( RPEntities.InFront( c, 40 ), amount );
		if ( go2 != null ) rp.OwnedEntities.Add( go2 );
		RPLog.Write( RP.Name( c ) + " dropped " + RPUtil.Money( amount ) );
	}

	/// <summary>/cheque: a cheque only the named player can cash.</summary>
	public static void Cheque( Connection c, Connection to, long amount )
	{
		if ( to == null ) { DarkRP.Notify( c, 1, 4, "Player not found." ); return; }
		if ( amount <= 1 ) { DarkRP.Notify( c, 1, 4, "Invalid amount." ); return; }
		if ( !RP.CanAfford( c, amount ) ) { DarkRP.Notify( c, 1, 4, "You can't afford that." ); return; }
		var rp = RP.Of( c );
		var count = rp.OwnedEntities.Count( go => go.IsValid() && go.Components.Get<RPCheque>() != null );
		if ( count >= DarkRP.Config().Int( "maxCheques" ) ) { DarkRP.Notify( c, 1, 4, "You have written too many cheques." ); return; }
		RP.AddMoney( c, -amount );
		var go2 = RPEntities.Spawn( "darkrp_cheque", RPEntities.InFront( c, 40 ), Rotation.Identity, c );
		if ( go2 == null ) return;
		var e = go2.Components.Get<RPEntity>();
		e.Amount = amount;
		e.Data = RP.Of( to ).SteamIdText;
		e.Label = "Cheque: " + RPUtil.Money( amount ) + "\nPay to: " + RP.Name( to ) + "\nFrom: " + RP.Name( c );
		DarkRP.Notify( to, 0, 5, RP.Name( c ) + " wrote you a cheque for " + RPUtil.Money( amount ) + "." );
	}
}

public static class RPShop
{
	/// <summary>DarkRP ammo type → s&box ammo resource.</summary>
	public static string AmmoPath( string type )
	{
		if ( string.IsNullOrEmpty( type ) ) return "ammo/9mm.ammo";
		var t = type.ToLowerInvariant();
		if ( t.EndsWith( ".ammo" ) ) return t;
		if ( t == "pistol" || t == "9mm" || t == "357" ) return "ammo/9mm.ammo";
		if ( t == "smg1" || t == "ar2" || t == "rifle" ) return "ammo/rifle.ammo";
		if ( t == "buckshot" || t == "shotgun" ) return "ammo/shotgun.ammo";
		if ( t == "sniper" || t == "sniperround" || t == "xbowbolt" ) return "ammo/sniper.ammo";
		if ( t == "rpg_round" || t == "rocket" ) return "ammo/rocket.ammo";
		if ( t == "grenade" ) return "ammo/grenade.ammo";
		return "ammo/9mm.ammo";
	}

	/// <summary>
	/// The ammo resource for a DarkRP ammo type. Scripts can't use ResourceLibrary, so it's read from a weapon prefab
	/// that uses that ammo.
	/// </summary>
	public static BaseAmmoResource AmmoResource( string type )
	{
		var path = AmmoPath( type );
		string prefab = "darkrp/weapons/weapon_glock2.prefab";
		if ( path == "ammo/rifle.ammo" ) prefab = "darkrp/weapons/weapon_ak472.prefab";
		else if ( path == "ammo/shotgun.ammo" ) prefab = "darkrp/weapons/weapon_pumpshotgun2.prefab";
		else if ( path == "ammo/sniper.ammo" ) prefab = "weapons/sniper/sniper.prefab";
		else if ( path == "ammo/rocket.ammo" ) prefab = "weapons/rpg/rpg.prefab";
		else if ( path == "ammo/grenade.ammo" ) prefab = "weapons/grenade/grenade.prefab";
		var go = GameObject.GetPrefab( prefab );
		var w = go != null ? go.GetComponent<BaseCombatWeapon>( true ) : null;
		return w != null ? w.PrimaryAmmoType : null;
	}

	public static void GiveAmmo( Connection c, string type, int amount )
	{
		var ply = RP.Pawn( c );
		if ( ply == null || amount <= 0 ) return;
		var inv = ply.GetComponent<PlayerInventory>();
		var res = AmmoResource( type );
		if ( inv == null || res == null ) { Log.Warning( "DarkRP: unknown ammo " + type ); return; }
		inv.SetAmmo( res, inv.GetAmmo( res ) + amount );
	}

	static bool JobAllowed( Connection c, string[] allowed )
	{
		if ( allowed == null || allowed.Length == 0 ) return true;
		var rp = RP.Of( c );
		return rp != null && RPUtil.ContainsIgnoreCase( allowed, rp.JobCommand );
	}

	static bool Check( Connection c, Func<Connection, bool> custom, string failMsg )
	{
		if ( custom == null ) return true;
		bool ok;
		try { ok = custom( c ); } catch ( Exception e ) { Log.Warning( "DarkRP customCheck failed: " + e.Message ); ok = false; }
		if ( !ok ) DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( failMsg ) ? "You can't buy this." : failMsg );
		return ok;
	}

	static bool Pay( Connection c, long price, string what )
	{
		if ( price <= 0 ) return true;
		if ( !RP.CanAfford( c, price ) ) { DarkRP.Notify( c, 1, 4, "You can't afford " + what + "! (" + RPUtil.Money( price ) + ")" ); return false; }
		RP.AddMoney( c, -price );
		return true;
	}

	static bool Alive( Connection c )
	{
		if ( RP.Alive( c ) && !RP.Of( c ).Arrested ) return true;
		DarkRP.Notify( c, 1, 4, "You can't buy things right now." );
		return false;
	}

	public static void Buy( Connection c, string kind, string id, int count )
	{
		var reg = DarkRP.Registry();
		if ( c == null || reg == null || !Alive( c ) ) return;
		var rp = RP.Of( c );
		if ( RP.CooldownLeft( rp, "buy", 0.5f ) > 0 ) return;

		if ( kind == "shipment" || kind == "weapon" )
		{
			var s = reg.Shipment( id );
			if ( s == null ) { DarkRP.Notify( c, 1, 4, "That doesn't exist." ); return; }
			bool single = kind == "weapon";
			if ( single && !(s.Separate || s.NoShip) ) { DarkRP.Notify( c, 1, 4, "This weapon isn't sold separately." ); return; }
			if ( !single && s.NoShip ) { DarkRP.Notify( c, 1, 4, "This is only sold separately." ); return; }
			if ( !JobAllowed( c, s.Allowed ) ) { DarkRP.Notify( c, 1, 4, "Your job can't buy " + s.Name + "." ); return; }
			if ( !Check( c, s.CustomCheck, s.CustomCheckFailMsg ) ) return;
			var h = DarkRP.RunHook( new RPHook { Name = single ? "canBuyPistol" : "canBuyShipment", Player = c, Shipment = s } );
			if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't buy this." : h.Reason ); return; }
			var price = single ? s.PriceSep : s.Price;
			if ( h.Amount > 0 ) price = (int)h.Amount;
			if ( !Pay( c, price, s.Name ) ) return;
			var prefab = reg.WeaponPrefab( s.Entity );
			if ( single )
			{
				var wgo = RPEntities.SpawnWeapon( prefab, RPEntities.InFront( c, 50 ), c );
				if ( wgo == null ) { RP.AddMoney( c, price ); DarkRP.Notify( c, 1, 4, "Couldn't spawn the weapon." ); return; }
				rp.GivenWeapons.Add( prefab.ToLowerInvariant() );
			}
			else
			{
				var go = RPEntities.Spawn( "spawned_shipment", RPEntities.InFront( c, 60 ), Rotation.Identity, c );
				if ( go == null ) { RP.AddMoney( c, price ); return; }
				var e = go.Components.Get<RPEntity>();
				e.Amount = s.Amount;
				e.Data = s.Entity;
				e.Label = s.Name;
				rp.OwnedEntities.Add( go );
			}
			DarkRP.Notify( c, 0, 4, "You bought " + (single ? "a " : "a shipment of ") + s.Name + " for " + RPUtil.Money( price ) + "." );
			DarkRP.RunHook( new RPHook { Name = single ? "playerBoughtPistol" : "playerBoughtShipment", Player = c, Shipment = s, Amount = price } );
			RPLog.Write( RP.Name( c ) + " bought " + (single ? "" : "a shipment of ") + s.Name + " for " + RPUtil.Money( price ) );
			return;
		}

		if ( kind == "entity" )
		{
			var d = reg.EntityDef( id );
			if ( d == null ) { DarkRP.Notify( c, 1, 4, "That doesn't exist." ); return; }
			if ( !JobAllowed( c, d.Allowed ) ) { DarkRP.Notify( c, 1, 4, "Your job can't buy " + d.Name + "." ); return; }
			if ( (d.Ent == "microwave") && !DarkRP.Config().Bool( "hungermod" ) ) { DarkRP.Notify( c, 1, 4, "The hunger module is off." ); return; }
			if ( !Check( c, d.CustomCheck, d.CustomCheckFailMsg ) ) return;
			var owned = rp.OwnedEntities.Count( go => go.IsValid() && go.Components.Get<RPEntity>() != null && go.Components.Get<RPEntity>().DefCmd == d.Cmd );
			bool adminBypass = RP.IsAdmin( c ) && DarkRP.Config().Bool( "adminsCanBypassMax" );
			if ( d.Max > 0 && owned >= d.Max && !adminBypass ) { DarkRP.Notify( c, 1, 4, "You have reached the limit of " + d.Max + " " + d.Name + "(s)." ); return; }
			var h = DarkRP.RunHook( new RPHook { Name = "canBuyCustomEntity", Player = c, EntityDef = d } );
			if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't buy this." : h.Reason ); return; }
			if ( !Pay( c, d.Price, d.Name ) ) return;
			var go3 = RPEntities.Spawn( d.Ent, RPEntities.InFront( c, 60 ), Rotation.FromYaw( RP.Pawn( c ).EyeTransform.Rotation.Angles().yaw + 180 ), c );
			if ( go3 == null ) { RP.AddMoney( c, d.Price ); DarkRP.Notify( c, 1, 4, "Couldn't spawn " + d.Name + "." ); return; }
			var ent = go3.Components.Get<RPEntity>();
			if ( ent != null ) { ent.DefCmd = d.Cmd; if ( string.IsNullOrEmpty( ent.Label ) ) ent.Label = d.Name; }
			rp.OwnedEntities.Add( go3 );
			DarkRP.Notify( c, 0, 4, "You bought a " + d.Name + " for " + RPUtil.Money( d.Price ) + "." );
			DarkRP.RunHook( new RPHook { Name = "playerBoughtCustomEntity", Player = c, EntityDef = d, Entity = go3, Amount = d.Price } );
			RPLog.Write( RP.Name( c ) + " bought a " + d.Name + " for " + RPUtil.Money( d.Price ) );
			return;
		}

		if ( kind == "ammo" )
		{
			RPAmmoDef a = null;
			foreach ( var x in reg.Ammo ) if ( string.Equals( x.Name, id, StringComparison.OrdinalIgnoreCase ) || string.Equals( x.AmmoType, id, StringComparison.OrdinalIgnoreCase ) ) { a = x; break; }
			if ( a == null ) { DarkRP.Notify( c, 1, 4, "That ammo doesn't exist." ); return; }
			if ( !JobAllowed( c, a.Allowed ) ) { DarkRP.Notify( c, 1, 4, "Your job can't buy this ammo." ); return; }
			if ( a.CustomCheck != null && !Check( c, a.CustomCheck, "" ) ) return;
			var h = DarkRP.RunHook( new RPHook { Name = "canBuyAmmo", Player = c, Text = a.AmmoType } );
			if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't buy ammo now." : h.Reason ); return; }
			if ( !Pay( c, a.Price, a.Name ) ) return;
			GiveAmmo( c, a.AmmoType, a.Amount );
			DarkRP.Notify( c, 0, 4, "You bought " + a.Amount + " " + a.Name + " for " + RPUtil.Money( a.Price ) + "." );
			return;
		}

		if ( kind == "food" )
		{
			if ( !DarkRP.Config().Bool( "hungermod" ) ) { DarkRP.Notify( c, 1, 4, "The hunger module is off." ); return; }
			RPFoodDef f = null;
			foreach ( var x in reg.Food ) if ( string.Equals( x.Name, id, StringComparison.OrdinalIgnoreCase ) ) { f = x; break; }
			if ( f == null ) { DarkRP.Notify( c, 1, 4, "That food doesn't exist." ); return; }
			var job = RP.JobOf( rp );
			bool cookOnline = RP.All().Any( p => { var j = RP.JobOf( p ); return j != null && j.Cook; } );
			if ( f.RequiresCook && cookOnline && (job == null || !job.Cook) ) { DarkRP.Notify( c, 1, 4, "Only cooks can sell food while there is a cook." ); return; }
			var h = DarkRP.RunHook( new RPHook { Name = "canBuyFood", Player = c, Text = f.Name } );
			if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't buy food now." : h.Reason ); return; }
			if ( !Pay( c, f.Price, f.Name ) ) return;
			var go4 = RPEntities.SpawnFood( f, RPEntities.InFront( c, 40 ), c );
			if ( go4 != null ) rp.OwnedEntities.Add( go4 );
			DarkRP.Notify( c, 0, 4, "You bought " + f.Name + " for " + RPUtil.Money( f.Price ) + "." );
			return;
		}
		DarkRP.Notify( c, 1, 4, "Unknown item." );
	}
}

public static class RPHunger
{
	public static void Tick( RPWorld w )
	{
		var cfg = DarkRP.Config();
		if ( !cfg.Bool( "hungermod" ) ) return;
		var loss = cfg.Float( "hungerspeed" ) / 10f;
		foreach ( var rp in RP.All() )
		{
			var c = RP.ConnOf( rp );
			if ( !RP.Alive( c ) || rp.Arrested ) continue;
			var h = DarkRP.RunHook( new RPHook { Name = "hungerUpdate", Player = c, Amount = (long)(rp.Energy * 100) } );
			if ( !h.Allowed ) continue;
			rp.Energy = Math.Max( 0, rp.Energy - loss );
			if ( rp.Energy <= 0 )
			{
				var ply = RP.Pawn( c );
				if ( ply == null ) continue;
				var dmg = cfg.Int( "starverate" );
				ply.Health -= dmg;
				if ( ply.Health <= 0 )
				{
					ply.Health = 1;
					ply.OnDamage( new DamageInfo( 10, ply.GameObject, null ) );
					DarkRP.NotifyAll( 0, 4, RP.Name( c ) + " has starved to death." );
				}
			}
		}
	}

	public static void Eat( Connection c, int energy )
	{
		var rp = RP.Of( c );
		if ( rp == null ) return;
		rp.Energy = Math.Clamp( rp.Energy + energy, 0, 100 );
	}
}
