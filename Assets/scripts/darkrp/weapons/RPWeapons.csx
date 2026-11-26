// DarkRP for s&box - DarkRP's weapons: arrest/unarrest baton, stunstick, battering ram, keys, lockpick, pocket, med kit,
// weapon checker and keypad checker. Ported from DarkRP's entities/weapons (MIT, Falco Peijnenburg). They are Sandbox
// melee weapons (prefabs in Assets/darkrp/weapons built on Sandbox's crowbar and fists). The owner's machine traces
// and asks the host (RPWorld.WeaponUse), which checks range and rules and does the work (RPWeaponLogic).
// DarkRP's guns are prefab variants of Sandbox's guns with DarkRP's numbers and need no code.

public static class RPWeaponFx
{
	/// <summary>The player holding the weapon (BaseSandboxWeapon.Owner hides the engine's Owner, which scripts can't tell apart).</summary>
	public static Player OwnerOf( BaseSandboxWeapon w ) => w == null ? null : w.GameObject.Components.Get<Player>( FindMode.EverythingInSelfAndAncestors );

	/// <summary>What the holder looks at within range (players through their hitboxes).</summary>
	public static SceneTraceResult Aim( BaseSandboxWeapon w, float range )
	{
		var ply = OwnerOf( w );
		var eye = ply.EyeTransform;
		var tr = w.Scene.Trace.Ray( eye.Position, eye.Position + eye.Forward * range )
			.IgnoreGameObjectHierarchy( ply.GameObject )
			.WithoutTags( "playercontroller", "trigger" )
			.UseHitboxes()
			.Run();
		if ( !tr.Hit )
		{
			tr = w.Scene.Trace.Ray( eye.Position, eye.Position + eye.Forward * range )
				.IgnoreGameObjectHierarchy( ply.GameObject )
				.WithoutTags( "playercontroller", "trigger" )
				.Radius( 8 )
				.UseHitboxes()
				.Run();
		}
		return tr;
	}

	/// <summary>The networked object to send for a hit: the player, the door, the DarkRP entity or the prop.</summary>
	public static GameObject Target( GameObject hit )
	{
		if ( !hit.IsValid() ) return null;
		var ply = hit.Components.Get<Player>( FindMode.EverythingInSelfAndAncestors );
		if ( ply != null ) return ply.GameObject;
		var door = hit.Components.Get<Sandbox.Mapping.Door>( FindMode.EverythingInSelfAndAncestors );
		if ( door != null ) return door.GameObject;
		var e = hit.Components.Get<RPEntity>( FindMode.EverythingInSelfAndAncestors );
		if ( e != null ) return e.GameObject;
		var rb = hit.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndAncestors );
		if ( rb != null ) return rb.GameObject;
		return hit;
	}

	/// <summary>Owner's machine: trace and send the use to the host.</summary>
	public static GameObject Send( BaseSandboxWeapon w, string weapon, string action, float range )
	{
		if ( OwnerOf( w ) == null || w.IsProxy ) return null;
		var tr = Aim( w, range );
		var target = tr.Hit ? Target( tr.GameObject ) : null;
		var world = RPWorld.Current();
		if ( world != null ) world.WeaponUse( weapon, action, target, tr.Hit ? tr.HitPosition : Vector3.Zero );
		return target;
	}

	public static bool IsActive( BaseSandboxWeapon w )
	{
		var owner = OwnerOf( w );
		if ( owner == null ) return false;
		var inv = owner.GetComponent<PlayerInventory>();
		return inv != null && inv.ActiveWeapon == w;
	}

	/// <summary>Reload pressed by the local holder (keys, pocket, checkers use it for menus).</summary>
	public static bool LocalReload( BaseSandboxWeapon w ) => !w.IsProxy && OwnerOf( w ) != null && IsActive( w ) && Input.Pressed( "reload" ) && !RPUi.Typing();

	/// <summary>Colours DarkRP's baton mesh (view model, world model, dropped) - not the arms holding it.</summary>
	public static void Tint( BaseSandboxWeapon w, Color c )
	{
		TintIn( w.GameObject, c );
		TintIn( w.ViewModel, c );
		TintIn( w.WorldModel, c );
	}

	static void TintIn( GameObject root, Color c )
	{
		if ( !root.IsValid() ) return;
		foreach ( var r in root.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			var m = r.Model;
			if ( m != null && m.ResourcePath != null && m.ResourcePath.Contains( "darkrp/stunbaton" ) && r.Tint != c ) r.Tint = c;
		}
	}
}

[Title( "Arrest Baton" )]
public sealed class RPArrestStick : MeleeWeapon
{
	public override void PrimaryAttack()
	{
		base.PrimaryAttack();
		RPWeaponFx.Send( this, "arrest_stick", "hit", 100 );
	}
	protected override void OnUpdate() { base.OnUpdate(); RPWeaponFx.Tint( this, new Color( 1f, 0.35f, 0.35f ) ); }
}

[Title( "Unarrest Baton" )]
public sealed class RPUnarrestStick : MeleeWeapon
{
	public override void PrimaryAttack()
	{
		base.PrimaryAttack();
		RPWeaponFx.Send( this, "unarrest_stick", "hit", 100 );
	}
	protected override void OnUpdate() { base.OnUpdate(); RPWeaponFx.Tint( this, new Color( 0.4f, 1f, 0.45f ) ); }
}

[Title( "Stun Stick" )]
public sealed class RPStunStick : MeleeWeapon
{
	public override bool CanSecondaryAttack() => NextSecondaryFire <= 0;

	public override void PrimaryAttack()
	{
		base.PrimaryAttack();
		RPWeaponFx.Send( this, "stunstick", "hit", 100 );
	}

	public override void SecondaryAttack()
	{
		SetNextSecondaryFire( 0.6f );
		RPWeaponFx.Send( this, "stunstick", "push", 100 );
	}

	protected override void OnUpdate() { base.OnUpdate(); RPWeaponFx.Tint( this, new Color( 0.45f, 0.65f, 1f ) ); }
}

[Title( "Battering Ram" )]
public sealed class RPDoorRam : MeleeWeapon
{
	public override void PrimaryAttack()
	{
		SetNextPrimaryFire( 2.5f );
		RPWeaponFx.Send( this, "door_ram", "ram", 110 );
		base.PrimaryAttack();
		SetNextPrimaryFire( 2.5f );
	}
}

[Title( "Keys" )]
public sealed class RPKeys : MeleeWeapon
{
	public override bool CanSecondaryAttack() => NextSecondaryFire <= 0;

	public override void PrimaryAttack()
	{
		SetNextPrimaryFire( 0.4f );
		RPWeaponFx.Send( this, "keys", "lock", 120 );
	}

	public override void SecondaryAttack()
	{
		SetNextSecondaryFire( 0.4f );
		RPWeaponFx.Send( this, "keys", "unlock", 120 );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !RPWeaponFx.LocalReload( this ) ) return;
		var tr = RPWeaponFx.Aim( this, 150 );
		var door = tr.Hit ? RPDoors.DoorOf( tr.GameObject ) : null;
		if ( door != null ) RPUi.OpenDoorMenu( door );
		else RPUi.Notify( 1, 3, "Look at a door to open the door menu." );
	}
}

[Title( "Lock Pick" )]
public sealed class RPLockpick : MeleeWeapon
{
	GameObject _target;
	float _start;
	float _duration;
	bool _picking;

	protected override void OnPrimaryPressed()
	{
		if ( _picking ) return;
		var target = RPWeaponFx.Send( this, "lockpick", "start", 100 );
		if ( target == null ) return;
		_target = target;
		_picking = true;
		_start = Time.Now;
		_duration = 0;
	}

	protected override void OnPrimaryDown() { }

	protected override void OnPrimaryReleased() => Cancel();

	void Cancel()
	{
		if ( !_picking ) return;
		_picking = false;
		RPUi.Progress( "", 0 );
		var w = RPWorld.Current();
		if ( w != null ) w.WeaponUse( "lockpick", "cancel", _target, Vector3.Zero );
	}

	/// <summary>The host tells us how long this lock takes.</summary>
	public void SetDuration( float seconds ) => _duration = seconds;

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !_picking || IsProxy ) return;
		if ( !RPWeaponFx.IsActive( this ) ) { Cancel(); return; }
		var tr = RPWeaponFx.Aim( this, 110 );
		if ( !tr.Hit || RPWeaponFx.Target( tr.GameObject ) != _target ) { Cancel(); return; }
		var duration = _duration > 0 ? _duration : RPUi.LockpickDuration();
		if ( duration <= 0 ) { RPUi.Progress( "Picking the lock...", 0 ); return; }
		var t = (Time.Now - _start) / duration;
		RPUi.Progress( "Picking the lock...", t );
		if ( t >= 1 )
		{
			_picking = false;
			RPUi.Progress( "", 0 );
			var w = RPWorld.Current();
			if ( w != null ) w.WeaponUse( "lockpick", "finish", _target, Vector3.Zero );
		}
	}
}

[Title( "Pocket" )]
public sealed class RPPocketWeapon : MeleeWeapon
{
	public override bool CanSecondaryAttack() => NextSecondaryFire <= 0;

	public override void PrimaryAttack()
	{
		SetNextPrimaryFire( 0.4f );
		RPWeaponFx.Send( this, "pocket", "pickup", 100 );
	}

	public override void SecondaryAttack()
	{
		SetNextSecondaryFire( 0.4f );
		var w = RPWorld.Current();
		var rp = RP.Local();
		if ( w == null || rp == null || string.IsNullOrEmpty( rp.PocketList ) ) { RPUi.Notify( 1, 3, "Your pocket is empty." ); return; }
		var count = rp.PocketList.Split( '\u0001' ).Length;
		w.PocketAction( "drop", count - 1 );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( RPWeaponFx.LocalReload( this ) ) RPUi.OpenPocket();
	}
}

[Title( "Medic Kit" )]
public sealed class RPMedKit : MeleeWeapon
{
	public override bool CanSecondaryAttack() => NextSecondaryFire <= 0;

	public override void PrimaryAttack()
	{
		SetNextPrimaryFire( 0.1f );
		RPWeaponFx.Send( this, "med_kit", "heal", 85 );
	}

	public override void SecondaryAttack()
	{
		SetNextSecondaryFire( 0.3f );
		var w = RPWorld.Current();
		if ( w != null ) w.WeaponUse( "med_kit", "self", null, Vector3.Zero );
	}

	protected override void OnUpdate() { base.OnUpdate(); RPWeaponFx.Tint( this, new Color( 1f, 0.85f, 0.85f ) ); }
}

[Title( "Weapon Checker" )]
public sealed class RPWeaponChecker : MeleeWeapon
{
	public override bool CanSecondaryAttack() => NextSecondaryFire <= 0;

	public override void PrimaryAttack()
	{
		SetNextPrimaryFire( 1f );
		RPWeaponFx.Send( this, "weaponchecker", "check", 100 );
	}

	public override void SecondaryAttack()
	{
		SetNextSecondaryFire( 2f );
		RPWeaponFx.Send( this, "weaponchecker", "confiscate", 100 );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( RPWeaponFx.LocalReload( this ) ) RPWeaponFx.Send( this, "weaponchecker", "return", 100 );
	}
}

[Title( "Keypad Checker" )]
public sealed class RPKeypadChecker : MeleeWeapon
{
	public override void PrimaryAttack()
	{
		SetNextPrimaryFire( 0.5f );
		RPWeaponFx.Send( this, "weapon_keypadchecker", "check", 150 );
	}
}

/// <summary>Host side of DarkRP's weapons.</summary>
public sealed class RPLockpickState
{
	public Connection Conn;
	public GameObject Target;
	public float Start;
	public float Duration;
}

public sealed class RPWeaponHolder
{
	public readonly List<RPLockpickState> Picks = new List<RPLockpickState>();
	/// <summary>steam id → confiscated weapon prefabs (weapon checker).</summary>
	public readonly Dictionary<string, List<string>> Confiscated = new Dictionary<string, List<string>>();
}

public static class RPWeaponLogic
{
	static readonly RPWeaponHolder H = new RPWeaponHolder();

	public static void Reset() { H.Picks.Clear(); H.Confiscated.Clear(); }

	static bool Holding( Connection c, string weapon )
	{
		var ply = RP.Pawn( c );
		var inv = ply != null ? ply.GetComponent<PlayerInventory>() : null;
		var active = inv != null ? inv.ActiveWeapon : null;
		if ( active == null ) return false;
		var path = active.GameObject.PrefabInstanceSource ?? "";
		return string.Equals( path, DarkRP.Registry().WeaponPrefab( weapon ), StringComparison.OrdinalIgnoreCase );
	}

	static bool InRange( Connection c, GameObject target, float range )
	{
		var ply = RP.Pawn( c );
		if ( ply == null || !target.IsValid() ) return false;
		var b = target.GetBounds();
		var closest = b.ClosestPoint( ply.EyeTransform.Position );
		return closest.Distance( ply.EyeTransform.Position ) <= range;
	}

	public static void Use( Connection c, string weapon, string action, GameObject target, Vector3 hitPos )
	{
		if ( c == null || !Holding( c, weapon ) ) return;
		var rp = RP.Of( c );
		if ( rp == null || rp.Arrested ) return;
		var w = RPWorld.Current();
		var cfg = DarkRP.Config();
		var targetConn = RP.ConnOfPawn( target );
		if ( targetConn == c ) targetConn = null;

		switch ( weapon )
		{
			case "arrest_stick":
				{
					if ( targetConn == null || !InRange( c, target, 130 ) ) return;
					if ( !RP.IsCP( c ) && !RP.IsAdmin( c ) ) { DarkRP.Notify( c, 1, 3, "Only the police can arrest." ); return; }
					var reason = RPPolice.ArrestBlocker( c, targetConn );
					if ( reason != null ) { DarkRP.Notify( c, 1, 4, reason ); return; }
					RPPolice.Arrest( targetConn, 0, c );
					return;
				}
			case "unarrest_stick":
				{
					if ( targetConn == null || !InRange( c, target, 130 ) ) return;
					if ( !RP.Of( targetConn ).Arrested ) { DarkRP.Notify( c, 1, 3, RP.Name( targetConn ) + " isn't arrested." ); return; }
					var h = DarkRP.RunHook( new RPHook { Name = "canUnarrest", Player = c, Target = targetConn } );
					if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't unarrest this player." : h.Reason ); return; }
					RPPolice.Unarrest( targetConn, c );
					return;
				}
			case "stunstick":
				{
					if ( !target.IsValid() || !InRange( c, target, 130 ) ) return;
					var me = RP.Pawn( c );
					var push = (target.WorldPosition - me.WorldPosition).WithZ( 0 ).Normal;
					if ( targetConn != null )
					{
						var victim = RP.Pawn( targetConn );
						if ( victim == null ) return;
						if ( action == "hit" ) victim.OnDamage( new DamageInfo { Damage = 10, Attacker = me.GameObject, Position = hitPos, Origin = me.WorldPosition } );
						var wv = RPWorld.Current();
						if ( wv != null ) using ( Rpc.FilterInclude( targetConn ) ) wv.PushRpc( push * 350 + Vector3.Up * 120 );
						return;
					}
					var e = target.Components.Get<RPEntity>();
					if ( action == "hit" && e != null ) RPEntities.Damage( e, cfg.Int( "stunstickdamage" ), me.GameObject );
					var body = target.Components.Get<Rigidbody>();
					if ( body != null && body.MotionEnabled ) body.Velocity += push * 300;
					return;
				}
			case "door_ram":
				{
					if ( !target.IsValid() || !InRange( c, target, 140 ) ) return;
					if ( !RP.IsCP( c ) && !RP.IsAdmin( c ) ) { DarkRP.Notify( c, 1, 3, "Only the police can use the battering ram." ); return; }
					var door = RPDoors.DoorOf( target );
					if ( door != null )
					{
						var reason = RPDoors.RamBlocker( w, c, door );
						if ( reason != null ) { DarkRP.Notify( c, 1, 4, reason ); return; }
						RPDoors.ForceOpen( w, door, RP.Pawn( c ).GameObject );
						RPNet.Sound( "sounds/impacts/melee/impact-melee-wood.sound", door.WorldPosition );
						DarkRP.RunHook( new RPHook { Name = "onDoorRamUsed", Player = c, Entity = door.GameObject, Allowed = true } );
						return;
					}
					var rb = target.Components.Get<Rigidbody>();
					if ( rb != null && !rb.MotionEnabled && cfg.Bool( "copscanunfreeze" ) )
					{
						rb.MotionEnabled = true;
						DarkRP.RunHook( new RPHook { Name = "onDoorRamUsed", Player = c, Entity = target } );
					}
					return;
				}
			case "keys":
				{
					var door = RPDoors.DoorOf( target );
					if ( door == null || !InRange( c, door.GameObject, 150 ) ) return;
					var d = RPDoors.Get( w, RPDoors.IdOf( door ) );
					if ( !RPDoors.HasAccess( d, rp ) ) { RPDoors.Action( c, RPDoors.IdOf( door ), "knock", "" ); return; }
					RPDoors.Action( c, RPDoors.IdOf( door ), action == "lock" ? "lock" : "unlock", "" );
					return;
				}
			case "lockpick":
				Lockpick( c, action, target );
				return;
			case "pocket":
				{
					if ( !target.IsValid() || !InRange( c, target, 110 ) ) return;
					RPPocket.Pickup( c, target );
					return;
				}
			case "med_kit":
				{
					var who = action == "self" ? c : targetConn;
					if ( who == null ) return;
					if ( action != "self" && !InRange( c, target, 100 ) ) return;
					var p = RP.Pawn( who );
					if ( p == null || p.Health >= p.MaxHealth ) return;
					p.Health = Math.Min( p.MaxHealth, p.Health + 1 );
					return;
				}
			case "weaponchecker":
				WeaponCheck( c, action, targetConn, target );
				return;
			case "weapon_keypadchecker":
				KeypadCheck( c, target );
				return;
		}
	}

	static void Lockpick( Connection c, string action, GameObject target )
	{
		var w = RPWorld.Current();
		for ( int i = H.Picks.Count - 1; i >= 0; i-- ) if ( H.Picks[i].Conn == c && action != "finish" ) H.Picks.RemoveAt( i );
		if ( action == "cancel" ) return;
		var door = RPDoors.DoorOf( target );
		if ( action == "start" )
		{
			if ( door == null || !InRange( c, door.GameObject, 120 ) ) return;
			var h = DarkRP.RunHook( new RPHook { Name = "canLockpick", Player = c, Entity = door.GameObject } );
			if ( !h.Allowed ) { DarkRP.Notify( c, 1, 3, string.IsNullOrEmpty( h.Reason ) ? "You can't pick this lock." : h.Reason ); return; }
			var d = RPDoors.Get( w, RPDoors.IdOf( door ) );
			if ( d.NonOwnable && !DarkRP.Config().Bool( "canforcedooropen" ) ) { DarkRP.Notify( c, 1, 3, "This lock can't be picked." ); return; }
			var time = Game.Random.Float( 10, 30 );
			var th = DarkRP.RunHook( new RPHook { Name = "lockpickTime", Player = c, Entity = door.GameObject, Amount = (long)time } );
			if ( th.Amount > 0 ) time = th.Amount;
			H.Picks.Add( new RPLockpickState { Conn = c, Target = door.GameObject, Start = Time.Now, Duration = time } );
			RPNet.Show( c, "lockpick", RPUtil.FloatStr( time ) );
			DarkRP.RunHook( new RPHook { Name = "lockpickStarted", Player = c, Entity = door.GameObject } );
			return;
		}
		if ( action == "finish" )
		{
			RPLockpickState st = null;
			foreach ( var p in H.Picks ) if ( p.Conn == c ) st = p;
			if ( st == null || door == null || st.Target != door.GameObject ) return;
			H.Picks.Remove( st );
			if ( Time.Now - st.Start < st.Duration - 1f ) return;
			if ( !InRange( c, door.GameObject, 130 ) ) return;
			RPDoors.ForceOpen( w, door, RP.Pawn( c ).GameObject );
			DarkRP.RunHook( new RPHook { Name = "onLockpickCompleted", Player = c, Entity = door.GameObject, Allowed = true } );
		}
	}

	static void WeaponCheck( Connection c, string action, Connection target, GameObject targetGo )
	{
		if ( target == null || !InRange( c, targetGo, 130 ) ) return;
		var ply = RP.Pawn( target );
		var inv = ply != null ? ply.GetComponent<PlayerInventory>() : null;
		if ( inv == null ) return;
		var reg = DarkRP.Registry();
		var trp = RP.Of( target );
		var job = RP.JobOf( trp );
		var jobPrefabs = RPJobs.LoadoutPrefabs( job ).Select( x => x.ToLowerInvariant() ).ToList();
		var key = trp.SteamIdText;

		if ( action == "check" )
		{
			var names = new List<string>();
			foreach ( var wpn in inv.Weapons )
			{
				var path = (wpn.GameObject.PrefabInstanceSource ?? "").ToLowerInvariant();
				if ( jobPrefabs.Contains( path ) ) continue;
				names.Add( wpn.DisplayName );
			}
			RPNet.Show( c, "weaponcheck", RP.Name( target ) + "\u0001" + (trp.License ? "1" : "0") + "\u0001" + string.Join( "\u0002", names ) );
			DarkRP.RunHook( new RPHook { Name = "playerWeaponsChecked", Player = c, Target = target } );
			return;
		}
		if ( action == "confiscate" )
		{
			if ( !RP.IsCP( c ) && !RP.IsAdmin( c ) ) return;
			if ( !H.Confiscated.ContainsKey( key ) ) H.Confiscated[key] = new List<string>();
			int n = 0;
			foreach ( var wpn in inv.Weapons.ToList() )
			{
				var path = (wpn.GameObject.PrefabInstanceSource ?? "").ToLowerInvariant();
				if ( jobPrefabs.Contains( path ) || string.IsNullOrEmpty( path ) ) continue;
				H.Confiscated[key].Add( path );
				inv.Remove( wpn );
				n++;
			}
			DarkRP.Notify( c, 0, 4, "You confiscated " + n + " weapon(s) from " + RP.Name( target ) + "." );
			if ( n > 0 ) DarkRP.Notify( target, 1, 4, RP.Name( c ) + " confiscated your weapons." );
			DarkRP.RunHook( new RPHook { Name = "playerWeaponsConfiscated", Player = c, Target = target, Amount = n } );
			return;
		}
		if ( action == "return" )
		{
			if ( !H.Confiscated.ContainsKey( key ) || H.Confiscated[key].Count == 0 ) { DarkRP.Notify( c, 1, 3, RP.Name( target ) + " has no confiscated weapons." ); return; }
			foreach ( var path in H.Confiscated[key] ) inv.Pickup( path, false );
			DarkRP.Notify( c, 0, 4, "You gave " + RP.Name( target ) + " their weapons back." );
			H.Confiscated.Remove( key );
			DarkRP.RunHook( new RPHook { Name = "playerWeaponsReturned", Player = c, Target = target } );
		}
	}

	/// <summary>Shows what a button or keypad is wired to (Sandbox's links).</summary>
	static void KeypadCheck( Connection c, GameObject target )
	{
		if ( !target.IsValid() || !InRange( c, target, 160 ) ) return;
		var lines = new List<string>();
		var root = target.Root;
		foreach ( var link in root.Components.GetAll<ManualLink>( FindMode.EverythingInSelfAndDescendants ) )
		{
			lines.Add( link.ToString() );
		}
		var h = DarkRP.RunHook( new RPHook { Name = "keypadCheck", Player = c, Entity = target, Data = lines } );
		RPNet.Show( c, "keypadcheck", target.Name + "\u0001" + string.Join( "\u0002", lines ) );
	}
}
