// DarkRP for s&box - client UI plumbing: creates the HUD, chat, menu and dialogs for each scene, routes clicks and
// host messages to them. Panels are built in code (no Razor in addons) and clicks go through one OnClick override
// per root plus a table, never stored lambdas (those break when scripts hotload).
using Sandbox.UI;

public sealed class RPUiHolder
{
	public Scene Scene;
	public GameObject Root;
	public bool MenuOpen;
	public string MenuTab = "jobs";
	public string MenuSelected = "";
	public bool ChatOpen;
	/// <summary>Tests: show the scoreboard without holding the key.</summary>
	public bool ScoreForced;
	public bool ChatTeam;
	public string Dialog = "";
	public string DialogData = "";
	public string DoorId = "";
	public string PromptTitle = "";
	public string PromptCommand = "";
	public string PromptHint = "";
	public string ProgressText = "";
	public float ProgressValue;
	public float LockpickDuration;
	public readonly List<string> ChatLines = new List<string>();
	public readonly List<float> ChatTimes = new List<float>();
	public int ChatVersion;
	public readonly List<string> AdminEdits = new List<string>();
}

public sealed class RPUi
{
	public static readonly RPUiHolder H = new RPUiHolder();

	public static bool Typing() => H.ChatOpen || H.Dialog == "prompt" || H.Dialog == "tipjar";
	public static float LockpickDuration() => H.LockpickDuration;

	/// <summary>Creates the UI objects for this scene once (never on a dedicated server).</summary>
	public static void Ensure( Scene scene )
	{
		if ( scene == null || scene.IsEditor ) return;
		if ( Connection.Local != null && Connection.Local.IsDedicatedServer ) return;
		if ( H.Scene == scene && H.Root.IsValid() ) return;
		H.Scene = scene;
		H.ChatLines.Clear();
		H.ChatTimes.Clear();
		H.ChatVersion++;
		H.MenuOpen = false;
		H.ChatOpen = false;
		H.Dialog = "";
		var go = new GameObject( true, "darkrp_ui" );
		go.Flags = GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
		H.Root = go;
		Layer( go, "darkrp_hud", 90, "RPHud" );
		Layer( go, "darkrp_chat", 95, "RPChatPanel" );
		Layer( go, "darkrp_scoreboard", 100, "RPScoreboard" );
		Layer( go, "darkrp_menu", 110, "RPMenuPanel" );
		Layer( go, "darkrp_dialog", 120, "RPDialogPanel" );
	}

	static void Layer( GameObject parent, string name, int z, string component )
	{
		var go = new GameObject( true, name );
		go.Flags = GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
		go.SetParent( parent, false );
		var sp = go.AddComponent<ScreenPanel>();
		sp.ZIndex = z;
		go.Components.Create( TypeLibrary.GetType( component ) );
	}

	// ---- host messages ----

	public static void Notify( int type, float seconds, string message )
	{
		string icon = "info";
		var color = new Color( 0.3f, 0.57f, 0.99f );
		if ( type == 1 ) { icon = "error"; color = new Color( 0.95f, 0.3f, 0.3f ); }
		else if ( type == 2 ) { icon = "undo"; color = new Color( 0.9f, 0.7f, 0.25f ); }
		else if ( type == 3 ) { icon = "lightbulb"; color = new Color( 1f, 0.85f, 0.3f ); }
		else if ( type == 4 ) { icon = "payments"; color = new Color( 0.35f, 0.85f, 0.4f ); }
		try { Notices.AddNotice( icon, color, message, Math.Max( 2, seconds ) ); }
		catch { ChatLine( RPNet.Seg( "#ffd27f", message ) ); }
	}

	public static void ChatLine( string segments )
	{
		H.ChatLines.Add( segments );
		H.ChatTimes.Add( RealTime.Now );
		while ( H.ChatLines.Count > 80 ) { H.ChatLines.RemoveAt( 0 ); H.ChatTimes.RemoveAt( 0 ); }
		H.ChatVersion++;
	}

	public static void Show( string panel, string data )
	{
		if ( panel == "lockpick" ) { H.LockpickDuration = RPUtil.ParseFloat( data, 15 ); return; }
		H.Dialog = panel;
		H.DialogData = data ?? "";
	}

	public static void Progress( string text, float value )
	{
		H.ProgressText = text ?? "";
		H.ProgressValue = Math.Clamp( value, 0, 1 );
	}

	public static void OpenDoorMenu( Sandbox.Mapping.Door door )
	{
		H.DoorId = RPDoors.IdOf( door );
		H.Dialog = "door";
	}

	public static void OpenPocket() { H.Dialog = "pocket"; }

	public static void Prompt( string title, string command, string hint )
	{
		H.PromptTitle = title;
		H.PromptCommand = command;
		H.PromptHint = hint ?? "";
		H.Dialog = "prompt";
		H.MenuOpen = false;
	}

	public static void CloseDialog() { H.Dialog = ""; H.DialogData = ""; }

	public static void Command( string text )
	{
		var w = RPWorld.Current();
		if ( w != null ) w.RunCommand( text );
	}

	/// <summary>Every button: "kind:value" (see RPUi.Click).</summary>
	public static void Click( string action )
	{
		if ( string.IsNullOrEmpty( action ) ) return;
		var colon = action.IndexOf( ':' );
		var kind = colon < 0 ? action : action.Substring( 0, colon );
		var value = colon < 0 ? "" : action.Substring( colon + 1 );
		var w = RPWorld.Current();
		switch ( kind )
		{
			case "tab": H.MenuTab = value; H.MenuSelected = ""; break;
			case "select": H.MenuSelected = value; break;
			case "close": H.MenuOpen = false; CloseDialog(); break;
			case "job": Command( "/" + value ); H.MenuOpen = false; break;
			case "buy":
				{
					var p = value.IndexOf( '|' );
					if ( w != null && p > 0 ) w.Buy( value.Substring( 0, p ), value.Substring( p + 1 ), 1 );
					break;
				}
			case "cmd": Command( value ); break;
			case "prompt":
				{
					var p = value.Split( '|' );
					Prompt( p[0], p.Length > 1 ? p[1] : "", p.Length > 2 ? p[2] : "" );
					break;
				}
			case "door":
				{
					var p = value.IndexOf( '|' );
					var act = p < 0 ? value : value.Substring( 0, p );
					var arg = p < 0 ? "" : value.Substring( p + 1 );
					if ( act == "title" || act == "addowner" || act == "removeowner" || act == "teams" || act == "group" )
					{
						if ( arg == "" ) { Prompt( act == "title" ? "Door title" : act == "addowner" ? "Add co-owner (player name)" : act == "removeowner" ? "Remove co-owner (player name)" : act == "teams" ? "Jobs that may use this door (commands, comma separated)" : "Door group name", "door:" + act, "" ); break; }
					}
					if ( w != null ) w.DoorAction( H.DoorId, act, arg );
					if ( act == "buy" || act == "sell" ) CloseDialog();
					break;
				}
			case "pocket": if ( w != null ) w.PocketAction( "drop", (int)RPUtil.ParseFloat( value, 0 ) ); break;
			case "vote":
				{
					var p = value.Split( '|' );
					if ( w != null && p.Length == 2 ) w.CastVote( (int)RPUtil.ParseFloat( p[0], 0 ), p[1] == "1" );
					break;
				}
			case "config": AdminEdit( value ); break;
		}
	}

	/// <summary>Admin tab: toggles a bool setting or asks for a new value.</summary>
	static void AdminEdit( string key )
	{
		var cfg = DarkRP.Config();
		if ( cfg == null || !cfg.Has( key ) ) return;
		var s = cfg.Settings[key];
		var w = RPWorld.Current();
		if ( s.Kind == "bool" ) { if ( w != null ) w.AdminAction( "config", key, cfg.Bool( key ) ? "false" : "true" ); return; }
		Prompt( key + " (" + s.Help + ")", "config:" + key, "Current: " + s.Value );
	}

	/// <summary>A prompt was confirmed with this text.</summary>
	public static void Submit( string text )
	{
		var cmd = H.PromptCommand ?? "";
		var w = RPWorld.Current();
		CloseDialog();
		if ( cmd.StartsWith( "door:" ) ) { if ( w != null ) w.DoorAction( H.DoorId, cmd.Substring( 5 ), text ); H.Dialog = "door"; return; }
		if ( cmd.StartsWith( "config:" ) ) { if ( w != null ) w.AdminAction( "config", cmd.Substring( 7 ), text ); H.MenuOpen = true; return; }
		if ( cmd.StartsWith( "tip:" ) ) { if ( w != null ) w.EntityAction( Guid.Parse( cmd.Substring( 4 ) ), "tip", text ); return; }
		if ( string.IsNullOrEmpty( cmd ) ) return;
		Command( cmd + " " + text );
	}

	// ---- small panel helpers ----

	public static Panel Div( Panel parent, string cls )
	{
		var p = parent.AddChild<Panel>();
		if ( !string.IsNullOrEmpty( cls ) ) p.AddClass( cls );
		return p;
	}

	public static Label Text( Panel parent, string text, string cls )
	{
		var l = parent.AddChild<Label>();
		l.Text = text ?? "";
		if ( !string.IsNullOrEmpty( cls ) ) l.AddClass( cls );
		return l;
	}

	public static Label Icon( Panel parent, string icon, string cls )
	{
		var l = parent.AddChild<Label>();
		l.Text = icon;
		l.AddClass( "icon" );
		if ( !string.IsNullOrEmpty( cls ) ) l.AddClass( cls );
		return l;
	}

	/// <summary>A clickable panel registered in its root's click table.</summary>
	public static Panel Button( RPClickRoot root, Panel parent, string text, string cls, string action )
	{
		var b = Div( parent, "btn" );
		if ( !string.IsNullOrEmpty( cls ) ) b.AddClass( cls );
		if ( text != null ) Text( b, text, "btn-text" );
		root.Clicks[b] = action;
		return b;
	}

	/// <summary>"#rrggbb\u0001text\u0002..." chat segments → labels.</summary>
	public static void Segments( Panel parent, string segments )
	{
		foreach ( var seg in (segments ?? "").Split( '\u0002' ) )
		{
			var p = seg.IndexOf( '\u0001' );
			var color = p > 0 ? seg.Substring( 0, p ) : "#ffffff";
			var text = p >= 0 ? seg.Substring( p + 1 ) : seg;
			var l = Text( parent, text, "seg" );
			l.Style.FontColor = RPUtil.ParseColor( color );
		}
	}

	public static string PlainText( string segments )
	{
		var sb = new System.Text.StringBuilder();
		foreach ( var seg in (segments ?? "").Split( '\u0002' ) )
		{
			var p = seg.IndexOf( '\u0001' );
			sb.Append( p >= 0 ? seg.Substring( p + 1 ) : seg );
		}
		return sb.ToString();
	}
}

/// <summary>A root panel with a click table: every DarkRP UI layer builds inside one.</summary>
public sealed class RPClickRoot : Panel
{
	public readonly Dictionary<Panel, string> Clicks = new Dictionary<Panel, string>();

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );
		var p = e.Target;
		while ( p != null && p != this )
		{
			if ( Clicks.ContainsKey( p ) )
			{
				var action = Clicks[p];
				e.StopPropagation();
				RPUi.Click( action );
				return;
			}
			p = p.Parent;
		}
	}

	public void Reset()
	{
		Clicks.Clear();
		DeleteChildren( true );
	}
}

public static class RPStyle
{
	public const string Base = @"
.rp { position: absolute; left: 0; top: 0; width: 100%; height: 100%; pointer-events: none; font-family: Poppins; color: #eef1f4; }
.rp .icon { font-family: Material Icons; }
.glass { background-color: rgba( 14, 16, 20, 0.86 ); backdrop-filter: blur( 12px ); border: 1px solid rgba( 255, 255, 255, 0.07 ); border-radius: 10px; box-shadow: 0 10px 30px rgba( 0, 0, 0, 0.45 ); }
.btn { flex-direction: row; align-items: center; justify-content: center; gap: 6px; padding: 7px 14px; border-radius: 7px; background-color: rgba( 255, 255, 255, 0.07 ); cursor: pointer; flex-shrink: 0; }
.btn:hover { background-color: rgba( 255, 255, 255, 0.14 ); }
.btn .btn-text { font-size: 13px; font-weight: 600; white-space: nowrap; }
.btn.primary { background-color: #2f9e57; }
.btn.primary:hover { background-color: #3cba68; }
.btn.danger { background-color: #b33a3a; }
.btn.danger:hover { background-color: #cf4a4a; }
.btn.disabled { opacity: 0.45; }
.muted { color: #9aa4ae; }
.money { color: #6fe08a; }
.seg { white-space: pre; }
";
}

/// <summary>A text field that reports Enter and Escape itself: while a text field has focus the game doesn't see those
/// keys (Sandbox's chat box does the same).</summary>
public sealed class RPTextEntry : TextEntry
{
	public bool Submitted;
	public bool Cancelled;

	public override void OnButtonTyped( ButtonEvent e )
	{
		var b = e.Button;
		if ( b == "enter" || b == "pad_enter" || b == "kp_enter" || b == "return" )
		{
			e.StopPropagation = true;
			Submitted = true;
			return;
		}
		if ( b == "escape" )
		{
			e.StopPropagation = true;
			Cancelled = true;
			return;
		}
		base.OnButtonTyped( e );
	}

	/// <summary>True once per Enter press.</summary>
	public bool TakeSubmit() { var s = Submitted; Submitted = false; return s; }
	public bool TakeCancel() { var c = Cancelled; Cancelled = false; return c; }
}
