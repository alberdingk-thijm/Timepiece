using ZenLib;

namespace Timekeeper.Json.AstExpr;

public class Some<T, TState> : UnaryOpExpr<T, Option<T>, TState>
{
  public Some(Expr<T, TState> value) : base(value, Option.Create)
  {
  }
}
