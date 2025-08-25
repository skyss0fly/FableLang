// FabLang v0.1 – Minimal interpreter in C#
// Features:
// - File extension: .fab
// - Single-line comments: /# ... (to end of line)
// - Block comments: #* ... *#
// - Variables: $name = "skyss0fly", $age = 17
// - Nested data objects via brackets: $user = [$name = "skyss0fly", $age = 17]
// - Dot access: echo $user.$name
// - echo 
//
// Not yet implemented: arithmetic, conditionals, arrays distinct from maps, escapes in strings
//
// Usage:
//   dotnet run -- path/to/script.fab
//
// Example printing.fab:
//   /# This is a Comment
//   $word = "Hello World"
//   echo $word
//   #*
//   This is a Block Comment
//   *#
//   $user = [$name = "Sebastian", $age = 17]
//   echo $user.$name


using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;


namespace FabLang
{
public enum TokenType
{
EOF,
DOLLAR, IDENT, NUMBER, STRING,
EQUAL, LBRACKET, RBRACKET, COMMA, DOT,
KEYWORD_ECHO,
NEWLINE
}


public class Token
{
    public TokenType Type { get; }
    public string Lexeme { get; }
    public int Line { get; }
    public int Col { get; }
    public Token(TokenType type, string lexeme, int line, int col)
    { Type = type; Lexeme = lexeme; Line = line; Col = col; }
    public override string ToString() => $"{Type} '{Lexeme}' @ {Line}:{Col}";
}

public class Lexer
{
    private readonly string _src;
    private int _i = 0, _line = 1, _col = 1;

    public Lexer(string src)
    {
        _src = StripComments(src);
    }

    private static string StripComments(string s)
    {
        // Block comments #* ... *# (multiline)
        s = Regex.Replace(s, @"#\*[\s\S]*?\*#", "", RegexOptions.Multiline);
        // Line comments /# ... EOL
        s = Regex.Replace(s, @"/#[^\n\r]*", "");
        return s;
    }

    private bool End => _i >= _src.Length;
    private char Peek(int k = 0) => (_i + k < _src.Length) ? _src[_i + k] : '\0';
    private char Next()
    {
        if (End) return '\0';
        var c = _src[_i++];
        if (c == '\n') { _line++; _col = 1; } else { _col++; }
        return c;
    }

    public List<Token> Tokenize()
    {
        var toks = new List<Token>();
        while (!End)
        {
            char c = Peek();
            if (c == ' ' || c == '\t' || c == '\r') { Next(); continue; }
            if (c == '\n') { Next(); toks.Add(new Token(TokenType.NEWLINE, "\\n", _line, _col)); continue; }

            int line = _line, col = _col;

            switch (c)
            {
				//case ')': Next(); toks.Add(new Token(TokenType.RPAREN, ')', line,col)); break;
				//case '(': Next(); toks.Add(new Token(TokenType.LPAREN, '(', line,col)); break;
                case '$': Next(); toks.Add(new Token(TokenType.DOLLAR, "$", line, col)); break;
                case '=': Next(); toks.Add(new Token(TokenType.EQUAL, "=", line, col)); break;
                case '[': Next(); toks.Add(new Token(TokenType.LBRACKET, "[", line, col)); break;
                case ']': Next(); toks.Add(new Token(TokenType.RBRACKET, "]", line, col)); break;
                case ',': Next(); toks.Add(new Token(TokenType.COMMA, ",", line, col)); break;
                case '.': Next(); toks.Add(new Token(TokenType.DOT, ".", line, col)); break;
                case '"':
                    toks.Add(LexString());
                    break;
                default:
                    if (char.IsDigit(c) || (c == '-' && char.IsDigit(Peek(1))))
                    {
                        toks.Add(LexNumber());
                    }
                    else if (char.IsLetter(c) || c == '_')
                    {
                        var ident = LexIdentifier();
                        if (ident.Lexeme == "echo") ident = new Token(TokenType.KEYWORD_ECHO, ident.Lexeme, ident.Line, ident.Col);
                        toks.Add(ident);
                    }
                    else
                    {
                        throw new Exception($"Unexpected character '{c}' at {line}:{col}");
                    }
                    break;
            }
        }
        toks.Add(new Token(TokenType.EOF, "", _line, _col));
        return toks;
    }

    private Token LexString()
    {
        int line = _line, col = _col; // opening quote position
        var sb = new StringBuilder();
        Next(); // consume opening quote
        while (!End)
        {
            char c = Next();
            if (c == '"') break;
            if (c == '\\')
            {
                char n = Next();
                sb.Append(n switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => n
                });
            }
            else sb.Append(c);
        }
        return new Token(TokenType.STRING, sb.ToString(), line, col);
    }

    private Token LexNumber()
    {
        int line = _line, col = _col;
        var sb = new StringBuilder();
        if (Peek() == '-') { sb.Append(Next()); }
        while (char.IsDigit(Peek())) sb.Append(Next());
        if (Peek() == '.')
        {
            sb.Append(Next());
            while (char.IsDigit(Peek())) sb.Append(Next());
        }
        return new Token(TokenType.NUMBER, sb.ToString(), line, col);
    }

    private Token LexIdentifier()
    {
        int line = _line, col = _col;
        var sb = new StringBuilder();
        while (char.IsLetterOrDigit(Peek()) || Peek() == '_') sb.Append(Next());
        return new Token(TokenType.IDENT, sb.ToString(), line, col);
    }
}

// AST nodes (minimal)
public abstract class Stmt { }
public class StmtEcho : Stmt { public Expr Value; public StmtEcho(Expr v){ Value = v; } }
public class StmtAssign : Stmt { public string Name; public Expr Value; public StmtAssign(string n, Expr v){ Name=n; Value=v; } }

public abstract class Expr { }
public class ExprString : Expr { public string Value; public ExprString(string v){ Value=v; } }
public class ExprNumber : Expr { public double Value; public ExprNumber(double v){ Value=v; } }
public class ExprVarRef : Expr { public string Name; public ExprVarRef(string n){ Name=n; } }
public class ExprPath : Expr { public Expr Base; public string Key; public ExprPath(Expr b, string k){ Base=b; Key=k; } }
public class ExprMap : Expr { public List<(string key, Expr value)> Entries = new(); }

public class Parser
{
    private readonly List<Token> _toks; private int _p = 0;
    public Parser(List<Token> toks){ _toks = toks; }

    private Token Peek(int k=0) => (_p + k < _toks.Count) ? _toks[_p + k] : _toks[^1];
    private Token Advance() => _toks[_p++];
    private bool Match(TokenType t){ if (Peek().Type==t){ Advance(); return true; } return false; }
    private Token Expect(TokenType t, string msg){ var tok = Peek(); if (tok.Type!=t) throw new Exception($"{msg} at {tok.Line}:{tok.Col}"); return Advance(); }

    public List<Stmt> Parse()
    {
        var list = new List<Stmt>();
        while (Peek().Type != TokenType.EOF)
        {
            if (Peek().Type == TokenType.NEWLINE) { Advance(); continue; }
            list.Add(ParseStatement());
            if (Peek().Type == TokenType.NEWLINE) Advance();
        }
        return list;
    }

    private Stmt ParseStatement()
    {
        if (Peek().Type == TokenType.KEYWORD_ECHO)
        {
            Advance();
            var e = ParseExpr();
            return new StmtEcho(e);
        }
        // assignment: $name = expr
        if (Peek().Type == TokenType.DOLLAR)
        {
            Advance();
            string name = Expect(TokenType.IDENT, "Expected variable name after $").Lexeme;
            Expect(TokenType.EQUAL, "Expected '=' after variable");
            var value = ParseExpr();
            return new StmtAssign(name, value);
        }
        throw new Exception($"Unexpected token {Peek()}");
    }

    private Expr ParseExpr()
    {
        // Only primaries + map literals + dotted paths for v0.1
        var expr = ParsePrimary();
        // Handle .path .path ...
        while (Match(TokenType.DOT))
        {
            Expect(TokenType.DOLLAR, "Expected '$' after '.' for path access");
            var key = Expect(TokenType.IDENT, "Expected key after '.$'").Lexeme;
            expr = new ExprPath(expr, key);
        }
        return expr;
    }

    private Expr ParsePrimary()
    {
        var t = Peek();
        switch (t.Type)
        {
            case TokenType.STRING: Advance(); return new ExprString(t.Lexeme);
            case TokenType.NUMBER:
                Advance();
                if (!double.TryParse(t.Lexeme, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                    throw new Exception($"Invalid number literal {t.Lexeme} at {t.Line}:{t.Col}");
                return new ExprNumber(num);
            case TokenType.DOLLAR:
                Advance();
                string name = Expect(TokenType.IDENT, "Expected variable name after $").Lexeme;
                return new ExprVarRef(name);
            case TokenType.LBRACKET:
                return ParseMapLiteral();
            default:
                throw new Exception($"Unexpected token in expression: {t}");
        }
    }

    private Expr ParseMapLiteral()
    {
        // [ $key = expr, $key2 = expr, ... ]
        Expect(TokenType.LBRACKET, "Expected '['");
        var map = new ExprMap();
        while (Peek().Type != TokenType.RBRACKET)
        {
            Expect(TokenType.DOLLAR, "Expected '$' to start key in map literal");
            var key = Expect(TokenType.IDENT, "Expected identifier after '$' in map literal").Lexeme;
            Expect(TokenType.EQUAL, "Expected '=' after key in map literal");
            var val = ParseExpr();
            map.Entries.Add((key, val));
            if (Peek().Type == TokenType.COMMA) { Advance(); }
            else if (Peek().Type == TokenType.RBRACKET) { }
            else throw new Exception($"Expected ',' or ']' in map literal, got {Peek()}");
        }
        Expect(TokenType.RBRACKET, "Expected ']' to close map literal");
        return map;
    }
}

// Runtime values
public abstract class Value { public abstract string Print(); }
public class VNull : Value { public static readonly VNull Instance = new(); public override string Print() => "null"; }
public class VNumber : Value { public double Num; public VNumber(double n){ Num=n; } public override string Print()=> Num.ToString("0.########", CultureInfo.InvariantCulture); }
public class VString : Value { public string Str; public VString(string s){ Str=s; } public override string Print()=> Str; }
public class VMap : Value { public Dictionary<string, Value> Fields = new(StringComparer.Ordinal);
    public override string Print()
    {
        var sb = new StringBuilder();
        sb.Append("[");
        bool first = true;
        foreach (var kv in Fields)
        {
            if (!first) sb.Append(", "); first = false;
            sb.Append("$").Append(kv.Key).Append(" = ").Append(kv.Value.Print());
        }
        sb.Append("]");
        return sb.ToString();
    }
}

public class Interpreter
{
    private readonly Dictionary<string, Value> _env = new(StringComparer.Ordinal);
	
	public void LoadLibrary()
{
    var mathDllPath = @"C:\Libraries\FabMath\bin\Debug\net8.0\FabMath.dll";
    var assembly = Assembly.LoadFrom(mathDllPath);
    var mathType = assembly.GetType("FabMath.MathLibrary");
    var methods = mathType.GetMethods(BindingFlags.Public | BindingFlags.Static);

    var mathMap = new VMap();
    foreach (var method in methods)
    {
        // placeholder; you can wrap methods to callable delegates later
        mathMap.Fields[method.Name.ToLower()] = new VString($"[native method {method.Name}]");
    }

    _env["math"] = mathMap;
	}

    public void Execute(IEnumerable<Stmt> stmts)
    {
        foreach (var s in stmts) Exec(s);
    }

    private void Exec(Stmt s)
    {
        switch (s)
        {
            case StmtEcho e:
                var v = Eval(e.Value);
                Console.WriteLine(v.Print());
                break;
            case StmtAssign a:
                _env[a.Name] = Eval(a.Value);
                break;
            default:
                throw new Exception($"Unknown statement {s.GetType().Name}");
        }
    }

    private Value Eval(Expr e)
    {
        switch (e)
        {
            case ExprString es: return new VString(es.Value);
            case ExprNumber en: return new VNumber(en.Value);
            case ExprVarRef vr:
                return _env.TryGetValue(vr.Name, out var v) ? v : VNull.Instance;
            case ExprPath p:
            {
                var baseVal = Eval(p.Base);
                if (baseVal is VMap m && m.Fields.TryGetValue(p.Key, out var got)) return got;
                return VNull.Instance;
            }
            case ExprMap mm:
            {
                var vm = new VMap();
                foreach (var (key, valExpr) in mm.Entries)
                {
                    vm.Fields[key] = Eval(valExpr);
                }
                return vm;
            }
            default:
                throw new Exception($"Unknown expression {e.GetType().Name}");
        }
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("FableLang v0.1 — usage: dotnet run -- <file.fab>");
            Console.WriteLine("Running demo...\n");
            var demo = "$word = \"Hello World\"\n" +
                       "echo $word\n" +
                       "$user = [$name = \"skyss0fly\", $age = 17]\n" +
                       "echo $user.$name\n"+
					   "echo \"FableLang Made by skyss0fly!\"";
            RunSource(demo);
            return;
        }

        var path = args[0];
        var src = File.ReadAllText(path);
        RunSource(src);
    }

    private static void RunSource(string src)
    {
        try
        {
            var lexer = new Lexer(src);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var prog = parser.Parse();
            var interp = new Interpreter();
            interp.Execute(prog);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }
}



}
