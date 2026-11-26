// DarkRP for s&box - small helpers (culture-independent numbers, colours, JSON, player lookup).
using System.Text.Json.Nodes;

public static class RPUtil
{
	/// <summary>Sandbox's game manager (respawning).</summary>
	public static GameManager GM() => Game.ActiveScene != null ? Game.ActiveScene.GetSystem<GameManager>() : null;

	/// <summary>Parses "1.5" (or "1,5") without depending on the machine's culture.</summary>
	public static float ParseOrNaN( string s )
	{
		if ( string.IsNullOrWhiteSpace( s ) ) return float.NaN;
		s = s.Trim().Replace( ',', '.' );
		bool neg = false;
		int i = 0;
		if ( s[0] == '-' ) { neg = true; i = 1; }
		else if ( s[0] == '+' ) i = 1;
		double whole = 0, frac = 0, scale = 1;
		bool any = false, dot = false;
		for ( ; i < s.Length; i++ )
		{
			char ch = s[i];
			if ( ch == '.' )
			{
				if ( dot ) return float.NaN;
				dot = true;
				continue;
			}
			if ( ch < '0' || ch > '9' ) return float.NaN;
			any = true;
			if ( dot ) { scale *= 10; frac += (ch - '0') / scale; }
			else whole = whole * 10 + (ch - '0');
		}
		if ( !any ) return float.NaN;
		return (float)((whole + frac) * (neg ? -1 : 1));
	}

	public static float ParseFloat( string s, float fallback )
	{
		var v = ParseOrNaN( s );
		return float.IsNaN( v ) ? fallback : v;
	}

	public static bool IsNumber( string s ) => !float.IsNaN( ParseOrNaN( s ) );

	/// <summary>"1.5", "2", "-0.25" - always with a dot.</summary>
	public static string FloatStr( float f )
	{
		var neg = f < 0;
		long scaled = (long)Math.Round( Math.Abs( (double)f ) * 1000.0 );
		long whole = scaled / 1000;
		long frac = scaled % 1000;
		var s = whole.ToString();
		if ( frac != 0 )
		{
			var fs = frac.ToString().PadLeft( 3, '0' ).TrimEnd( '0' );
			s += "." + fs;
		}
		return neg && scaled != 0 ? "-" + s : s;
	}

	/// <summary>A colour from "#rrggbb", "#rgb" or "r,g,b" (0-255).</summary>
	public static Color ParseColor( string s )
	{
		if ( string.IsNullOrWhiteSpace( s ) ) return Color.White;
		s = s.Trim();
		if ( s.Contains( "," ) )
		{
			var p = s.Split( ',' );
			if ( p.Length >= 3 )
				return Rgb( (int)ParseFloat( p[0], 255 ), (int)ParseFloat( p[1], 255 ), (int)ParseFloat( p[2], 255 ) );
		}
		var c = Color.Parse( s.StartsWith( "#" ) ? s : "#" + s );
		return c.HasValue ? c.Value : Color.White;
	}

	public static Color Rgb( int r, int g, int b ) => new Color( r / 255f, g / 255f, b / 255f, 1f );

	public static string Hex( Color c )
	{
		int r = Math.Clamp( (int)Math.Round( c.r * 255 ), 0, 255 ), g = Math.Clamp( (int)Math.Round( c.g * 255 ), 0, 255 ), b = Math.Clamp( (int)Math.Round( c.b * 255 ), 0, 255 );
		return "#" + r.ToString( "x2" ) + g.ToString( "x2" ) + b.ToString( "x2" );
	}

	/// <summary>Money as text with the configured currency: $1,250.</summary>
	public static string Money( long amount )
	{
		var cfg = DarkRP.Config();
		var sep = cfg != null ? cfg.Str( "currencyThousandSeparator" ) : ",";
		var cur = cfg != null ? cfg.Str( "currency" ) : "$";
		var left = cfg == null || cfg.Bool( "currencyLeft" );
		var neg = amount < 0;
		var digits = Math.Abs( amount ).ToString();
		var sb = new System.Text.StringBuilder();
		for ( int i = 0; i < digits.Length; i++ )
		{
			if ( i > 0 && (digits.Length - i) % 3 == 0 ) sb.Append( sep );
			sb.Append( digits[i] );
		}
		var num = sb.ToString();
		var res = left ? cur + num : num + cur;
		return neg ? "-" + res : res;
	}

	/// <summary>"2:05" style durations.</summary>
	public static string Duration( float seconds )
	{
		if ( seconds < 0 ) seconds = 0;
		int s = (int)Math.Ceiling( seconds );
		int m = s / 60;
		s %= 60;
		return m + ":" + s.ToString().PadLeft( 2, '0' );
	}

	public static bool ContainsIgnoreCase( string[] list, string value )
	{
		if ( list == null || value == null ) return false;
		for ( int i = 0; i < list.Length; i++ )
			if ( string.Equals( list[i], value, StringComparison.OrdinalIgnoreCase ) ) return true;
		return false;
	}

	/// <summary>Splits chat arguments, keeping "quoted words" together.</summary>
	public static string[] SplitArgs( string text )
	{
		var list = new List<string>();
		if ( string.IsNullOrEmpty( text ) ) return list.ToArray();
		var cur = new System.Text.StringBuilder();
		bool quoted = false;
		for ( int i = 0; i < text.Length; i++ )
		{
			char ch = text[i];
			if ( ch == '"' ) { quoted = !quoted; continue; }
			if ( ch == ' ' && !quoted )
			{
				if ( cur.Length > 0 ) { list.Add( cur.ToString() ); cur.Clear(); }
				continue;
			}
			cur.Append( ch );
		}
		if ( cur.Length > 0 ) list.Add( cur.ToString() );
		return list.ToArray();
	}

	/// <summary>The current map's name (for per-map data), or the scene name.</summary>
	public static string MapName( Scene scene )
	{
		if ( scene == null ) return "unknown";
		foreach ( var map in scene.GetAllComponents<MapInstance>() )
		{
			if ( !string.IsNullOrEmpty( map.MapName ) ) return Safe( map.MapName );
		}
		var name = scene.Name;
		if ( string.IsNullOrEmpty( name ) ) name = "scene";
		return Safe( name );
	}

	/// <summary>Keeps letters, digits, dot, dash and underscore (safe for file names).</summary>
	public static string Safe( string s )
	{
		var sb = new System.Text.StringBuilder();
		foreach ( var ch in (s ?? "").ToLowerInvariant() )
		{
			if ( (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_' || ch == '.' ) sb.Append( ch );
			else sb.Append( '_' );
		}
		return sb.Length == 0 ? "_" : sb.ToString();
	}
}

/// <summary>Writes JSON text (strings escaped, numbers culture-independent).</summary>
public sealed class RPJsonWriter
{
	readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder();
	readonly List<bool> _first = new List<bool>();
	int _indent;
	public bool Pretty = true;

	void Comma()
	{
		if ( _first.Count == 0 ) return;
		var last = _first.Count - 1;
		if ( !_first[last] ) _sb.Append( ',' );
		_first[last] = false;
		if ( Pretty ) { _sb.Append( '\n' ); _sb.Append( new string( '\t', _indent ) ); }
	}

	void Key( string key )
	{
		Comma();
		if ( key != null )
		{
			_sb.Append( Quote( key ) );
			_sb.Append( Pretty ? ": " : ":" );
		}
	}

	public RPJsonWriter BeginObject( string key = null )
	{
		Key( key );
		_sb.Append( '{' );
		_first.Add( true );
		_indent++;
		return this;
	}

	public RPJsonWriter EndObject()
	{
		_indent--;
		var empty = _first[_first.Count - 1];
		_first.RemoveAt( _first.Count - 1 );
		if ( Pretty && !empty ) { _sb.Append( '\n' ); _sb.Append( new string( '\t', _indent ) ); }
		_sb.Append( '}' );
		return this;
	}

	public RPJsonWriter BeginArray( string key = null )
	{
		Key( key );
		_sb.Append( '[' );
		_first.Add( true );
		_indent++;
		return this;
	}

	public RPJsonWriter EndArray()
	{
		_indent--;
		var empty = _first[_first.Count - 1];
		_first.RemoveAt( _first.Count - 1 );
		if ( Pretty && !empty ) { _sb.Append( '\n' ); _sb.Append( new string( '\t', _indent ) ); }
		_sb.Append( ']' );
		return this;
	}

	public RPJsonWriter Str( string key, string value ) { Key( key ); _sb.Append( value == null ? "null" : Quote( value ) ); return this; }
	public RPJsonWriter Num( string key, long value ) { Key( key ); _sb.Append( value.ToString() ); return this; }
	public RPJsonWriter Flt( string key, float value ) { Key( key ); _sb.Append( RPUtil.FloatStr( value ) ); return this; }
	public RPJsonWriter Bool( string key, bool value ) { Key( key ); _sb.Append( value ? "true" : "false" ); return this; }
	public RPJsonWriter Raw( string key, string json ) { Key( key ); _sb.Append( json ); return this; }

	public RPJsonWriter StrArray( string key, string[] values )
	{
		if ( values == null ) { Key( key ); _sb.Append( "null" ); return this; }
		var pretty = Pretty;
		BeginArray( key );
		Pretty = false;
		for ( int i = 0; i < values.Length; i++ )
		{
			if ( i > 0 ) _sb.Append( ", " );
			_first[_first.Count - 1] = false;
			_sb.Append( Quote( values[i] ?? "" ) );
		}
		_indent--;
		_first.RemoveAt( _first.Count - 1 );
		_sb.Append( ']' );
		Pretty = pretty;
		return this;
	}

	public override string ToString() => _sb.ToString();

	public static string Quote( string s )
	{
		var sb = new System.Text.StringBuilder( "\"" );
		foreach ( var ch in s )
		{
			if ( ch == '"' ) sb.Append( "\\\"" );
			else if ( ch == '\\' ) sb.Append( "\\\\" );
			else if ( ch == '\n' ) sb.Append( "\\n" );
			else if ( ch == '\r' ) sb.Append( "\\r" );
			else if ( ch == '\t' ) sb.Append( "\\t" );
			else if ( ch < ' ' ) sb.Append( "\\u" + ((int)ch).ToString( "x4" ) );
			else sb.Append( ch );
		}
		sb.Append( '"' );
		return sb.ToString();
	}
}

/// <summary>Reads values from parsed JSON (missing or wrong-typed values give the fallback).</summary>
public static class RPJson
{
	public static JsonObject Parse( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) ) return null;
		try { return Json.ParseToJsonObject( text ); }
		catch ( Exception e ) { Log.Warning( "DarkRP: bad JSON: " + e.Message ); return null; }
	}

	public static bool HasKey( JsonObject o, string key ) => o != null && o.ContainsKey( key ) && o[key] != null;

	public static string Str( JsonObject o, string key, string fallback )
	{
		if ( !HasKey( o, key ) ) return fallback;
		try { return o[key].ToString(); } catch { return fallback; }
	}

	public static long Long( JsonObject o, string key, long fallback )
	{
		if ( !HasKey( o, key ) ) return fallback;
		try { return (long)o[key]; }
		catch
		{
			try { return (long)(double)o[key]; } catch { }
			return (long)RPUtil.ParseFloat( Str( o, key, "" ), fallback );
		}
	}

	public static int Int( JsonObject o, string key, int fallback ) => (int)Long( o, key, fallback );

	public static float Flt( JsonObject o, string key, float fallback )
	{
		if ( !HasKey( o, key ) ) return fallback;
		try { return (float)(double)o[key]; }
		catch { return RPUtil.ParseFloat( Str( o, key, "" ), fallback ); }
	}

	public static bool Bool( JsonObject o, string key, bool fallback )
	{
		if ( !HasKey( o, key ) ) return fallback;
		try { return (bool)o[key]; }
		catch
		{
			var s = Str( o, key, "" ).ToLowerInvariant();
			if ( s == "true" || s == "1" ) return true;
			if ( s == "false" || s == "0" ) return false;
			return fallback;
		}
	}

	/// <summary>A string array; a single string becomes a one-element array. Missing: fallback.</summary>
	public static string[] StrArray( JsonObject o, string key, string[] fallback )
	{
		if ( !HasKey( o, key ) ) return fallback;
		var node = o[key];
		var list = new List<string>();
		try
		{
			var arr = node.AsArray();
			foreach ( var item in arr ) if ( item != null ) list.Add( item.ToString() );
		}
		catch
		{
			var s = node.ToString();
			if ( !string.IsNullOrEmpty( s ) ) list.Add( s );
		}
		return list.ToArray();
	}

	public static JsonArray Arr( JsonObject o, string key )
	{
		if ( !HasKey( o, key ) ) return null;
		try { return o[key].AsArray(); } catch { return null; }
	}

	public static JsonObject Obj( JsonObject o, string key )
	{
		if ( !HasKey( o, key ) ) return null;
		try { return o[key].AsObject(); } catch { return null; }
	}
}
