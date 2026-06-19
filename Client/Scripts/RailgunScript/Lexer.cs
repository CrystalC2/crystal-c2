using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Scripts.RailgunScript;

internal enum TokenKind
{
    Eof, Newline,
    Import, Include, Define, If, Else, While,
    LBrace, RBrace, LParen, RParen, Comma, Semi,
    Assign, EqEq, Neq, Lt, Gt, Le, Ge,
    Bang, Amp, Star, Pipe,
    Var, Ident, Int, Str, WStr,
}

internal sealed record Token(TokenKind Kind, string Text, ulong IntVal, int Line);

internal sealed class Lexer(string src)
{
    private int _pos;
    private int _line = 1;

    public List<Token> Tokenize()
    {
        var toks = new List<Token>();
        while (true)
        {
            SkipSpaces();
            if (_pos >= src.Length)
            {
                toks.Add(Tok(TokenKind.Eof, ""));
                break;
            }

            var ch = src[_pos];

            if (ch == '#') { SkipLine(); continue; }

            if (ch is '\n' or '\r')
            {
                while (_pos < src.Length && src[_pos] is '\n' or '\r')
                {
                    if (src[_pos] == '\n') _line++;
                    _pos++;
                }
                var last = toks.Count > 0 ? toks[^1].Kind : TokenKind.Eof;
                if (last is not (TokenKind.Newline or TokenKind.LBrace or TokenKind.Eof))
                    toks.Add(Tok(TokenKind.Newline, "\n"));
                continue;
            }

            switch (ch)
            {
                case ';':  _pos++; toks.Add(Tok(TokenKind.Semi,   ";")); continue;
                case '{':  _pos++; toks.Add(Tok(TokenKind.LBrace, "{")); continue;
                case '}':  _pos++; toks.Add(Tok(TokenKind.RBrace, "}")); continue;
                case '(':  _pos++; toks.Add(Tok(TokenKind.LParen, "(")); continue;
                case ')':  _pos++; toks.Add(Tok(TokenKind.RParen, ")")); continue;
                case ',':  _pos++; toks.Add(Tok(TokenKind.Comma,  ",")); continue;
                case '&':  _pos++; toks.Add(Tok(TokenKind.Amp,    "&")); continue;
                case '*':  _pos++; toks.Add(Tok(TokenKind.Star,   "*")); continue;
                case '|':  _pos++; toks.Add(Tok(TokenKind.Pipe,   "|")); continue;
            }

            if (ch == '!')
            {
                _pos++;
                if (Peek() == '=') { _pos++; toks.Add(Tok(TokenKind.Neq, "!=")); }
                else toks.Add(Tok(TokenKind.Bang, "!"));
                continue;
            }
            if (ch == '=')
            {
                _pos++;
                if (Peek() == '=') { _pos++; toks.Add(Tok(TokenKind.EqEq, "==")); }
                else toks.Add(Tok(TokenKind.Assign, "="));
                continue;
            }
            if (ch == '<')
            {
                _pos++;
                if (Peek() == '=') { _pos++; toks.Add(Tok(TokenKind.Le, "<=")); }
                else toks.Add(Tok(TokenKind.Lt, "<"));
                continue;
            }
            if (ch == '>')
            {
                _pos++;
                if (Peek() == '=') { _pos++; toks.Add(Tok(TokenKind.Ge, ">=")); }
                else toks.Add(Tok(TokenKind.Gt, ">"));
                continue;
            }

            if (ch == '$')
            {
                _pos++;
                toks.Add(Tok(TokenKind.Var, ReadIdent()));
                continue;
            }

            if (ch == '"')
            {
                toks.Add(Tok(TokenKind.Str, ReadString()));
                continue;
            }
            if (ch == 'L' && _pos + 1 < src.Length && src[_pos + 1] == '"')
            {
                _pos++;
                toks.Add(Tok(TokenKind.WStr, ReadString()));
                continue;
            }

            if (char.IsDigit(ch))
            {
                var (text, val) = ReadInt();
                toks.Add(new Token(TokenKind.Int, text, val, _line));
                continue;
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                var ident = ReadIdent();
                // DLL names can contain dots: kernel32.dll
                while (_pos < src.Length && src[_pos] == '.' &&
                       _pos + 1 < src.Length && char.IsLetterOrDigit(src[_pos + 1]))
                {
                    _pos++;
                    ident += '.' + ReadIdent();
                }

                var kind = ident.ToLowerInvariant() switch
                {
                    "import"  => TokenKind.Import,
                    "include" => TokenKind.Include,
                    "define"  => TokenKind.Define,
                    "if"      => TokenKind.If,
                    "else"    => TokenKind.Else,
                    "while"   => TokenKind.While,
                    _         => TokenKind.Ident,
                };
                toks.Add(Tok(kind, ident));
                continue;
            }

            throw new Exception($"Unexpected character '{ch}' at line {_line}");
        }
        return toks;
    }

    private char Peek() => _pos < src.Length ? src[_pos] : '\0';

    private void SkipSpaces()
    {
        while (_pos < src.Length && src[_pos] is ' ' or '\t') _pos++;
    }

    private void SkipLine()
    {
        while (_pos < src.Length && src[_pos] != '\n') _pos++;
    }

    private string ReadIdent()
    {
        var sb = new StringBuilder();
        while (_pos < src.Length && (char.IsLetterOrDigit(src[_pos]) || src[_pos] == '_'))
            sb.Append(src[_pos++]);
        return sb.ToString();
    }

    private string ReadString()
    {
        _pos++; // skip opening "
        var sb = new StringBuilder();
        while (_pos < src.Length && src[_pos] != '"')
        {
            if (src[_pos] == '\\' && _pos + 1 < src.Length)
            {
                _pos++;
                sb.Append(src[_pos++] switch
                {
                    'n' => '\n', 't' => '\t', 'r' => '\r',
                    '"' => '"', '\\' => '\\', var c => c
                });
            }
            else sb.Append(src[_pos++]);
        }
        if (_pos < src.Length) _pos++; // skip closing "
        return sb.ToString();
    }

    private (string text, ulong value) ReadInt()
    {
        var start = _pos;
        ulong val = 0;
        if (_pos + 1 < src.Length && src[_pos] == '0' && src[_pos + 1] is 'x' or 'X')
        {
            _pos += 2;
            while (_pos < src.Length && IsHex(src[_pos]))
                val = val * 16 + HexVal(src[_pos++]);
        }
        else
        {
            while (_pos < src.Length && char.IsDigit(src[_pos]))
                val = val * 10 + (ulong)(src[_pos++] - '0');
        }
        return (src[start.._pos], val);
    }

    private Token Tok(TokenKind k, string text, ulong val = 0) => new(k, text, val, _line);

    private static bool IsHex(char c) =>
        c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    private static ulong HexVal(char c) =>
        c >= 'a' ? (ulong)(c - 'a' + 10) :
        c >= 'A' ? (ulong)(c - 'A' + 10) :
        (ulong)(c - '0');
}