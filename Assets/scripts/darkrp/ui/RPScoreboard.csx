// DarkRP for s&box - the scoreboard (hold the scoreboard key, Tab), in the style of DarkRP's FAdmin scoreboard: players
// grouped by job in the job's colour, RP name, job title, status, kills, deaths, ping. Click a player for actions
// (mute, prop buddy, and for admins: set job, money, arrest, kick). Replaces Sandbox's scoreboard.

[Title( "DarkRP Scoreboard" ), Hide]
public sealed class RPScoreboard : PanelComponent
{
	const string Css = @"
.sb { position: absolute; left: 0; right: 0; top: 0; bottom: 0; justify-content: center; align-items: flex-start; padding-top: 7%; opacity: 0; transition: opacity 0.12s ease; }
.sb.visible { opacity: 1; pointer-events: all; }
.sb .board { flex-direction: column; width: 980px; max-height: 84%; padding: 18px 20px 16px; }
.sb .head { flex-direction: row; align-items: flex-end; justify-content: space-between; margin-bottom: 12px; padding: 0 4px; }
.sb .head .left { flex-direction: column; }
.sb .head .server { font-size: 26px; font-weight: 800; }
.sb .head .sub { font-size: 13px; color: #9aa4ae; }
.sb .head .count { font-size: 14px; font-weight: 700; color: #c5ccd3; }
.sb .cols { flex-direction: row; align-items: center; padding: 6px 12px 6px 18px; font-size: 11px; font-weight: 800; letter-spacing: 1px; color: #8a949e; text-transform: uppercase; border-bottom: 1px solid rgba( 255, 255, 255, 0.08 ); }
.sb .c-name { flex-grow: 1; flex-shrink: 1; width: 300px; }
.sb .c-job { width: 220px; }
.sb .c-stat { width: 64px; justify-content: flex-end; text-align: right; }
.sb .c-mute { width: 40px; justify-content: center; }
.sb .rows { flex-direction: column; overflow-y: scroll; flex-shrink: 1; }
.sb .group { flex-direction: row; align-items: center; gap: 8px; margin-top: 10px; padding: 4px 12px; border-radius: 6px; font-size: 13px; font-weight: 800; color: #fff; text-shadow: 0 1px 2px rgba( 0, 0, 0, 0.6 ); }
.sb .group .gcount { font-size: 12px; font-weight: 700; color: rgba( 255, 255, 255, 0.8 ); }
.sb .row { flex-direction: row; align-items: center; min-height: 44px; margin-top: 3px; padding: 4px 12px 4px 0; border-radius: 6px; background-color: rgba( 255, 255, 255, 0.04 ); cursor: pointer; }
.sb .row:hover { background-color: rgba( 255, 255, 255, 0.1 ); }
.sb .row.me { background-color: rgba( 255, 255, 255, 0.09 ); }
.sb .row.dead { opacity: 0.55; }
.sb .row .stripe { width: 5px; height: 100%; min-height: 36px; border-radius: 3px; margin-right: 10px; }
.sb .row .avatar { width: 32px; height: 32px; border-radius: 5px; margin-right: 10px; background-size: cover; }
.sb .row .names { flex-direction: column; }
.sb .row .rpname { font-size: 15px; font-weight: 700; }
.sb .row .steam { font-size: 11px; color: #8a949e; }
.sb .row .badges { flex-direction: row; gap: 4px; margin-left: 8px; }
.sb .badge { font-size: 10px; font-weight: 800; letter-spacing: 0.5px; padding: 1px 6px; border-radius: 8px; background-color: rgba( 255, 255, 255, 0.12 ); }
.sb .badge.admin { background-color: #8e44ad; }
.sb .badge.wanted { background-color: #c0392b; }
.sb .badge.arrested { background-color: #6c3483; }
.sb .badge.license { background-color: #1f7a4a; }
.sb .badge.dead { background-color: #555; }
.sb .row .job { font-size: 13px; font-weight: 700; }
.sb .row .stat { font-size: 14px; font-weight: 600; color: #dfe4e8; }
.sb .row .mute { font-family: Material Icons; font-size: 20px; color: #9aa4ae; }
.sb .row .mute.muted { color: #e74c3c; }
.sb .row .mute:hover { color: #fff; }
.sb .foot { margin-top: 10px; font-size: 11px; color: #7f8992; justify-content: center; }
";

	RPClickRoot _root;
	Panel _sb, _rows;
	Label _server, _sub, _count;
	string _key;
	float _nextKey;
	bool _shown;
	/// <summary>The open player menu (closed with the scoreboard).</summary>
	public Panel Menu;

	protected override void OnUpdate()
	{
		var root = Panel;
		if ( root == null ) return;
		if ( _root == null || !_root.IsValid() ) Build( root );
		foreach ( var sb in Scene.GetAllComponents<Scoreboard>() ) if ( sb.Enabled ) sb.Enabled = false;

		var show = (Input.Down( "Score" ) || RPUi.H.ScoreForced) && !RPHud.MenusOpen() && !RPUi.H.ChatOpen;
		if ( !show && _shown && Menu != null && Menu.IsValid() ) { Menu.Delete(); Menu = null; }
		_shown = show;
		_sb.SetClass( "visible", show );
		if ( !show ) return;
		if ( RealTime.Now < _nextKey ) return;
		_nextKey = RealTime.Now + 0.5f;
		var key = Key();
		if ( key == _key ) return;
		_key = key;
		Rebuild();
	}

	void Build( Panel root )
	{
		root.StyleSheet.Parse( RPStyle.Base );
		root.StyleSheet.Parse( Css );
		_root = root.AddChild<RPClickRoot>();
		_root.AddClass( "rp" );
		_sb = RPUi.Div( _root, "sb" );
		var board = RPUi.Div( _sb, "board glass" );
		var head = RPUi.Div( board, "head" );
		var left = RPUi.Div( head, "left" );
		_server = RPUi.Text( left, "", "server" );
		_sub = RPUi.Text( left, "", "sub" );
		_count = RPUi.Text( head, "", "count" );
		var cols = RPUi.Div( board, "cols" );
		RPUi.Text( cols, "Name", "c-name" );
		RPUi.Text( cols, "Job", "c-job" );
		RPUi.Text( cols, "Kills", "c-stat" );
		RPUi.Text( cols, "Deaths", "c-stat" );
		RPUi.Text( cols, "Ping", "c-stat" );
		RPUi.Text( cols, "", "c-mute" );
		_rows = RPUi.Div( board, "rows" );
		RPUi.Text( board, "Click a player for more", "foot" );
		_key = null;
	}

	static string Key()
	{
		var sb = new System.Text.StringBuilder();
		foreach ( var c in Connection.All )
		{
			var rp = RP.Of( c );
			var pd = PlayerData.For( c );
			sb.Append( c.Id ).Append( c.DisplayName ).Append( (int)(c.Ping / 5) );
			if ( rp != null ) sb.Append( rp.JobCommand ).Append( rp.JobTitle ).Append( rp.RPName ).Append( rp.Wanted ).Append( rp.Arrested ).Append( rp.License );
			if ( pd != null ) sb.Append( pd.Kills ).Append( '/' ).Append( pd.Deaths );
			sb.Append( Player.FindForConnection( c ) != null ).Append( SandboxVoice.IsMuted( c.SteamId ) ).Append( '|' );
		}
		var reg = DarkRP.Registry();
		sb.Append( reg != null ? reg.Version : 0 );
		return sb.ToString();
	}

	void Rebuild()
	{
		_server.Text = string.IsNullOrEmpty( Networking.ServerName ) ? "DarkRP" : Networking.ServerName;
		var map = Networking.MapName;
		_sub.Text = "DarkRP for s&box" + (string.IsNullOrEmpty( map ) ? "" : "  ·  " + map);
		var all = Connection.All.ToList();
		_count.Text = all.Count + (all.Count == 1 ? " player" : " players") + (Networking.MaxPlayers > 0 ? " / " + Networking.MaxPlayers : "");

		_rows.DeleteChildren( true );
		var reg = DarkRP.Registry();
		var jobs = reg != null ? reg.Jobs : new List<RPJob>();
		// DarkRP sorts the scoreboard by team: groups in job order, then players by name.
		var byJob = new Dictionary<string, List<Connection>>();
		var order = new List<string>();
		foreach ( var c in all )
		{
			var rp = RP.Of( c );
			var cmd = rp != null ? rp.JobCommand ?? "" : "";
			if ( !byJob.ContainsKey( cmd ) ) { byJob[cmd] = new List<Connection>(); order.Add( cmd ); }
			byJob[cmd].Add( c );
		}
		order = order.OrderBy( cmd => { var i = jobs.FindIndex( j => j.Command == cmd ); return i < 0 ? 9999 : i; } ).ToList();

		foreach ( var cmd in order )
		{
			var job = reg != null ? reg.Job( cmd ) : null;
			var color = job != null ? RPScoreboard.ParseColor( job.Color ) : Color.White;
			var group = RPUi.Div( _rows, "group" );
			group.Style.BackgroundColor = color.WithAlpha( 0.75f );
			RPUi.Text( group, job != null ? job.Name : "Connecting", "" );
			var list = byJob[cmd];
			string count = list.Count.ToString();
			if ( job != null && job.Max >= 1 ) count += " / " + (int)job.Max;
			RPUi.Text( group, count, "gcount" );
			foreach ( var c in list.OrderBy( x => RP.Name( x ) ) ) Row( c, job, color );
		}
	}

	/// <summary>Dark job colours (DarkRP's police blue) made light enough to read on the dark board.</summary>
	public static Color Readable( Color c )
	{
		var lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
		return lum < 0.55f ? Color.Lerp( c, Color.White, 0.55f - lum + 0.25f ) : c;
	}

	public static Color ParseColor( string hex )
	{
		var c = Color.Parse( hex ?? "" );
		return c.HasValue ? c.Value : Color.White;
	}

	void Row( Connection c, RPJob job, Color color )
	{
		var rp = RP.Of( c );
		var pd = PlayerData.For( c );
		var row = new RPScoreRow();
		row.Conn = c;
		row.Board = this;
		row.AddClass( "row" );
		_rows.AddChild( row );
		row.SetClass( "me", c == Connection.Local );
		var alive = Player.FindForConnection( c ) != null;
		row.SetClass( "dead", !alive );

		var stripe = RPUi.Div( row, "stripe" );
		stripe.Style.BackgroundColor = color;
		var nameCol = RPUi.Div( row, "c-name" );
		nameCol.Style.FlexDirection = FlexDirection.Row;
		nameCol.Style.AlignItems = Align.Center;
		var img = new Image();
		img.AddClass( "avatar" );
		img.SetTexture( "avatar:" + c.SteamId );
		nameCol.AddChild( img );
		var names = RPUi.Div( nameCol, "names" );
		var rpName = rp != null ? RP.NameOf( rp ) : c.DisplayName;
		RPUi.Text( names, rpName, "rpname" );
		if ( rpName != c.DisplayName ) RPUi.Text( names, c.DisplayName, "steam" );
		var badges = RPUi.Div( nameCol, "badges" );
		if ( RP.IsAdmin( c ) ) RPUi.Text( badges, "ADMIN", "badge admin" );
		if ( rp != null && rp.Arrested ) RPUi.Text( badges, "ARRESTED", "badge arrested" );
		else if ( rp != null && rp.Wanted ) RPUi.Text( badges, "WANTED", "badge wanted" );
		if ( rp != null && rp.License ) RPUi.Text( badges, "LICENSE", "badge license" );
		if ( !alive ) RPUi.Text( badges, "DEAD", "badge dead" );

		var jobLabel = RPUi.Text( row, rp != null ? RP.JobName( rp ) : "", "c-job job" );
		jobLabel.Style.FontColor = RPScoreboard.Readable( color );
		RPUi.Text( row, (pd != null ? pd.Kills : 0).ToString(), "c-stat stat" );
		RPUi.Text( row, (pd != null ? pd.Deaths : 0).ToString(), "c-stat stat" );
		RPUi.Text( row, ((int)Math.Ceiling( c.Ping )).ToString(), "c-stat stat" );
		var muteCol = RPUi.Div( row, "c-mute" );
		if ( c != Connection.Local )
		{
			var muted = SandboxVoice.IsMuted( c.SteamId );
			var m = RPUi.Text( muteCol, muted ? "volume_off" : "volume_up", "mute" );
			m.SetClass( "muted", muted );
			row.MuteButton = m;
		}
	}
}

/// <summary>A scoreboard row: click it for a menu of actions on that player.</summary>
public sealed class RPScoreRow : Panel
{
	public Connection Conn;
	public RPScoreboard Board;
	public Panel MuteButton;

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );
		if ( Conn == null ) return;
		e.StopPropagation();
		var t = e.Target;
		while ( t != null && t != this )
		{
			if ( t == MuteButton ) { SandboxVoice.Mute( Conn.SteamId ); return; }
			t = t.Parent;
		}
		OpenMenu();
	}

	protected override void OnRightClick( MousePanelEvent e )
	{
		base.OnRightClick( e );
		if ( Conn == null ) return;
		e.StopPropagation();
		OpenMenu();
	}

	void OpenMenu()
	{
		var c = Conn;
		var id = c.SteamId.ToString();
		var name = RP.Name( c );
		var menu = new Sandbox.UI.Menu();
		// Clipboard isn't available to addon scripts: the SteamID goes to the console instead.
		menu.AddOption( "Show SteamID", "badge", () => { Log.Info( name + ": " + id ); RPUi.Notify( 0, 6, name + "'s SteamID: " + id + " (also in the console)" ); } );
		if ( c != Connection.Local )
		{
			var muted = SandboxVoice.IsMuted( c.SteamId );
			menu.AddOption( muted ? "Unmute" : "Mute", muted ? "volume_up" : "volume_off", () => SandboxVoice.Mute( c.SteamId ) );
			if ( !DarkRP.ModuleDisabled( "propprotection" ) )
			{
				var buddy = RPPropProtection.IsBuddy( RPPropProtection.SteamOf( Connection.Local ), RPPropProtection.SteamOf( c ) );
				menu.AddOption( buddy ? "Remove prop buddy" : "Make prop buddy", buddy ? "person_remove" : "person_add", () => RPPropProtectionPage.Send( "buddy", id, "" ) );
			}
		}

		if ( RP.IsAdmin( Connection.Local ) )
		{
			menu.AddSeparator();
			var jobs = menu.AddMenu( "Set job", "work" );
			var reg = DarkRP.Registry();
			if ( reg != null ) foreach ( var j in reg.Jobs ) { var cmd = j.Command; jobs.AddOption( j.Name, "", () => RPUi.Command( "/setjob " + id + " " + cmd ) ); }
			menu.AddOption( "Set money", "payments", () => RPUi.Prompt( "Set the wallet of " + name, "/setmoney " + id, "Amount" ) );
			menu.AddOption( "Give money", "add_card", () => RPUi.Prompt( "Give money to " + name, "/addmoney " + id, "Amount (can be negative)" ) );
			menu.AddOption( "Set RP name", "badge", () => RPUi.Prompt( "RP name of " + name, "/setname " + id, "Name" ) );
			var rp = RP.Of( c );
			if ( rp != null && rp.Arrested ) menu.AddOption( "Unarrest", "lock_open", () => RPUi.Command( "/unarrest " + id ) );
			else menu.AddOption( "Arrest", "lock", () => RPUi.Command( "/arrest " + id ) );
			if ( c != Connection.Local ) menu.AddOption( "Kick", "person_remove", () => UIModal.Prompt( this, "Kick " + name, "", reason => GameManager.RpcKickPlayer( c, string.IsNullOrWhiteSpace( reason ) ? "Kicked" : reason ), "Reason", "Kick", "check", null ) );
		}
		menu.Open( this, Popup.PositionMode.UnderMouse );
		if ( Board != null ) Board.Menu = menu;
	}
}
