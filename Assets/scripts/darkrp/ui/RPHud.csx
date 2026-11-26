// DarkRP for s&box - the HUD (wallet, job, salary, health, armour, hunger, ammo, wanted/arrest status, agenda, lockdown,
// votes, what you look at) and DarkRP's chat box. Layout ideas from DarkRP's modules/hud and chat (MIT, Falco Peijnenburg).
using Sandbox.UI;

[Title( "DarkRP HUD" ), Hide]
public sealed class RPHud : PanelComponent
{
	const string Css = @"
.hud-info { position: absolute; left: 24px; bottom: 24px; flex-direction: column; gap: 3px; width: 300px; padding: 12px 16px 14px; }
.hud-info .name { font-size: 18px; font-weight: 800; }
.hud-info .job-row { flex-direction: row; align-items: center; gap: 8px; }
.hud-info .dot { width: 10px; height: 10px; border-radius: 5px; }
.hud-info .job { font-size: 14px; font-weight: 600; }
.hud-info .money-row { flex-direction: row; align-items: flex-end; gap: 10px; margin-top: 2px; }
.hud-info .wallet { font-size: 22px; font-weight: 800; color: #6fe08a; }
.hud-info .salary { font-size: 12px; color: #9aa4ae; margin-bottom: 4px; }
.hud-info .chips { flex-direction: row; gap: 6px; flex-wrap: wrap; margin-top: 4px; }
.stat { flex-direction: row; align-items: center; gap: 8px; margin-top: 6px; }
.stat .icon { font-size: 16px; width: 18px; }
.stat .bar { flex-grow: 1; height: 10px; border-radius: 5px; background-color: rgba( 255, 255, 255, 0.1 ); overflow: hidden; }
.stat .fill { height: 100%; border-radius: 5px; }
.stat .num { font-size: 14px; font-weight: 800; width: 40px; text-align: right; }
.stat.health .icon, .stat.health .num { color: #ff6b6b; }
.stat.health .fill { background-color: #e04848; }
.stat.health.low .fill { background-color: #ff2d2d; }
.stat.armor .icon, .stat.armor .num { color: #6fb7ff; }
.stat.armor .fill { background-color: #3f8fe0; }
.stat.energy .icon, .stat.energy .num { color: #f0b85a; }
.stat.energy .fill { background-color: #e0a64b; }
.chip { font-size: 11px; font-weight: 800; letter-spacing: 0.5px; padding: 2px 8px; border-radius: 10px; background-color: rgba( 255, 255, 255, 0.1 ); }
.chip.wanted { background-color: #c0392b; }
.chip.warrant { background-color: #d68910; }
.chip.arrested { background-color: #6c3483; }
.chip.license { background-color: #1f7a4a; }
.ammo { position: absolute; right: 24px; bottom: 24px; flex-direction: column; align-items: flex-end; padding: 10px 18px 12px; min-width: 170px; }
.ammo .wname { font-size: 12px; font-weight: 800; letter-spacing: 1px; color: #9aa4ae; text-transform: uppercase; }
.ammo .counts { flex-direction: row; align-items: flex-end; gap: 6px; }
.ammo .clip { font-size: 40px; font-weight: 800; }
.ammo .clip.low { color: #ff6b6b; }
.ammo .reserve { font-size: 18px; font-weight: 700; color: #c5ccd3; margin-bottom: 6px; }
.agenda { position: absolute; left: 24px; top: 24px; flex-direction: column; width: 320px; padding: 10px 14px; gap: 3px; }
.agenda .title { font-size: 12px; font-weight: 800; letter-spacing: 1px; color: #9aa4ae; text-transform: uppercase; margin-bottom: 2px; }
.agenda .text { font-size: 13px; }
.banner { position: absolute; top: 22px; left: 0; right: 0; justify-content: center; }
.banner .inner { padding: 8px 22px; border-radius: 8px; font-size: 16px; font-weight: 800; letter-spacing: 1px; }
.banner .lockdown { background-color: rgba( 192, 57, 43, 0.92 ); }
.banner .lottery { background-color: rgba( 31, 122, 74, 0.92 ); margin-left: 10px; font-size: 13px; }
.votes { position: absolute; left: 24px; top: 34%; flex-direction: column; gap: 8px; width: 300px; }
.vote { flex-direction: column; gap: 6px; padding: 10px 14px; }
.vote .q { font-size: 13px; font-weight: 600; }
.vote .row { flex-direction: row; align-items: center; gap: 8px; }
.vote .keys { font-size: 12px; color: #9aa4ae; }
.vote .count { font-size: 12px; }
.target { position: absolute; left: 0; right: 0; top: 56%; flex-direction: column; align-items: center; }
.target .box { flex-direction: column; align-items: center; padding: 8px 16px; gap: 2px; max-width: 420px; }
.target .t-title { font-size: 16px; font-weight: 800; }
.target .t-line { font-size: 12px; text-align: center; }
.progress { position: absolute; left: 0; right: 0; top: 62%; flex-direction: column; align-items: center; }
.progress .box { flex-direction: column; width: 320px; padding: 10px 14px; gap: 6px; }
.progress .pbar { height: 6px; border-radius: 3px; background-color: rgba( 255, 255, 255, 0.1 ); overflow: hidden; }
.progress .pfill { height: 100%; border-radius: 3px; background-color: #4aa3ff; }
.owner { position: absolute; left: 10px; top: 50%; padding: 4px 10px; border-radius: 5px; background-color: rgba( 0, 0, 0, 0.62 ); font-size: 15px; font-weight: 700; color: #74e08f; text-shadow: 0 1px 2px rgba( 0, 0, 0, 0.8 ); }
.owner.denied { color: #ff6464; }
.hint { position: absolute; left: 0; right: 0; bottom: 6px; text-align: center; font-size: 12px; color: rgba( 255, 255, 255, 0.4 ); }
";

	RPClickRoot _root;
	Label _name, _job, _wallet, _salary, _hpNum, _armorNum, _energyNum, _wname, _clip, _reserve;
	Panel _dot, _chips, _hpStat, _hpFill, _armorStat, _armorFill, _energyStat, _energyFill, _agenda, _banner, _votes, _target, _progress, _progressFill, _info, _ammo;
	Label _progressText, _hint, _owner;
	string _chipKey, _agendaKey, _bannerKey, _voteKey, _targetKey;

	bool _vitalsOff;

	/// <summary>DarkRP draws health and ammo itself: Sandbox's vitals (health, armour, ammo) are switched off, and
	/// switched back on when the "hud" module is disabled in darkrp/disabled_defaults.json.</summary>
	void SandboxVitals( bool on )
	{
		if ( !on )
		{
			foreach ( var v in Scene.GetAllComponents<Vitals>() ) if ( v.Enabled ) v.Enabled = false;
			_vitalsOff = true;
			return;
		}
		if ( !_vitalsOff ) return;
		_vitalsOff = false;
		foreach ( var go in Scene.GetAllObjects( false ) )
		{
			var v = go.Components.Get<Vitals>( true );
			if ( v != null && !v.Enabled ) v.Enabled = true;
		}
	}

	/// <summary>The HUD steps aside while the spawn menu (Q) or the inspect/context menu (C) is open, like Sandbox's.</summary>
	public static bool MenusOpen() => SpawnMenuHost.IsOpen;

	protected override void OnUpdate()
	{
		var root = Panel;
		if ( root == null ) return;
		if ( _root == null || !_root.IsValid() ) Build( root );
		var hudOn = !DarkRP.ModuleDisabled( "hud" );
		SandboxVitals( !hudOn );
		_hint.Style.Display = hudOn ? DisplayMode.Flex : DisplayMode.None;
		_agenda.Style.Display = hudOn ? _agenda.Style.Display : DisplayMode.None;

		HandleKeys();
		_root.Style.Display = MenusOpen() ? DisplayMode.None : DisplayMode.Flex;
		var rp = RP.Local();
		var w = RPWorld.Current();
		var cfg = DarkRP.Config();
		var ply = Player.FindLocalPlayer();
		_info.Style.Display = rp != null && hudOn ? DisplayMode.Flex : DisplayMode.None;
		if ( !hudOn ) _ammo.Style.Display = DisplayMode.None;
		if ( rp == null || w == null || cfg == null ) return;

		_name.Text = RP.NameOf( rp );
		_job.Text = RP.JobName( rp );
		_dot.Style.BackgroundColor = RP.JobColor( rp );
		_wallet.Text = RPUtil.Money( rp.Money );
		_salary.Text = "+" + RPUtil.Money( rp.Salary ) + " in " + RPUtil.Duration( w.PaydayIn );

		float hp = ply != null ? Math.Max( 0, ply.Health ) : 0;
		float maxHp = ply != null && ply.MaxHealth > 0 ? ply.MaxHealth : 100;
		_hpNum.Text = ((int)Math.Ceiling( hp )).ToString();
		_hpFill.Style.Width = Length.Percent( Math.Clamp( hp / maxHp * 100f, 0, 100 ) );
		_hpStat.SetClass( "low", hp > 0 && hp / maxHp < 0.25f );
		float armor = ply != null ? ply.Armour : 0;
		_armorStat.Style.Display = armor > 0 ? DisplayMode.Flex : DisplayMode.None;
		_armorNum.Text = ((int)armor).ToString();
		_armorFill.Style.Width = Length.Percent( Math.Clamp( armor, 0, 100 ) );

		var hunger = cfg.Bool( "hungermod" );
		_energyStat.Style.Display = hunger ? DisplayMode.Flex : DisplayMode.None;
		if ( hunger )
		{
			_energyFill.Style.Width = Length.Percent( Math.Clamp( rp.Energy, 0, 100 ) );
			_energyNum.Text = ((int)rp.Energy).ToString();
		}

		var myHit = "";
		foreach ( var part in (w.Hits ?? "").Split( ';', StringSplitOptions.RemoveEmptyEntries ) )
		{
			var hparts = part.Split( '|' );
			if ( hparts.Length >= 3 && hparts[0] == rp.SteamIdText ) myHit = hparts[1] + " (" + RPUtil.Money( (long)RPUtil.ParseFloat( hparts[2], 0 ) ) + ")";
		}
		var chipKey = rp.Wanted + "|" + rp.WantedReason + "|" + rp.Warranted + "|" + rp.Arrested + "|" + rp.ArrestLeft + "|" + rp.License + "|" + myHit;
		if ( chipKey != _chipKey )
		{
			_chipKey = chipKey;
			_chips.DeleteChildren( true );
			if ( rp.Arrested ) RPUi.Text( _chips, "ARRESTED " + RPUtil.Duration( rp.ArrestLeft ), "chip arrested" );
			if ( rp.Wanted ) RPUi.Text( _chips, "WANTED: " + rp.WantedReason, "chip wanted" );
			if ( rp.Warranted ) RPUi.Text( _chips, "WARRANT", "chip warrant" );
			if ( rp.License ) RPUi.Text( _chips, "GUN LICENSE", "chip license" );
			if ( myHit != "" ) RPUi.Text( _chips, "HIT: " + myHit, "chip wanted" );
		}
		_chips.Style.Display = _chips.ChildrenCount > 0 ? DisplayMode.Flex : DisplayMode.None;

		if ( hudOn ) UpdateAmmo( ply );
		if ( hudOn ) UpdateAgenda( w, rp );
		else _agendaKey = null;
		UpdateBanner( w );
		UpdateVotes( w );
		UpdateTarget( w );
		UpdateOwner( ply );
		UpdateProgress();
	}

	void UpdateAmmo( Player ply )
	{
		var inv = ply != null ? ply.GetComponent<PlayerInventory>() : null;
		var weapon = inv != null ? inv.ActiveWeapon : null;
		var show = weapon != null && weapon.UsesAmmo;
		_ammo.Style.Display = show ? DisplayMode.Flex : DisplayMode.None;
		if ( !show ) return;
		_wname.Text = weapon.DisplayName;
		if ( weapon.UsesClips )
		{
			_clip.Text = weapon.Clip1.ToString();
			_reserve.Text = "/ " + weapon.Ammo1;
			_clip.SetClass( "low", weapon.ClipMaxSize > 0 && weapon.Clip1 <= weapon.ClipMaxSize * 0.3f );
			_reserve.Style.Display = DisplayMode.Flex;
		}
		else
		{
			_clip.Text = weapon.Ammo1.ToString();
			_clip.SetClass( "low", weapon.Ammo1 <= 0 );
			_reserve.Style.Display = DisplayMode.None;
		}
	}

	Panel Stat( Panel parent, string cls, string icon )
	{
		var row = RPUi.Div( parent, "stat " + cls );
		RPUi.Icon( row, icon, "" );
		var bar = RPUi.Div( row, "bar" );
		var fill = RPUi.Div( bar, "fill" );
		fill.AddClass( "fill-" + cls );
		return row;
	}

	void Build( Panel root )
	{
		root.StyleSheet.Parse( RPStyle.Base );
		root.StyleSheet.Parse( Css );
		_root = root.AddChild<RPClickRoot>();
		_root.AddClass( "rp" );

		// Bottom left: DarkRP's info box, which replaces Sandbox's health display.
		_info = RPUi.Div( _root, "hud-info glass" );
		_name = RPUi.Text( _info, "", "name" );
		var jobRow = RPUi.Div( _info, "job-row" );
		_dot = RPUi.Div( jobRow, "dot" );
		_job = RPUi.Text( jobRow, "", "job" );
		var moneyRow = RPUi.Div( _info, "money-row" );
		_wallet = RPUi.Text( moneyRow, "", "wallet" );
		_salary = RPUi.Text( moneyRow, "", "salary" );
		_hpStat = Stat( _info, "health", "favorite" );
		_hpFill = _hpStat.GetChild( 1 ).GetChild( 0 );
		_hpNum = RPUi.Text( _hpStat, "", "num" );
		_armorStat = Stat( _info, "armor", "shield" );
		_armorFill = _armorStat.GetChild( 1 ).GetChild( 0 );
		_armorNum = RPUi.Text( _armorStat, "", "num" );
		_energyStat = Stat( _info, "energy", "restaurant" );
		_energyFill = _energyStat.GetChild( 1 ).GetChild( 0 );
		_energyNum = RPUi.Text( _energyStat, "", "num" );
		_chips = RPUi.Div( _info, "chips" );

		// Bottom right: ammo, which replaces Sandbox's ammo display.
		_ammo = RPUi.Div( _root, "ammo glass" );
		_wname = RPUi.Text( _ammo, "", "wname" );
		var counts = RPUi.Div( _ammo, "counts" );
		_clip = RPUi.Text( counts, "", "clip" );
		_reserve = RPUi.Text( counts, "", "reserve" );

		_agenda = RPUi.Div( _root, "agenda glass" );
		_banner = RPUi.Div( _root, "banner" );
		_votes = RPUi.Div( _root, "votes" );
		_target = RPUi.Div( _root, "target" );
		_progress = RPUi.Div( _root, "progress" );
		var pbox = RPUi.Div( _progress, "box glass" );
		_progressText = RPUi.Text( pbox, "", "t-line" );
		var pbar = RPUi.Div( pbox, "pbar" );
		_progressFill = RPUi.Div( pbar, "pfill" );
		_hint = RPUi.Text( _root, "M  DarkRP menu   ·   Enter  chat   ·   Tab  scoreboard", "hint" );
		_owner = RPUi.Text( _root, "", "owner" );
		_chipKey = _agendaKey = _bannerKey = _voteKey = _targetKey = null;
	}

	void HandleKeys()
	{
		var h = RPUi.H;
		if ( RPUi.Typing() ) return;
		if ( Input.Keyboard.Pressed( "m" ) && h.Dialog == "" )
		{
			h.MenuOpen = !h.MenuOpen;
			return;
		}
		if ( (h.MenuOpen || h.Dialog != "") && (Input.EscapePressed || Input.Keyboard.Pressed( "escape" )) )
		{
			Input.EscapePressed = false;
			h.MenuOpen = false;
			RPUi.CloseDialog();
			return;
		}
		if ( h.MenuOpen || h.Dialog != "" ) return;
		bool yes = Input.Keyboard.Pressed( "y" ), no = Input.Keyboard.Pressed( "n" );
		if ( !yes && !no ) return;
		var vote = FirstOpenVote();
		var w = RPWorld.Current();
		if ( vote > 0 && w != null ) w.CastVote( vote, yes );
	}

	/// <summary>The oldest vote the local player can still answer.</summary>
	public static int FirstOpenVote()
	{
		var w = RPWorld.Current();
		if ( w == null ) return 0;
		var arr = RPJson.Parse( "{\"v\":" + (string.IsNullOrEmpty( w.VotesJson ) ? "[]" : w.VotesJson) + "}" );
		var list = RPJson.Arr( arr, "v" );
		if ( list == null ) return 0;
		var me = Connection.Local != null ? Connection.Local.Id.ToString() : "";
		foreach ( var n in list )
		{
			var v = n.AsObject();
			if ( CanAnswer( v, me ) ) return RPJson.Int( v, "id", 0 );
		}
		return 0;
	}

	static bool CanAnswer( System.Text.Json.Nodes.JsonObject v, string me )
	{
		if ( RPJson.Str( v, "target", "" ) == me ) return false;
		var voted = RPJson.StrArray( v, "voted", new string[0] );
		if ( RPUtil.ContainsIgnoreCase( voted, me ) ) return false;
		var voters = RPJson.StrArray( v, "voters", null );
		return voters == null || RPUtil.ContainsIgnoreCase( voters, me );
	}

	void UpdateAgenda( RPWorld w, RPPlayer rp )
	{
		RPAgendaDef def = null;
		foreach ( var a in DarkRP.Registry().Agendas )
			if ( RPUtil.ContainsIgnoreCase( a.Managers, rp.JobCommand ) || RPUtil.ContainsIgnoreCase( a.Listeners, rp.JobCommand ) ) def = a;
		var text = def != null && w.AgendaTexts.ContainsKey( def.Title ) ? w.AgendaTexts[def.Title] : "";
		var key = (def != null ? def.Title : "") + "|" + text;
		if ( key == _agendaKey ) return;
		_agendaKey = key;
		_agenda.DeleteChildren( true );
		_agenda.Style.Display = def != null ? DisplayMode.Flex : DisplayMode.None;
		if ( def == null ) return;
		RPUi.Text( _agenda, def.Title, "title" );
		RPUi.Text( _agenda, string.IsNullOrEmpty( text ) ? "(no agenda set - /agenda <text>)" : text, "text" );
	}

	void UpdateBanner( RPWorld w )
	{
		var key = w.Lockdown + "|" + w.Lottery;
		if ( key == _bannerKey ) return;
		_bannerKey = key;
		_banner.DeleteChildren( true );
		if ( w.Lockdown ) RPUi.Text( _banner, "LOCKDOWN IN PROGRESS - STAY INSIDE", "inner lockdown" );
		if ( !string.IsNullOrEmpty( w.Lottery ) )
		{
			var p = w.Lottery.Split( '|' );
			long price = 0, pot = 0;
			if ( p.Length > 1 ) { long.TryParse( p[0], out price ); long.TryParse( p[1], out pot ); }
			RPUi.Text( _banner, "LOTTERY  ticket " + RPUtil.Money( price ) + "  ·  pot " + RPUtil.Money( pot ), "inner lottery" );
		}
	}

	void UpdateVotes( RPWorld w )
	{
		var key = w.VotesJson;
		if ( key == _voteKey ) return;
		_voteKey = key;
		_votes.DeleteChildren( true );
		var arr = RPJson.Parse( "{\"v\":" + (string.IsNullOrEmpty( key ) ? "[]" : key) + "}" );
		var list = RPJson.Arr( arr, "v" );
		if ( list == null ) return;
		var me = Connection.Local != null ? Connection.Local.Id.ToString() : "";
		bool firstOpen = true;
		foreach ( var n in list )
		{
			var v = n.AsObject();
			var box = RPUi.Div( _votes, "vote glass" );
			RPUi.Text( box, RPJson.Str( v, "q", "" ), "q" );
			var row = RPUi.Div( box, "row" );
			RPUi.Text( row, "Yes " + RPJson.Int( v, "yes", 0 ) + "  ·  No " + RPJson.Int( v, "no", 0 ) + "  ·  " + RPJson.Int( v, "left", 0 ) + "s", "count" );
			if ( CanAnswer( v, me ) )
			{
				RPUi.Text( box, firstOpen ? "Press Y for yes, N for no (or /vote yes)" : "Waiting for the vote above", "keys" );
				firstOpen = false;
			}
		}
	}

	/// <summary>Prop protection: who owns what you look at, in a box on the left middle of the screen like FPP's
	/// classic owner display (green: you can touch it, red: you can't).</summary>
	void UpdateOwner( Player ply )
	{
		string text = "";
		bool denied = false;
		if ( ply != null && ply.Health > 0 && RPPropProtection.Active() )
		{
			var eye = ply.EyeTransform;
			var tr = Scene.Trace.Ray( eye.Position, eye.Position + eye.Forward * 4096 ).IgnoreGameObjectHierarchy( ply.GameObject ).WithoutTags( "trigger" ).Run();
			var root = tr.Hit && tr.GameObject.IsValid() ? tr.GameObject.Root : null;
			if ( root != null && root.Components.Get<Player>() == null && (root.Components.Get<RPEntity>() != null || root.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) != null) )
			{
				var guard = root.Components.Get<RPPropGuard>();
				long owner = guard != null ? guard.OwnerSteamId : RPPropProtection.OwnerOf( root );
				if ( owner == 0 ) text = "World";
				else
				{
					var name = RPPropProtection.OwnerName( owner );
					text = name == "someone who left" ? "Disconnected player" : name;
				}
				denied = !RPPropProtection.CanTouch( Connection.Local, root, "physgun" );
			}
		}
		_owner.Text = text;
		_owner.Style.Display = text != "" ? DisplayMode.Flex : DisplayMode.None;
		_owner.SetClass( "denied", denied );
	}

	void UpdateTarget( RPWorld w )
	{
		var ply = Player.FindLocalPlayer();
		string key = "";
		var lines = new List<string>();
		string title = "";
		Color titleColor = Color.White;
		if ( ply != null && ply.Health > 0 )
		{
			var eye = ply.EyeTransform;
			var tr = Scene.Trace.Ray( eye.Position, eye.Position + eye.Forward * 400 ).IgnoreGameObjectHierarchy( ply.GameObject ).WithoutTags( "trigger" ).UseHitboxes().Run();
			if ( tr.Hit && tr.GameObject.IsValid() )
			{
				var other = tr.GameObject.Components.Get<Player>( FindMode.EverythingInSelfAndAncestors );
				var door = RPDoors.DoorOf( tr.GameObject );
				var ent = tr.GameObject.Components.Get<RPEntity>( FindMode.EverythingInSelfAndAncestors );
				var cfg = DarkRP.Config();
				if ( other != null && other != ply )
				{
					var orp = RP.Of( other.Network.Owner );
					if ( orp != null )
					{
						title = cfg.Bool( "showname" ) ? RP.NameOf( orp ) : "";
						titleColor = RP.JobColor( orp );
						if ( cfg.Bool( "showjob" ) ) lines.Add( RP.JobName( orp ) );
						if ( cfg.Bool( "showhealth" ) ) lines.Add( "Health " + (int)other.Health );
						if ( orp.Wanted ) lines.Add( "WANTED: " + orp.WantedReason );
						if ( orp.License ) lines.Add( "Has a gun license" );
						if ( orp.Arrested ) lines.Add( "Arrested" );
					}
				}
				else if ( door != null && tr.Distance < 220 )
				{
					var d = RPDoors.Get( w, RPDoors.IdOf( door ) );
					title = string.IsNullOrEmpty( d.Title ) ? "Door" : d.Title;
					if ( d.IsOwned )
					{
						lines.Add( "Owned by " + OwnerName( d.Owner ) );
						if ( d.CoOwners.Count > 0 ) lines.Add( "Co-owners: " + string.Join( ", ", d.CoOwners.Select( x => OwnerName( x ) ) ) );
					}
					else if ( !string.IsNullOrEmpty( d.Group ) ) lines.Add( d.Group );
					else if ( d.Teams != null && d.Teams.Length > 0 ) lines.Add( "Jobs: " + string.Join( ", ", d.Teams.Select( x => DarkRP.Registry().Job( x ) != null ? DarkRP.Registry().Job( x ).Name : x ) ) );
					else if ( d.NonOwnable ) lines.Add( "Not ownable" );
					else lines.Add( "For sale: " + RPUtil.Money( cfg.Int( "doorcost" ) ) + " (keys + R)" );
					if ( d.Locked ) lines.Add( "Locked" );
				}
				else if ( ent != null && tr.Distance < 250 && ent.TypeId != "darkrp_door" && ent.TypeId != "darkrp_laws" )
				{
					var label = string.IsNullOrEmpty( ent.Label ) ? ent.TypeId : ent.Label;
					var parts = label.Split( '\n' );
					title = parts[0];
					for ( int i = 1; i < parts.Length; i++ ) lines.Add( parts[i] );
					if ( ent.TypeId == "spawned_shipment" ) lines.Add( ent.Amount + " left · press E" );
					if ( ent.TypeId == "spawned_money" ) lines.Add( "Press E to pick up" );
					if ( !string.IsNullOrEmpty( ent.OwnerName ) && ent.TypeId != "spawned_money" ) lines.Add( "Owner: " + ent.OwnerName );
				}
			}
		}
		key = title + "|" + string.Join( "|", lines ) + "|" + titleColor;
		if ( key == _targetKey ) return;
		_targetKey = key;
		_target.DeleteChildren( true );
		if ( string.IsNullOrEmpty( title ) && lines.Count == 0 ) return;
		var box = RPUi.Div( _target, "box glass" );
		if ( !string.IsNullOrEmpty( title ) ) { var t = RPUi.Text( box, title, "t-title" ); t.Style.FontColor = titleColor; }
		foreach ( var l in lines ) RPUi.Text( box, l, "t-line" );
	}

	static string OwnerName( string steamId )
	{
		foreach ( var rp in RP.All() ) if ( rp.SteamIdText == steamId ) return RP.NameOf( rp );
		return "someone offline";
	}

	void UpdateProgress()
	{
		var h = RPUi.H;
		_progress.Style.Display = string.IsNullOrEmpty( h.ProgressText ) ? DisplayMode.None : DisplayMode.Flex;
		_progressText.Text = h.ProgressText;
		_progressFill.Style.Width = Length.Percent( h.ProgressValue * 100 );
	}
}

/// <summary>DarkRP's chat box (replaces Sandbox's: addon scripts can't intercept the engine chat).</summary>
[Title( "DarkRP Chat" ), Hide]
public sealed class RPChatPanel : PanelComponent
{
	const string Css = @"
.chat { position: absolute; left: 24px; bottom: 400px; width: 520px; flex-direction: column; }
.chat .history { flex-direction: column; max-height: 300px; overflow: hidden; padding: 6px 10px; gap: 2px; justify-content: flex-end; }
.chat.open .history { overflow-y: scroll; background-color: rgba( 14, 16, 20, 0.75 ); border-radius: 8px 8px 0 0; pointer-events: all; }
.chat .line { flex-direction: row; flex-wrap: wrap; font-size: 14px; text-shadow: 1px 1px 2px rgba( 0, 0, 0, 0.9 ); }
.chat .entry-row { flex-direction: row; align-items: center; gap: 8px; padding: 6px 10px; background-color: rgba( 14, 16, 20, 0.9 ); border-radius: 0 0 8px 8px; pointer-events: all; }
.chat .entry-row .who { font-size: 12px; font-weight: 800; color: #9aa4ae; }
.chat .entry { flex-grow: 1; font-size: 14px; color: white; }
";

	RPClickRoot _root;
	Panel _chat, _history, _entryRow;
	RPTextEntry _entry;
	int _builtVersion = -1;
	bool _builtOpen;
	float _nextFadeCheck;

	protected override void OnUpdate()
	{
		var root = Panel;
		if ( root == null ) return;
		if ( _root == null || !_root.IsValid() ) Build( root );
		DisableSandboxChat();
		Place();
		var h = RPUi.H;

		bool justOpened = false;
		if ( !h.ChatOpen && !RPUi.Typing() && h.Dialog == "" && !h.MenuOpen && (Input.Pressed( "Chat" ) || Input.Keyboard.Pressed( "enter" ) || Input.Keyboard.Pressed( "kp_enter" )) )
		{
			h.ChatOpen = true;
			_entry.Text = "";
			_entry.TakeSubmit();
			_entry.TakeCancel();
			justOpened = true;
		}

		if ( h.ChatOpen )
		{
			if ( !_entry.HasFocus ) _entry.Focus();
			// The Enter press that opened the chat must not also send (and close) it in the same frame.
			var submit = _entry.TakeSubmit();
			var cancel = _entry.TakeCancel();
			if ( !justOpened && (submit || Input.Keyboard.Pressed( "enter" ) || Input.Keyboard.Pressed( "return" ) || Input.Keyboard.Pressed( "kp_enter" )) )
			{
				var text = (_entry.Text ?? "").Trim();
				_entry.Text = "";
				h.ChatOpen = false;
				if ( text.Length > 0 )
				{
					var w = RPWorld.Current();
					if ( w != null ) w.Say( text, false );
				}
			}
			else if ( cancel || Input.EscapePressed || Input.Keyboard.Pressed( "escape" ) )
			{
				Input.EscapePressed = false;
				h.ChatOpen = false;
				_entry.Text = "";
			}
		}

		_chat.SetClass( "open", h.ChatOpen );
		_entryRow.Style.Display = h.ChatOpen ? DisplayMode.Flex : DisplayMode.None;
		if ( !h.ChatOpen && _entry.HasFocus ) _entry.Blur();

		bool fade = RealTime.Now >= _nextFadeCheck;
		if ( h.ChatVersion != _builtVersion || h.ChatOpen != _builtOpen || (fade && !h.ChatOpen) )
		{
			_nextFadeCheck = RealTime.Now + 1;
			Rebuild();
		}
	}

	void Build( Panel root )
	{
		root.StyleSheet.Parse( RPStyle.Base );
		root.StyleSheet.Parse( Css );
		_root = root.AddChild<RPClickRoot>();
		_root.AddClass( "rp" );
		_chat = RPUi.Div( _root, "chat" );
		_history = RPUi.Div( _chat, "history" );
		_entryRow = RPUi.Div( _chat, "entry-row" );
		RPUi.Text( _entryRow, "SAY", "who" );
		_entry = _entryRow.AddChild<RPTextEntry>();
		_entry.AddClass( "entry" );
		_entry.MaxLength = 300;
		_builtVersion = -1;
	}

	/// <summary>Above DarkRP's info box (bottom left); the history steps aside with the HUD while Q or C is open.</summary>
	void Place()
	{
		_chat.Style.Bottom = Length.Pixels( 250 );
		_chat.Style.Left = Length.Pixels( 24 );
		_chat.Style.Display = RPHud.MenusOpen() && !RPUi.H.ChatOpen ? DisplayMode.None : DisplayMode.Flex;
	}

	void Rebuild()
	{
		var h = RPUi.H;
		_builtVersion = h.ChatVersion;
		_builtOpen = h.ChatOpen;
		_history.DeleteChildren( true );
		int start = Math.Max( 0, h.ChatLines.Count - (h.ChatOpen ? 60 : 8) );
		for ( int i = start; i < h.ChatLines.Count; i++ )
		{
			if ( !h.ChatOpen && RealTime.Now - h.ChatTimes[i] > 12 ) continue;
			var line = RPUi.Div( _history, "line" );
			RPUi.Segments( line, h.ChatLines[i] );
		}
		if ( h.ChatOpen ) _history.TryScrollToBottom();
	}

	void DisableSandboxChat()
	{
		foreach ( var cb in Scene.GetAllComponents<ChatBox>() ) if ( cb.Enabled ) cb.Enabled = false;
	}
}
