// DarkRP for s&box - the networked "DarkRP" object: global state (laws, lockdown, doors, votes, content), the host's
// game loop, and every request a client can send. Logic lives in the module classes (RPJobs, RPPolice, RPDoors...).

/// <summary>
/// Creates the DarkRP world object on the host when the first connection becomes active (the host's own included),
/// or when a player spawns. No engine-held callbacks: those break when scripts hotload.
/// </summary>
public sealed class DarkRPSystem : GameObjectSystem, Component.INetworkListener, Global.IPlayerEvents
{
	public DarkRPSystem( Scene scene ) : base( scene )
	{
	}

	void Start()
	{
		if ( Scene == null || Scene.IsEditor ) return;
		if ( Networking.IsActive && !Networking.IsHost ) return;
		// Sandbox's "re-equip last" would let clients restore any weapons they carried: DarkRP gives loadouts itself.
		ServerSettings.ReEquipLast = false;
		RPWorld.Ensure( Scene );
	}

	public void OnActive( Connection channel ) => Start();
	public void OnPlayerSpawned( Player player ) => Start();

	public override void Dispose()
	{
		RPRegistries.Forget( Scene );
		base.Dispose();
	}
}

[Title( "DarkRP World" ), Hide]
public sealed class RPWorld : Component, Global.IConnectionEvents, Global.IPlayerEvents, Global.ISpawnEvents, Component.IDamageable.IEvents, IToolActionEvents, ICleanupEvents
{
	/// <summary>Content (jobs, shipments...) and shared config, sent by the host.</summary>
	[Sync( SyncFlags.FromHost )] public string ContentJson { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public NetList<string> Laws { get; set; } = new NetList<string>();
	[Sync( SyncFlags.FromHost )] public bool Lockdown { get; set; }
	/// <summary>Agenda title → text.</summary>
	[Sync( SyncFlags.FromHost )] public NetDictionary<string, string> AgendaTexts { get; set; } = new NetDictionary<string, string>();
	/// <summary>Door object id → packed door state (see RPDoorData).</summary>
	[Sync( SyncFlags.FromHost )] public NetDictionary<string, string> Doors { get; set; } = new NetDictionary<string, string>();
	/// <summary>Running votes (JSON list).</summary>
	[Sync( SyncFlags.FromHost )] public string VotesJson { get; set; } = "[]";
	/// <summary>Seconds until the next payday.</summary>
	[Sync( SyncFlags.FromHost )] public int PaydayIn { get; set; }
	/// <summary>Running lottery: "price|pot|seconds left" or empty.</summary>
	[Sync( SyncFlags.FromHost )] public string Lottery { get; set; } = "";
	/// <summary>Open hit requests (hitman module): "hitman steamid|target name|price;..."</summary>
	[Sync( SyncFlags.FromHost )] public string Hits { get; set; } = "";
	/// <summary>Prop protection settings (RPPPSettings JSON) and buddy lists (owner SteamId → SteamIds).</summary>
	[Sync( SyncFlags.FromHost )] public string PropProtectionJson { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public NetDictionary<string, string> PPBuddies { get; set; } = new NetDictionary<string, string>();

	// ---- host only ----
	int _syncedVersion = -1;
	float _nextSecond;
	public float LockdownCooldownUntil;
	public float NextPayday;
	public float NextWalletTax;
	public bool MapLoaded;

	// ---- client only ----
	string _appliedJson = "";

	public static RPWorld Find( Scene scene )
	{
		if ( scene == null ) return null;
		return scene.GetAllComponents<RPWorld>().FirstOrDefault();
	}

	public static RPWorld Current() => Find( Game.ActiveScene );

	/// <summary>Host: creates the networked DarkRP object if the scene has none.</summary>
	public static RPWorld Ensure( Scene scene )
	{
		if ( scene == null || scene.IsEditor ) return null;
		var existing = Find( scene );
		if ( existing != null ) return existing;
		if ( Networking.IsActive && !Networking.IsHost ) return null;
		var go = scene.CreateObject( true );
		go.Name = "DarkRP";
		go.Flags = GameObjectFlags.NotSaved;
		var w = go.AddComponent<RPWorld>();
		go.NetworkSpawn();
		return w;
	}

	protected override void OnStart()
	{
		if ( IsProxy ) return;
		// Static state survives script reloads and play sessions: start clean.
		RPJobs.Reset();
		RPLooks.Reset();
		RPVotes.Reset();
		RPWeaponLogic.Reset();
		RPHits.Reset();
		RPPropProtection.Reset();
		var reg = RPRegistries.For( Scene );
		if ( !reg.Built ) reg.Build();
		Laws.Clear();
		foreach ( var law in reg.DefaultLaws ) Laws.Add( law );
		NextPayday = Time.Now + reg.Config.Int( "paydelay" );
		NextWalletTax = Time.Now + reg.Config.Int( "wallettaxtime" );
		RPMap.Load( this );
		RPLog.Write( "DarkRP " + DarkRP.Version + " started: " + reg.Jobs.Count + " jobs, " + reg.Shipments.Count + " shipments, " + reg.Entities.Count + " entities. Config: data/darkrp/" );
	}

	bool _clientReset;

	protected override void OnUpdate()
	{
		if ( IsProxy && !_clientReset ) { _clientReset = true; RPLooks.Reset(); }
		// Clients (and the host's own UI): take the host's content, make sure the HUD exists.
		if ( IsProxy && ContentJson != _appliedJson && !string.IsNullOrEmpty( ContentJson ) )
		{
			_appliedJson = ContentJson;
			var reg = RPRegistries.For( Scene );
			if ( !reg.Built ) reg.Build();
			RPContentJson.ApplyFromHost( reg, ContentJson );
		}
		RPUi.Ensure( Scene );
		RPLooks.Update( Scene );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;
		var reg = DarkRP.Registry();
		if ( reg == null ) return;
		if ( reg.Version != _syncedVersion )
		{
			_syncedVersion = reg.Version;
			ContentJson = RPContentJson.ForClients( reg );
		}
		try { RPJobs.Tick( this ); } catch ( Exception e ) { Log.Warning( "DarkRP spawn tick: " + e.Message ); }
		if ( Time.Now < _nextSecond ) return;
		_nextSecond = Time.Now + 1f;
		try
		{
			RPPlayers.EnsureAll( this );
			RPPolice.Tick( this );
			RPMoney.Tick( this );
			RPHunger.Tick( this );
			RPVotes.Tick( this );
			RPMayor.Tick( this );
			RPHits.Tick( this );
			RPPropProtection.Tick( this );
			RPPlayers.AutoSave( this );
		}
		catch ( Exception e ) { Log.Warning( "DarkRP tick: " + e ); }
	}

	protected override void OnDestroy()
	{
		if ( IsProxy ) return;
		RPPlayers.SaveAll();
		RPMap.Save( this );
	}

	/// <summary>Local UI automation for tests and tools: "menu" (tab), "click" (action), "close", "chat" (text).</summary>
	public void UiTest( string what, string arg )
	{
		if ( what == "menu" ) { RPUi.H.MenuOpen = true; RPUi.H.MenuTab = arg; }
		else if ( what == "click" ) RPUi.Click( arg );
		else if ( what == "close" ) { RPUi.H.MenuOpen = false; RPUi.CloseDialog(); }
		else if ( what == "chat" ) RPUi.H.ChatOpen = arg == "1";
		else if ( what == "score" ) RPUi.H.ScoreForced = arg == "1";
		else if ( what == "dialog" ) RPUi.Show( arg, "" );
		else if ( what == "door" ) { RPUi.H.DoorId = arg; RPUi.H.Dialog = "door"; }
		else if ( what == "prompt" ) RPUi.Prompt( arg, "/advert", "Test prompt" );
	}

	// ======== client → host requests ========

	/// <summary>Chat: plain text or a /command.</summary>
	[Rpc.Host]
	public void Say( string text, bool team )
	{
		RPChat.HandleSay( Rpc.Caller, text, team );
	}

	/// <summary>Runs a chat command as if typed (the menu's buttons use these).</summary>
	[Rpc.Host]
	public void RunCommand( string text )
	{
		RPChat.RunCommand( Rpc.Caller, text );
	}

	[Rpc.Host]
	public void Buy( string kind, string id, int count )
	{
		RPShop.Buy( Rpc.Caller, kind, id, count );
	}

	[Rpc.Host]
	public void DoorAction( string doorId, string action, string arg )
	{
		RPDoors.Action( Rpc.Caller, doorId, action, arg );
	}

	[Rpc.Host]
	public void CastVote( int voteId, bool yes )
	{
		RPVotes.Cast( Rpc.Caller, voteId, yes );
	}

	[Rpc.Host]
	public void PocketAction( string action, int index )
	{
		RPPocket.Action( Rpc.Caller, action, index );
	}

	[Rpc.Host]
	public void AdminAction( string action, string a, string b )
	{
		RPAdmin.Action( Rpc.Caller, action, a, b );
	}

	/// <summary>A DarkRP weapon hit or used something (the owner's trace; the host checks range and rules).</summary>
	[Rpc.Host]
	public void WeaponUse( string weapon, string action, GameObject target, Vector3 hitPos )
	{
		RPWeaponLogic.Use( Rpc.Caller, weapon, action, target, hitPos );
	}

	[Rpc.Host]
	public void EntityAction( Guid entityId, string action, string arg )
	{
		RPEntities.Action( Rpc.Caller, entityId, action, arg );
	}

	// ======== host → client messages ========

	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void NotifyRpc( int type, float seconds, string message )
	{
		RPUi.Notify( type, seconds, message );
	}

	/// <summary>A chat line: segments "#rrggbb\u0001text\u0002#rrggbb\u0001text".</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void ChatRpc( string segments )
	{
		RPUi.ChatLine( segments );
	}

	/// <summary>Opens a client panel (weapon check results, a letter...).</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void ShowRpc( string panel, string data )
	{
		RPUi.Show( panel, data );
	}

	/// <summary>Moves the local player (players are simulated by their owner, so the host asks them).</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void TeleportRpc( Vector3 position )
	{
		var ply = Player.FindLocalPlayer();
		if ( ply == null ) return;
		ply.WorldPosition = position;
	}

	/// <summary>Pushes the local player (stunstick).</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void PushRpc( Vector3 velocity )
	{
		var ply = Player.FindLocalPlayer();
		if ( ply == null || ply.Controller == null ) return;
		ply.Controller.Jump( velocity );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void SoundRpc( string sound, Vector3 position )
	{
		if ( string.IsNullOrEmpty( sound ) ) return;
		var h = Sound.Play( sound, position );
	}

	// ======== events (host) ========

	public void OnPlayerJoined( PlayerData playerData )
	{
		if ( IsProxy ) return;
		RPPlayers.Join( this, playerData );
	}

	public void OnPlayerLeft( PlayerData playerData )
	{
		if ( IsProxy ) return;
		RPPlayers.Leave( this, playerData );
	}

	public void OnPlayerSpawned( Player player )
	{
		if ( IsProxy ) return;
		RPJobs.OnSpawned( this, player );
	}

	public void OnPlayerDied( Player player, PlayerDiedParams args )
	{
		if ( IsProxy ) return;
		RPJobs.OnDied( this, player, args );
	}

	public void OnPlayerLoadout( PlayerLoadoutEvent e )
	{
		if ( IsProxy ) return;
		RPJobs.OnLoadout( this, e );
	}

	public void OnPlayerRespawning( PlayerRespawnEvent e )
	{
		if ( IsProxy ) return;
		RPJobs.OnRespawning( this, e );
	}

	public void OnPlayerKill( PlayerKillEvent e )
	{
		if ( IsProxy ) return;
		RPMoney.OnKill( e );
	}

	public void OnPlayerDrop( PlayerDropEvent e )
	{
		if ( IsProxy ) return;
		RPJobs.OnDrop( e );
	}

	public void OnSpawn( Global.ISpawnEvents.SpawnData e )
	{
		if ( IsProxy ) return;
		RPAdmin.OnSpawnMenu( e );
		RPPropProtection.OnSpawn( e );
	}

	/// <summary>The local UI state (tests).</summary>
	public string UiState() => "chatOpen=" + RPUi.H.ChatOpen + " menuOpen=" + RPUi.H.MenuOpen + " dialog='" + RPUi.H.Dialog + "' typing=" + RPUi.Typing() + " menus=" + RPHud.MenusOpen() + " pressed=" + Input.Pressed( "Chat" );

	/// <summary>Tests (host): act as another connected player. "touch" (arg: object name) → prop protection answers,
	/// "spawn" (arg: model path or cloud ident) → whether the spawn is refused, "cmd" (arg: chat text) → runs it as them.</summary>
	public string TestAs( string player, string action, string arg )
	{
		var c = RP.Find( player );
		if ( c == null ) return "no player " + player;
		if ( action == "cmd" ) { RPChat.RunCommand( c, arg ); return "ran " + arg + " as " + RP.Name( c ); }
		if ( action == "touch" )
		{
			var go = Scene.GetAllObjects( true ).FirstOrDefault( g => g.Name == arg );
			if ( go == null ) return "no object " + arg;
			var ev = new IPhysgunEvent.GrabEvent { Grabber = c };
			go.Root.RunEvent<IPhysgunEvent>( x => x.OnPhysgunGrab( ev ) );
			return arg + " owner=" + RPPropProtection.OwnerName( RPPropProtection.OwnerOf( go ) ) + " physgun=" + RPPropProtection.CanTouch( c, go, "physgun" ) + " toolgun=" + RPPropProtection.CanTouch( c, go, "toolgun" ) + " pickup=" + RPPropProtection.CanTouch( c, go, "pickup" ) + " damage=" + RPPropProtection.CanTouch( c, go, "damage" ) + " grabCancelled=" + ev.Cancelled + " sandboxAccess=" + go.HasAccess( c );
		}
		if ( action == "spawn" )
		{
			var e = new Global.ISpawnEvents.SpawnData { Spawner = new PropSpawner( arg ), Player = c };
			RPPropProtection.OnSpawn( e );
			return "spawn " + arg + " cancelled=" + e.Cancelled;
		}
		return "unknown action";
	}

	/// <summary>Prop protection: every guarded object and its owner (tests, console).</summary>
	public string PPReport() => RPPropProtection.Report( Scene );

	/// <summary>Prop protection: buddies (anyone) and settings, blocked models, cleanup (admins).</summary>
	[Rpc.Host]
	public void PPAction( string action, string a, string b )
	{
		RPPropProtection.Action( Rpc.Caller, action, a, b );
	}

	public void OnPostSpawn( Global.ISpawnEvents.PostSpawnData e )
	{
		if ( IsProxy ) return;
		RPPropProtection.OnPostSpawn( e );
	}

	public void OnDamaging( Component.IDamageable.DamageEvent e )
	{
		if ( IsProxy ) return;
		RPPropProtection.OnDamaging( e );
		RPPolice.OnDamaging( e );
	}

	public void OnPlayerCarry( PlayerCarryEvent e )
	{
		if ( IsProxy ) return;
		RPPropProtection.OnCarry( e );
	}

	public void OnToolAction( IToolActionEvents.ActionData e )
	{
		if ( IsProxy ) return;
		RPPropProtection.OnToolAction( e );
	}

	public void OnPostToolAction( IToolActionEvents.PostActionData e )
	{
		if ( IsProxy ) return;
		RPPropProtection.OnPostToolAction( e );
	}

	public void OnCleaningUp( CleanupEvent e )
	{
		if ( IsProxy ) return;
		RPPropProtection.OnCleaningUp( e );
	}

	public void OnCleanup( int removedObjects, int restoredObjects ) { }

	public void OnDamaged( Component.IDamageable.DamageEvent e ) { }
	public void OnDying( Component.IDamageable.DyingEvent e ) { }
	public void OnDied( Component.IDamageable.DeathEvent e ) { }
}

/// <summary>Sends notices and chat lines to players (host).</summary>
public sealed class RPNet
{
	public static RPWorld W() => RPWorld.Current();

	public static void Notify( Connection c, int type, float seconds, string message )
	{
		var w = W();
		if ( w == null || c == null ) { Log.Info( "DarkRP: " + message ); return; }
		using ( Rpc.FilterInclude( c ) ) w.NotifyRpc( type, seconds, message );
	}

	public static void NotifyAll( int type, float seconds, string message )
	{
		var w = W();
		if ( w == null ) return;
		w.NotifyRpc( type, seconds, message );
	}

	public static string Seg( string color, string text ) => color + "\u0001" + text;
	public static string Join( string a, string b ) => a + "\u0002" + b;

	public static void Chat( Connection c, string text, string color )
	{
		var w = W();
		if ( w == null || c == null ) return;
		using ( Rpc.FilterInclude( c ) ) w.ChatRpc( Seg( color ?? "#ffffff", text ) );
	}

	public static void ChatAll( string text, string color )
	{
		var w = W();
		if ( w == null ) return;
		w.ChatRpc( Seg( color ?? "#ffffff", text ) );
	}

	/// <summary>A full chat line (built with Seg/Join) to the given players.</summary>
	public static void ChatLine( List<Connection> to, string segments )
	{
		var w = W();
		if ( w == null || to == null || to.Count == 0 ) return;
		using ( Rpc.FilterInclude( to ) ) w.ChatRpc( segments );
	}

	public static void Show( Connection c, string panel, string data )
	{
		var w = W();
		if ( w == null || c == null ) return;
		using ( Rpc.FilterInclude( c ) ) w.ShowRpc( panel, data );
	}

	public static void Sound( string sound, Vector3 position )
	{
		var w = W();
		if ( w == null ) return;
		w.SoundRpc( sound, position );
	}
}

public sealed class RPLogHolder
{
	public readonly List<string> Lines = new List<string>();
}

/// <summary>DarkRP's log (console + data/darkrp/logs/<date>.txt when "logging" is on; written every few seconds).</summary>
public static class RPLog
{
	static readonly RPLogHolder H = new RPLogHolder();

	public static void Write( string text )
	{
		Log.Info( "[DarkRP] " + text );
		var cfg = DarkRP.Config();
		if ( cfg == null || !cfg.Bool( "logging" ) || !(Networking.IsHost || !Networking.IsActive) ) return;
		H.Lines.Add( DateTime.Now.ToString( "HH:mm:ss" ) + " " + text );
		if ( H.Lines.Count > 200 ) Flush();
	}

	public static void Flush()
	{
		if ( H.Lines.Count == 0 ) return;
		try
		{
			var file = "logs/" + DateTime.Now.ToString( "yyyy-MM-dd" ) + ".txt";
			var old = RPDataFiles.Read( file ) ?? "";
			if ( old.Length > 4000000 ) old = "";
			RPDataFiles.Write( file, old + string.Join( "\n", H.Lines ) + "\n" );
		}
		catch { }
		H.Lines.Clear();
	}
}
