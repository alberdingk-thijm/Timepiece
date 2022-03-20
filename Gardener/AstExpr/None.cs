using ZenLib;

namespace Gardener.AstExpr;

public class None<T, TState> : Expr<Option<T>, TState>
{
  public override Func<Zen<TState>, Zen<Option<T>>> Evaluate(State<TState> state)
  {
    return _ => Option.None<T>();
  }

  public override void Rename(string oldVar, string newVar)
  {
    ;
  }
}