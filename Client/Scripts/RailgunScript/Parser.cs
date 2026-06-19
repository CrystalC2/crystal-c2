using System;
using System.Collections.Generic;

namespace Client.Scripts.RailgunScript;

internal sealed class Parser(List<Token> tokens)
{
    private int _pos;

    private Token Peek(int offset = 0) =>
        _pos + offset < tokens.Count ? tokens[_pos + offset] : tokens[^1];

    private Token Consume() => tokens[_pos++];

    private bool Check(TokenKind k) => Peek().Kind == k;

    private bool Match(TokenKind k)
    {
        if (!Check(k)) return false;
        _pos++;
        return true;
    }

    private Token Expect(TokenKind k)
    {
        if (!Check(k))
            throw new Exception($"Expected {k} but got '{Peek().Text}' at line {Peek().Line}");
        return Consume();
    }

    private void SkipTerminators()
    {
        while (Peek().Kind is TokenKind.Newline or TokenKind.Semi) _pos++;
    }

    private void ExpectTerminator()
    {
        if (Peek().Kind is TokenKind.Newline or TokenKind.Semi or TokenKind.Eof or TokenKind.RBrace)
            SkipTerminators();
        else
            throw new Exception($"Expected newline or ';' after statement at line {Peek().Line}");
    }

    public List<Stmt> ParseProgram()
    {
        SkipTerminators();
        var stmts = new List<Stmt>();
        while (!Check(TokenKind.Eof))
        {
            stmts.Add(ParseStmt());
            ExpectTerminator();
            SkipTerminators();
        }
        return stmts;
    }

    private Stmt ParseStmt()
    {
        if (Check(TokenKind.Import))
        {
            Consume();
            var dll = Expect(TokenKind.Ident).Text;
            return new ImportStmt(dll);
        }

        if (Check(TokenKind.Include))
        {
            Consume();
            var path = Expect(TokenKind.Str).Text;
            return new IncludeStmt(path);
        }

        if (Check(TokenKind.Define))
        {
            Consume();
            var name = Expect(TokenKind.Ident).Text;
            var tok  = Expect(TokenKind.Int);
            return new DefineStmt(name, tok.IntVal);
        }

        if (Check(TokenKind.If))    return ParseIf();
        if (Check(TokenKind.While)) return ParseWhile();

        // $var = expr
        if (Check(TokenKind.Var) && Peek(1).Kind == TokenKind.Assign)
        {
            var name = Consume().Text;
            Consume(); // =
            return new AssignStmt(name, ParseExpr());
        }

        return new ExprStmt(ParseExpr());
    }

    private IfStmt ParseIf()
    {
        Expect(TokenKind.If);
        Expect(TokenKind.LParen);
        var cond = ParseCond();
        Expect(TokenKind.RParen);
        SkipTerminators();
        var then = ParseBlock();

        List<Stmt>? els = null;
        var saved = _pos;
        SkipTerminators();
        if (Match(TokenKind.Else))
        {
            SkipTerminators();
            els = ParseBlock();
        }
        else
        {
            _pos = saved; // backtrack if no else
        }

        return new IfStmt(cond, then, els);
    }

    private WhileStmt ParseWhile()
    {
        Expect(TokenKind.While);
        Expect(TokenKind.LParen);
        var cond = ParseCond();
        Expect(TokenKind.RParen);
        SkipTerminators();
        return new WhileStmt(cond, ParseBlock());
    }

    private List<Stmt> ParseBlock()
    {
        Expect(TokenKind.LBrace);
        SkipTerminators();
        var stmts = new List<Stmt>();
        while (!Check(TokenKind.RBrace) && !Check(TokenKind.Eof))
        {
            stmts.Add(ParseStmt());
            ExpectTerminator();
            SkipTerminators();
        }
        Expect(TokenKind.RBrace);
        return stmts;
    }

    // Parses a condition — expr [op expr] — used inside if/while parens
    private Expr ParseCond()
    {
        var left = ParseExpr();
        var k = Peek().Kind;
        if (k is TokenKind.EqEq or TokenKind.Neq or
                 TokenKind.Lt   or TokenKind.Gt  or
                 TokenKind.Le   or TokenKind.Ge)
        {
            Consume();
            return new BinExpr(left, k, ParseExpr());
        }
        return left;
    }

    private List<Expr> ParseArgList()
    {
        // caller has already consumed the opening '('
        var args = new List<Expr>();
        while (!Check(TokenKind.RParen) && !Check(TokenKind.Eof))
        {
            args.Add(ParseExpr());
            if (!Match(TokenKind.Comma)) break;
        }
        Expect(TokenKind.RParen);
        return args;
    }

    private Expr ParseExpr()
    {
        var left = ParsePrimary();
        while (Check(TokenKind.Pipe))
        {
            Consume();
            left = new BinExpr(left, TokenKind.Pipe, ParsePrimary());
        }
        return left;
    }

    private Expr ParsePrimary()
    {
        // &$var  — address-of for out-params
        if (Check(TokenKind.Amp))
        {
            Consume();
            return new AddrExpr(Expect(TokenKind.Var).Text);
        }

        // *$var  — pointer dereference (reads one ULONG_PTR from the address in $var)
        if (Check(TokenKind.Star))
        {
            Consume();
            return new DerefExpr(Expect(TokenKind.Var).Text);
        }

        if (Check(TokenKind.Var))     return new VarExpr(Consume().Text);
        if (Check(TokenKind.Int))     { var t = Consume(); return new IntLit(t.IntVal); }
        if (Check(TokenKind.Str))     return new StrLit(Consume().Text, Wide: false);
        if (Check(TokenKind.WStr))    return new StrLit(Consume().Text, Wide: true);

        if (Check(TokenKind.Ident))
        {
            var first = Consume().Text;

            if (Check(TokenKind.Bang))
            {
                // dll!func(...)
                Consume();
                var func = Expect(TokenKind.Ident).Text;
                Expect(TokenKind.LParen);
                var args = ParseArgList();
                return new CallExpr(first, func, args);
            }

            if (Check(TokenKind.LParen))
            {
                // func(...)
                Consume();
                var args = ParseArgList();
                return new CallExpr(null, first, args);
            }

            // bare identifier — resolved as a named constant at compile time
            return new ConstExpr(first);
        }

        throw new Exception($"Unexpected token '{Peek().Text}' at line {Peek().Line}");
    }
}