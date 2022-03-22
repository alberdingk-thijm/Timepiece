using ZenLib;

namespace Gardener.AstExpr;

public class Second<TA, TB, TState> : UnaryOpExpr<Pair<TA, TB>, TB, TState>
{
  public Second(Expr<Pair<TA, TB>, TState> pair) : base(pair, p => p.Item2())
  {
  }
}
