using System.Collections.Generic;

namespace Client.Scripts.RailgunScript;

// ── Expressions ──────────────────────────────────────────────────────────────

internal abstract record Expr;

internal sealed record IntLit(ulong Value) : Expr;
internal sealed record StrLit(string Value, bool Wide) : Expr;
internal sealed record VarExpr(string Name) : Expr;
internal sealed record AddrExpr(string Name) : Expr;        // &$var
internal sealed record DerefExpr(string Name) : Expr;       // *$var
internal sealed record ConstExpr(string Name) : Expr;       // CONST_NAME
internal sealed record BinExpr(Expr Left, TokenKind Op, Expr Right) : Expr;
internal sealed record CallExpr(string? Dll, string Func, List<Expr> Args) : Expr;

// ── Statements ────────────────────────────────────────────────────────────────

internal abstract record Stmt;

internal sealed record ImportStmt(string Dll) : Stmt;
internal sealed record IncludeStmt(string Path) : Stmt;
internal sealed record DefineStmt(string Name, ulong Value) : Stmt;
internal sealed record AssignStmt(string Var, Expr Rhs) : Stmt;
internal sealed record ExprStmt(Expr Expr) : Stmt;
internal sealed record IfStmt(Expr Cond, List<Stmt> Then, List<Stmt>? Else) : Stmt;
internal sealed record WhileStmt(Expr Cond, List<Stmt> Body) : Stmt;