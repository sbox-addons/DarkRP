// DarkRP for s&box - spawned entities: money, shipments, weapons, food, ammo, money printers, labs, microwave, tip jar,
// letters, cheques, law boards and ownable doors. Ported from DarkRP's entities/entities (MIT, Falco Peijnenburg),
// built from s&box models.

/// <summary>Every DarkRP entity has one: what it is, who owns it, its amount/price/label, health. Pressing E uses it.</summary>
[Title( "DarkRP Entity" ), Hide]
public sealed class RPEntity : Component, Component.IPressable
{
	[Sync( SyncFlags.FromHost )] public string TypeId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string OwnerId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string OwnerName { get; set; } = "";
	/// <summary>The buy command of the entity definition it was bought from (limits per player).</summary>
	[Sync( SyncFlags.FromHost )] public string DefCmd { get; set; } = "";
	/// <summary>Money amount, shipment count, food energy...</summary>
	[Sync( SyncFlags.FromHost )] public long Amount { get; set; }
	/// <summary>Price set by the owner (labs, microwave).</summary>
	[Sync( SyncFlags.FromHost )] public int Price { get; set; }
	/// <summary>Text shown when looking at it.</summary>
	[Sync( SyncFlags.FromHost )] public string Label { get; set; } = "";
	/// <summary>Type-specific data (weapon id of a shipment, letter text, cheque recipient...).</summary>
	[Sync( SyncFlags.FromHost )] public string Data { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public float Health { get; set; } = 100;

	// host only
	public GameObject LastAttacker;
	public bool Broken;
	bool _hooked;

	public bool CanPress( Component.IPressable.Event e ) => TypeId != "darkrp_door";
	public bool Pressing( Component.IPressable.Event e ) => false;

	public bool Press( Component.IPressable.Event e )
	{
		var w = RPWorld.Current();
		if ( w != null ) w.EntityAction( GameObject.Id, "use", "" );
		return true;
	}

	public void Release( Component.IPressable.Event e ) { }

	protected override void OnStart()
	{
		if ( IsProxy ) return;
		var prop = Components.Get<Prop>();
		if ( prop != null && !_hooked )
		{
			_hooked = true;
			Action<DamageInfo> handler = d => PropDamaged( d );
			prop.OnPropTakeDamage = handler;
		}
	}

	void PropDamaged( DamageInfo d )
	{
		if ( IsProxy || Broken ) return;
		LastAttacker = d.Attacker;
		RPEntities.Damage( this, d.Damage, d.Attacker );
	}
}

public static class RPEntities
{
	/// <summary>A point in front of the player, on the floor if there is one.</summary>
	public static Vector3 InFront( Connection c, float distance )
	{
		var ply = RP.Pawn( c );
		if ( ply == null ) return Vector3.Zero;
		var eye = ply.EyeTransform;
		var flat = eye.Forward.WithZ( 0 ).Normal;
		var start = eye.Position;
		var tr = ply.Scene.Trace.Ray( start, start + flat * distance ).IgnoreGameObjectHierarchy( ply.GameObject ).Radius( 6 ).Run();
		var pos = tr.Hit ? tr.EndPosition - flat * 10 : tr.EndPosition;
		var down = ply.Scene.Trace.Ray( pos, pos + Vector3.Down * 120 ).IgnoreGameObjectHierarchy( ply.GameObject ).Run();
		return down.Hit ? down.EndPosition + Vector3.Up * 4 : pos;
	}

	/// <summary>Spawns an entity type (or a prefab path) for an owner. Host only.</summary>
	public static GameObject Spawn( string typeId, Vector3 pos, Rotation rot, Connection owner )
	{
		var reg = DarkRP.Registry();
		var t = reg.EntityType( typeId );
		GameObject go;
		if ( t == null || !string.IsNullOrEmpty( t.Prefab ) )
		{
			var prefab = t != null ? t.Prefab : typeId;
			if ( string.IsNullOrEmpty( prefab ) || !prefab.EndsWith( ".prefab" ) ) { Log.Warning( "DarkRP: unknown entity type " + typeId ); return null; }
			go = GameObject.Clone( prefab, new Transform( pos, rot ), null, true, null );
			if ( go == null ) return null;
		}
		else
		{
			go = new GameObject( true, t.Name );
			go.WorldPosition = pos;
			go.WorldRotation = rot;
			if ( t.Scale > 0 && t.Scale != 1f ) go.WorldScale = t.Scale;
			if ( !string.IsNullOrEmpty( t.Model ) ) AddPhysicsModel( go, t.Model );
		}
		go.Tags.Add( "darkrp" );
		var e = go.Components.Get<RPEntity>() ?? go.AddComponent<RPEntity>();
		e.TypeId = t != null ? t.Id : typeId;
		var hp = HealthFor( e.TypeId );
		e.Health = hp > 0 ? hp : 100000;
		if ( owner != null )
		{
			var rp = RP.Of( owner );
			e.OwnerId = rp != null ? rp.SteamIdText : "";
			e.OwnerName = RP.Name( owner );
		}
		if ( t != null && !string.IsNullOrEmpty( t.Component ) && t.Component != "RPEntity" )
		{
			var td = TypeLibrary.GetType( t.Component );
			if ( td != null ) go.Components.Create( td );
			else Log.Warning( "DarkRP: entity component type not found: " + t.Component );
		}
		Ownable.Set( go, owner );
		go.NetworkSpawn();
		return go;
	}

	/// <summary>The entity's body: a Sandbox Prop (renderer, collision, physics, takes damage).</summary>
	public static void AddPhysicsModel( GameObject go, string modelPath )
	{
		var model = Model.Load( modelPath );
		var prop = go.AddComponent<Prop>();
		prop.Model = model;
		prop.Health = 100000;
		if ( model == null || model.IsError || model.PhysicsBounds.Size.Length < 0.5f )
		{
			var bc = go.AddComponent<BoxCollider>();
			var b = model != null ? model.Bounds : new BBox( new Vector3( -8, -8, 0 ), new Vector3( 8, 8, 8 ) );
			bc.Scale = b.Size;
			bc.Center = b.Center;
			if ( go.Components.Get<Rigidbody>( true ) == null ) go.AddComponent<Rigidbody>();
		}
	}

	/// <summary>Health of entities that can be destroyed (others are practically unbreakable).</summary>
	public static float HealthFor( string typeId )
	{
		if ( typeId == "money_printer" ) return 100;
		if ( typeId == "drug_lab" || typeId == "gunlab" || typeId == "microwave" ) return 150;
		if ( typeId == "spawned_shipment" ) return 300;
		return 0;
	}

	public static RPEntity Of( Guid id )
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return null;
		foreach ( var e in scene.GetAllComponents<RPEntity>() ) if ( e.GameObject.Id == id ) return e;
		return null;
	}

	public static bool IsOwner( RPEntity e, Connection c )
	{
		var rp = RP.Of( c );
		return rp != null && e.OwnerId == rp.SteamIdText;
	}

	public static GameObject SpawnMoney( Vector3 pos, long amount )
	{
		var go = Spawn( "spawned_money", pos, Rotation.FromYaw( Game.Random.Float( 0, 360 ) ), null );
		if ( go == null ) return null;
		var e = go.Components.Get<RPEntity>();
		e.Amount = amount;
		e.Label = RPUtil.Money( amount );
		var time = DarkRP.Config().Int( "moneyRemoveTime" );
		if ( time > 0 ) RPTimers.DestroyAfter( go, time );
		return go;
	}

	public static GameObject SpawnFood( RPFoodDef f, Vector3 pos, Connection owner )
	{
		var go = Spawn( "spawned_food", pos, Rotation.Identity, owner );
		if ( go == null ) return null;
		var e = go.Components.Get<RPEntity>();
		e.Amount = f.Energy;
		e.Label = f.Name;
		if ( !string.IsNullOrEmpty( f.Model ) )
		{
			var mr = go.Components.Get<ModelRenderer>();
			var m = Model.Load( f.Model );
			if ( mr != null && m != null && !m.IsError ) { mr.Model = m; var mc = go.Components.Get<ModelCollider>(); if ( mc != null ) mc.Model = m; }
		}
		return go;
	}

	/// <summary>A real weapon lying in the world (bought singly, from a shipment, a gun lab...).</summary>
	public static GameObject SpawnWeapon( string prefab, Vector3 pos, Connection owner )
	{
		GameObject go = null;
		try { go = GameObject.Clone( prefab, new Transform( pos + Vector3.Up * 10 ), null, true, null ); }
		catch ( Exception e ) { Log.Warning( "DarkRP: can't spawn " + prefab + ": " + e.Message ); }
		if ( go == null ) return null;
		go.NetworkSpawn();
		var w = go.Components.Get<BaseSandboxWeapon>( true );
		if ( w != null ) w.DropIntoWorld( pos + Vector3.Up * 10, Vector3.Up * 50, owner );
		return go;
	}

	public static void DropActiveWeapon( Connection c )
	{
		var ply = RP.Pawn( c );
		if ( ply == null ) return;
		var inv = ply.GetComponent<PlayerInventory>();
		var w = inv != null ? inv.ActiveWeapon : null;
		if ( w == null ) { DarkRP.Notify( c, 1, 3, "You're not holding a weapon." ); return; }
		inv.Drop( w );
	}

	/// <summary>Removes the law boards someone placed (they belong to the mayor's office: gone when the mayor is).</summary>
	public static void RemoveLawBoards( RPPlayer rp )
	{
		if ( rp == null ) return;
		foreach ( var go in rp.OwnedEntities.ToList() )
		{
			if ( !go.IsValid() ) { rp.OwnedEntities.Remove( go ); continue; }
			if ( go.Components.Get<RPLawBoard>() != null ) { rp.OwnedEntities.Remove( go ); go.Destroy(); }
		}
	}

	public static void RemoveOwned( Connection c, string typeId )
	{
		var rp = RP.Of( c );
		if ( rp == null ) return;
		foreach ( var go in rp.OwnedEntities.ToList() )
		{
			if ( !go.IsValid() ) { rp.OwnedEntities.Remove( go ); continue; }
			var e = go.Components.Get<RPEntity>();
			if ( e != null && e.TypeId == typeId ) { rp.OwnedEntities.Remove( go ); go.Destroy(); }
		}
	}

	static RPEntity Looked( Connection c, float range )
	{
		var go = RP.AimObject( RP.Pawn( c ), range );
		return go != null ? go.Components.Get<RPEntity>( FindMode.EverythingInSelfAndAncestors ) : null;
	}

	public static void SetPrice( Connection c, long price )
	{
		var e = Looked( c, 200 );
		if ( e == null || !IsOwner( e, c ) || !(e.TypeId == "gunlab" || e.TypeId == "drug_lab" || e.TypeId == "microwave") ) { DarkRP.Notify( c, 1, 4, "Look at your gun lab, drug lab or microwave." ); return; }
		var cfg = DarkRP.Config();
		e.Price = (int)Math.Clamp( price, cfg.Int( "pricemin" ), cfg.Int( "pricecap" ) );
		UpdateLabLabel( e );
		DarkRP.Notify( c, 0, 4, "Price set to " + RPUtil.Money( e.Price ) + "." );
	}

	public static void UpdateLabLabel( RPEntity e )
	{
		var name = e.TypeId == "gunlab" ? "Gun Lab" : e.TypeId == "drug_lab" ? "Drug Lab" : "Microwave";
		e.Label = name + "\nPrice: " + RPUtil.Money( e.Price ) + "\nPress E to buy";
	}

	public static void Letter( Connection c, string text, bool typed )
	{
		if ( !DarkRP.Config().Bool( "letters" ) ) { DarkRP.Notify( c, 1, 4, "Letters are disabled." ); return; }
		text = (text ?? "").Trim();
		if ( text.Length < 1 ) { DarkRP.Notify( c, 1, 4, "Write something." ); return; }
		if ( text.Length > 600 ) text = text.Substring( 0, 600 );
		var rp = RP.Of( c );
		var count = rp.OwnedEntities.Count( go => go.IsValid() && go.Components.Get<RPEntity>() != null && go.Components.Get<RPEntity>().TypeId == "letter" );
		if ( count >= DarkRP.Config().Int( "maxletters" ) ) { DarkRP.Notify( c, 1, 4, "You have too many letters." ); return; }
		var go2 = Spawn( "letter", InFront( c, 40 ), Rotation.Identity, c );
		if ( go2 == null ) return;
		var e = go2.Components.Get<RPEntity>();
		e.Data = (typed ? "typed" : "written") + "\u0001" + text;
		e.Label = "Letter\nPress E to read";
		rp.OwnedEntities.Add( go2 );
	}

	public static void PlaceLawBoard( Connection c )
	{
		if ( !RP.IsMayor( c ) ) { DarkRP.Notify( c, 1, 4, "Only the mayor can place law boards." ); return; }
		var rp = RP.Of( c );
		var count = rp.OwnedEntities.Count( go => go.IsValid() && go.Components.Get<RPLawBoard>() != null );
		if ( count >= DarkRP.Config().Int( "maxlawboards" ) ) { DarkRP.Notify( c, 1, 4, "You have placed too many law boards." ); return; }
		var ply = RP.Pawn( c );
		var go2 = Spawn( "darkrp_laws", InFront( c, 90 ) - Vector3.Up * 4, Rotation.FromYaw( ply.EyeTransform.Rotation.Angles().yaw + 180 ), c );
		if ( go2 == null ) return;
		var prop = go2.Components.Get<Prop>();
		if ( prop != null ) prop.IsStatic = true;
		go2.Components.Get<RPEntity>().Label = "Laws of the city";
		rp.OwnedEntities.Add( go2 );
	}

	/// <summary>Admin: an ownable door (for maps without doors).</summary>
	public static void SpawnDoor( Connection c )
	{
		var ply = RP.Pawn( c );
		if ( ply == null ) return;
		var eye = ply.EyeTransform;
		var tr = ply.Scene.Trace.Ray( eye.Position, eye.Position + eye.Forward * 300 ).IgnoreGameObjectHierarchy( ply.GameObject ).Run();
		var pos = tr.EndPosition;
		var down = ply.Scene.Trace.Ray( pos + Vector3.Up * 10, pos + Vector3.Down * 200 ).IgnoreGameObjectHierarchy( ply.GameObject ).Run();
		if ( down.Hit ) pos = down.EndPosition;
		var go = Spawn( "darkrp_door", pos, Rotation.FromYaw( eye.Rotation.Angles().yaw + 90 ), null );
		if ( go == null ) return;
		var w = RPWorld.Current();
		var door = go.Components.Get<Sandbox.Mapping.Door>();
		if ( w != null && door != null ) w.Doors[RPDoors.IdOf( door )] = new RPDoorData().Pack();
		DarkRP.Notify( c, 0, 4, "Door spawned. It can be bought with the keys (R)." );
	}

	// ---- use / damage (host) ----

	public static void Action( Connection c, Guid id, string action, string arg )
	{
		var e = Of( id );
		if ( e == null || c == null ) return;
		var ply = RP.Pawn( c );
		if ( ply == null || ply.WorldPosition.Distance( e.WorldPosition ) > 200 ) return;
		var rp = RP.Of( c );
		if ( rp == null || rp.Arrested ) return;
		if ( RP.CooldownLeft( rp, "use_" + id, 0.3f ) > 0 ) return;
		if ( action == "use" ) Use( e, c );
		else if ( action == "tip" ) Tip( e, c, (long)RPUtil.ParseFloat( arg, 0 ) );
	}

	static void Use( RPEntity e, Connection c )
	{
		var h = DarkRP.RunHook( new RPHook { Name = "canUseEntity", Player = c, Entity = e.GameObject, Text = e.TypeId } );
		if ( !h.Allowed ) { if ( !string.IsNullOrEmpty( h.Reason ) ) DarkRP.Notify( c, 1, 3, h.Reason ); return; }
		var cfg = DarkRP.Config();
		var reg = DarkRP.Registry();
		switch ( e.TypeId )
		{
			case "spawned_money":
				{
					var amount = e.Amount;
					e.Amount = 0;
					e.GameObject.Destroy();
					if ( amount <= 0 ) return;
					RP.AddMoney( c, amount );
					DarkRP.Notify( c, 0, 3, "You picked up " + RPUtil.Money( amount ) + "." );
					DarkRP.RunHook( new RPHook { Name = "playerPickedUpMoney", Player = c, Amount = amount } );
					return;
				}
			case "darkrp_cheque":
				{
					var rp = RP.Of( c );
					bool mine = rp.SteamIdText == e.Data;
					bool writer = rp.SteamIdText == e.OwnerId;
					if ( !mine && !writer ) { DarkRP.Notify( c, 1, 3, "This cheque isn't made out to you." ); return; }
					var amount = e.Amount;
					e.Amount = 0;
					e.GameObject.Destroy();
					RP.AddMoney( c, amount );
					DarkRP.Notify( c, 0, 4, (mine ? "You cashed a cheque of " : "You tore up your cheque and got back ") + RPUtil.Money( amount ) + "." );
					return;
				}
			case "spawned_shipment":
				{
					var sh = e.Components.Get<RPShipmentEntity>();
					if ( sh != null && Time.Now < sh.NextDispense ) return;
					if ( e.Amount <= 0 ) { e.GameObject.Destroy(); return; }
					var prefab = reg.WeaponPrefab( e.Data );
					var go = SpawnWeapon( prefab, e.WorldPosition + Vector3.Up * 30, c );
					if ( go == null ) { DarkRP.Notify( c, 1, 3, "This shipment is broken." ); return; }
					var rp = RP.Of( c );
					rp.GivenWeapons.Add( prefab.ToLowerInvariant() );
					e.Amount--;
					if ( sh != null ) sh.NextDispense = Time.Now + Math.Max( 0.5f, cfg.Int( "shipmentspawntime" ) );
					if ( e.Amount <= 0 ) e.GameObject.Destroy();
					return;
				}
			case "spawned_food":
				{
					RPHunger.Eat( c, (int)e.Amount );
					var ply = RP.Pawn( c );
					if ( ply != null && !cfg.Bool( "hungermod" ) ) ply.Health = Math.Min( ply.MaxHealth, ply.Health + 10 );
					DarkRP.Notify( c, 0, 3, "You ate " + e.Label + "." );
					e.GameObject.Destroy();
					return;
				}
			case "spawned_ammo":
				{
					RPShop.GiveAmmo( c, e.Data, (int)e.Amount );
					e.GameObject.Destroy();
					return;
				}
			case "letter":
				{
					var parts = e.Data.Split( '\u0001' );
					RPNet.Show( c, "letter", e.OwnerName + "\u0001" + (parts.Length > 1 ? parts[1] : e.Data) + "\u0001" + parts[0] );
					return;
				}
			case "darkrp_tip_jar":
				RPNet.Show( c, "tipjar", e.GameObject.Id.ToString() + "\u0001" + e.OwnerName + "\u0001" + e.Amount );
				return;
			case "drug":
				{
					var ply = RP.Pawn( c );
					if ( ply != null ) ply.Health = Math.Min( ply.MaxHealth * 1.5f, ply.Health + 25 );
					DarkRP.Notify( c, 0, 4, "You feel great." );
					e.GameObject.Destroy();
					DarkRP.RunHook( new RPHook { Name = "playerUsedDrugs", Player = c, Entity = e.GameObject } );
					return;
				}
			case "gunlab":
			case "drug_lab":
			case "microwave":
				LabUse( e, c );
				return;
		}
		DarkRP.RunHook( new RPHook { Name = "playerUsedEntity", Player = c, Entity = e.GameObject, Text = e.TypeId } );
	}

	static void LabUse( RPEntity e, Connection c )
	{
		var cfg = DarkRP.Config();
		var reg = DarkRP.Registry();
		var lab = e.Components.Get<RPLab>();
		if ( lab != null && Time.Now < lab.Busy ) { DarkRP.Notify( c, 1, 3, "It's busy." ); return; }
		bool owner = IsOwner( e, c );
		long price = owner ? (e.TypeId == "gunlab" ? cfg.Int( "gunlabguncost" ) / 2 : 0) : e.Price;
		if ( price > 0 && !RP.CanAfford( c, price ) ) { DarkRP.Notify( c, 1, 4, "You can't afford this (" + RPUtil.Money( price ) + ")." ); return; }
		if ( price > 0 )
		{
			RP.AddMoney( c, -price );
			var ownerConn = OwnerConn( e );
			if ( ownerConn != null && !owner ) { RP.AddMoney( ownerConn, price ); DarkRP.Notify( ownerConn, 0, 3, RP.Name( c ) + " bought from your " + e.TypeId.Replace( "_", " " ) + " for " + RPUtil.Money( price ) + "." ); }
		}
		if ( lab != null ) lab.Busy = Time.Now + 2;
		var top = e.WorldPosition + Vector3.Up * 40;
		if ( e.TypeId == "gunlab" ) SpawnWeapon( reg.WeaponPrefab( cfg.Str( "gunlabweapon" ) ), top, c );
		else if ( e.TypeId == "drug_lab" ) Spawn( "drug", top, Rotation.Identity, c ).Components.Get<RPEntity>().Label = "Drugs";
		else if ( reg.Food.Count > 0 ) SpawnFood( reg.Food[Game.Random.Int( 0, reg.Food.Count - 1 )], top, c );
	}

	static void Tip( RPEntity e, Connection c, long amount )
	{
		if ( e.TypeId != "darkrp_tip_jar" || amount <= 0 ) return;
		if ( !RP.CanAfford( c, amount ) ) { DarkRP.Notify( c, 1, 3, "You can't afford that." ); return; }
		var owner = OwnerConn( e );
		RP.AddMoney( c, -amount );
		if ( owner != null ) { RP.AddMoney( owner, amount ); DarkRP.Notify( owner, 0, 4, RP.Name( c ) + " tipped you " + RPUtil.Money( amount ) + "!" ); }
		e.Amount += amount;
		e.Label = "Tip Jar (" + e.OwnerName + ")\nTipped: " + RPUtil.Money( e.Amount ) + "\nPress E to tip";
		DarkRP.Notify( c, 0, 3, "You tipped " + RPUtil.Money( amount ) + "." );
	}

	public static Connection OwnerConn( RPEntity e )
	{
		foreach ( var rp in RP.All() ) if ( rp.SteamIdText == e.OwnerId ) return RP.ConnOf( rp );
		return null;
	}

	/// <summary>Host: damage from guns (through the Prop), the stunstick, or an overheating printer.</summary>
	public static void Damage( RPEntity e, float amount, GameObject attacker )
	{
		if ( e == null || e.Broken ) return;
		if ( e.TypeId == "darkrp_door" || e.TypeId == "darkrp_laws" ) return;
		if ( HealthFor( e.TypeId ) <= 0 ) return;
		e.Health -= amount;
		if ( e.Health > 0 ) return;
		e.Broken = true;
		var by = RP.ConnOfPawn( attacker );
		var cfg = DarkRP.Config();
		if ( e.TypeId == "money_printer" && by != null && RP.IsCP( by ) && cfg.Int( "printerreward" ) > 0 && !IsOwner( e, by ) )
		{
			RP.AddMoney( by, cfg.Int( "printerreward" ) );
			DarkRP.Notify( by, 0, 4, "You got " + RPUtil.Money( cfg.Int( "printerreward" ) ) + " for destroying a money printer." );
		}
		var owner = OwnerConn( e );
		if ( owner != null ) DarkRP.Notify( owner, 1, 4, "Your " + (string.IsNullOrEmpty( e.Label ) ? e.TypeId : e.Label.Split( (char)10 )[0]) + " has been destroyed!" );
		RPNet.Sound( "sounds/impacts/melee/impact-melee-metal.sound", e.WorldPosition );
		DarkRP.RunHook( new RPHook { Name = "entityDestroyed", Player = owner, Target = by, Entity = e.GameObject, Text = e.TypeId } );
		var prop = e.Components.Get<Prop>();
		if ( prop != null ) { try { prop.Break(); } catch { } }
		if ( e.GameObject.IsValid() ) e.GameObject.Destroy();
	}
}

/// <summary>Timed destruction without engine-held delegates (checked by the component itself).</summary>
public sealed class RPDestroyTimer : Component
{
	public float At;
	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;
		if ( Time.Now >= At ) GameObject.Destroy();
	}
}

public static class RPTimers
{
	public static void DestroyAfter( GameObject go, float seconds )
	{
		var t = go.Components.Get<RPDestroyTimer>() ?? go.AddComponent<RPDestroyTimer>();
		t.At = Time.Now + seconds;
	}
}

// ---- type components (behaviour beyond "use") ----

[Title( "DarkRP Money" ), Hide]
public sealed class RPMoneyEntity : Component { }

[Title( "DarkRP Shipment" ), Hide]
public sealed class RPShipmentEntity : Component
{
	public float NextDispense;
}

[Title( "DarkRP Food" ), Hide]
public sealed class RPFoodEntity : Component { }

[Title( "DarkRP Ammo" ), Hide]
public sealed class RPAmmoEntity : Component { }

[Title( "DarkRP Weapon" ), Hide]
public sealed class RPSpawnedWeapon : Component { }

[Title( "DarkRP Drug" ), Hide]
public sealed class RPDrug : Component { }

[Title( "DarkRP Letter" ), Hide]
public sealed class RPLetter : Component { }

[Title( "DarkRP Cheque" ), Hide]
public sealed class RPCheque : Component
{
	protected override void OnStart()
	{
		var mr = Components.Get<ModelRenderer>();
		if ( mr != null ) mr.Tint = new Color( 0.8f, 0.9f, 1f );
	}
}

[Title( "DarkRP Tip Jar" ), Hide]
public sealed class RPTipJar : Component
{
	protected override void OnStart()
	{
		if ( IsProxy ) return;
		var e = Components.Get<RPEntity>();
		if ( e != null ) e.Label = "Tip Jar (" + e.OwnerName + ")\nPress E to tip";
	}
}

/// <summary>Gun lab, drug lab and microwave: sell what they make at the owner's price.</summary>
[Title( "DarkRP Lab" ), Hide]
public sealed class RPLab : Component
{
	public float Busy;
	protected override void OnStart()
	{
		if ( IsProxy ) return;
		var e = Components.Get<RPEntity>();
		if ( e == null ) return;
		var cfg = DarkRP.Config();
		if ( e.Price <= 0 ) e.Price = e.TypeId == "gunlab" ? cfg.Int( "gunlabguncost" ) : e.TypeId == "drug_lab" ? cfg.Int( "druglabdrugcost" ) : cfg.Int( "microwavefoodcost" );
		RPEntities.UpdateLabLabel( e );
	}
}

[Title( "DarkRP Gun Lab" ), Hide]
public sealed class RPGunLab : Component
{
	protected override void OnAwake() { if ( Components.Get<RPLab>() == null ) Components.Create<RPLab>(); }
}

[Title( "DarkRP Drug Lab" ), Hide]
public sealed class RPDrugLab : Component
{
	protected override void OnAwake() { if ( Components.Get<RPLab>() == null ) Components.Create<RPLab>(); }
}

[Title( "DarkRP Microwave" ), Hide]
public sealed class RPMicrowave : Component
{
	protected override void OnAwake() { if ( Components.Get<RPLab>() == null ) Components.Create<RPLab>(); }
}

/// <summary>Prints money every so often; may overheat and burn.</summary>
[Title( "DarkRP Money Printer" ), Hide]
public sealed class RPMoneyPrinter : Component
{
	[Sync( SyncFlags.FromHost )] public bool Overheating { get; set; }
	float _next;
	float _burnAt;

	protected override void OnStart()
	{
		if ( IsProxy ) return;
		Schedule();
		var e = Components.Get<RPEntity>();
		if ( e != null ) { e.Label = "Money Printer\n" + e.OwnerName; e.Health = 100; }
	}

	void Schedule()
	{
		var d = Math.Max( 10, DarkRP.Config().Int( "printerdelay" ) );
		_next = Time.Now + d * Game.Random.Float( 0.6f, 1.4f );
	}

	protected override void OnUpdate()
	{
		var mr = Components.Get<ModelRenderer>();
		if ( mr != null ) mr.Tint = Overheating ? Color.Lerp( new Color( 1f, 0.35f, 0.2f ), Color.White, MathF.Abs( MathF.Sin( Time.Now * 6 ) ) ) : new Color( 0.75f, 1f, 0.8f );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;
		var cfg = DarkRP.Config();
		if ( cfg == null ) return;
		if ( Overheating )
		{
			if ( Time.Now >= _burnAt )
			{
				var e = Components.Get<RPEntity>();
				if ( e != null ) RPEntities.Damage( e, 10000, null );
				else GameObject.Destroy();
			}
			return;
		}
		if ( Time.Now < _next ) return;
		Schedule();
		var chance = cfg.Int( "printeroverheatchance" );
		if ( cfg.Bool( "printeroverheat" ) && chance > 0 && Game.Random.Int( 1, chance ) == 1 )
		{
			Overheating = true;
			_burnAt = Time.Now + 15;
			var e = Components.Get<RPEntity>();
			var owner = e != null ? RPEntities.OwnerConn( e ) : null;
			if ( owner != null ) DarkRP.Notify( owner, 1, 6, "Your money printer is overheating!" );
			return;
		}
		var amount = cfg.Int( "mprintamount" );
		var h = DarkRP.RunHook( new RPHook { Name = "moneyPrinterPrintMoney", Entity = GameObject, Amount = amount } );
		if ( !h.Allowed ) return;
		RPEntities.SpawnMoney( WorldPosition + WorldRotation.Up * 30, h.Amount );
	}
}

/// <summary>Shows the city's laws on the mayor's board (models/darkrp/lawboard: the face is the +X side, 50 x 40 units,
/// 30 above the floor). TextRenderers, since world panels aren't available to scripts in games.</summary>
[Title( "DarkRP Law Board" ), Hide]
public sealed class RPLawBoard : Component
{
	TextRenderer _title, _body;
	string _shown;

	TextRenderer Line( string name, Vector3 pos, float scale, TextRenderer.HAlignment h )
	{
		var go = new GameObject( true, name );
		go.Flags = GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
		go.SetParent( GameObject, false );
		go.LocalPosition = pos;
		go.LocalRotation = Rotation.FromYaw( 180 );
		var t = go.AddComponent<TextRenderer>();
		t.Scale = scale;
		t.HorizontalAlignment = h;
		t.VerticalAlignment = TextRenderer.VAlignment.Top;
		t.Color = new Color( 0.93f, 0.93f, 0.88f );
		return t;
	}

	/// <summary>Wraps a law to the board's width.</summary>
	public static string Wrap( string text, int width )
	{
		var sb = new System.Text.StringBuilder();
		int col = 0;
		foreach ( var word in text.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
		{
			if ( col > 0 && col + 1 + word.Length > width ) { sb.Append( (char)10 ); sb.Append( "    " ); col = 4; }
			else if ( col > 0 ) { sb.Append( ' ' ); col++; }
			sb.Append( word );
			col += word.Length;
		}
		return sb.ToString();
	}

	protected override void OnUpdate()
	{
		var w = RPWorld.Current();
		if ( w == null ) return;
		if ( _title == null )
		{
			_title = Line( "laws_title", new Vector3( 0.8f, 0, 68.5f ), 0.09f, TextRenderer.HAlignment.Center );
			_title.Text = "LAWS OF THE CITY";
			_body = Line( "laws_text", new Vector3( 0.8f, 0, 62.5f ), 0.05f, TextRenderer.HAlignment.Center );
		}
		var key = string.Join( (char)10, w.Laws );
		if ( key == _shown ) return;
		_shown = key;
		// 12 lines of 60 characters fit at full size; longer lists get smaller text (and longer lines) to stay on the board.
		string s = "There are no laws.";
		float size = 1f;
		for ( ; w.Laws.Count > 0 && size >= 0.35f; size -= 0.05f )
		{
			var sb = new System.Text.StringBuilder();
			for ( int i = 0; i < w.Laws.Count; i++ )
			{
				if ( i > 0 ) sb.Append( (char)10 );
				sb.Append( Wrap( (i + 1) + ". " + w.Laws[i], (int)(60 / size) ) );
			}
			s = sb.ToString();
			if ( s.Split( (char)10 ).Length * size <= 12f ) break;
		}
		_body.Text = s;
		_body.Scale = 0.05f * Math.Max( size, 0.35f );
	}
}

/// <summary>An ownable door spawned by an admin (/spawndoor): the s&box door model with a Door component.</summary>
[Title( "DarkRP Door" ), Hide]
public sealed class RPDoorEntity : Component
{
	protected override void OnAwake()
	{
		var prop = Components.Get<Prop>();
		if ( prop != null ) prop.IsStatic = true;
		if ( Components.Get<Sandbox.Mapping.Door>() == null ) Components.Create<Sandbox.Mapping.Door>();
	}
}
