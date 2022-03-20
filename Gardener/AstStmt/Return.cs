using Gardener.AstExpr;
using ZenLib;

namespace Gardener.AstStmt;

public class Return<T>: Statement<T, T>
{
  public Return(Expr<T, T> expr)
  {
    Expr = expr;
  }

  public Expr<T, T> Expr { get; set; }

  public override State<T> Evaluate(State<T> state)
  {
    state.Return = Expr.Evaluate(state);
    return state;
  }

  public override Statement<Unit, T> Bind(string var)
  {
    return new Assign<T>(var, Expr);
  }

  public override void Rename(string oldVar, string newVar)
  {
    Expr.Rename(oldVar, newVar);
  }
}