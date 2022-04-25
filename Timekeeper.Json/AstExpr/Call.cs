using ZenLib;

namespace Timekeeper.Json.AstExpr;

public class Call<T> : Expr<T, T>
{
  public Call(string name)
  {
    Name = name;
  }

  public string Name { get; set; }

  public override Func<Zen<T>, Zen<T>> Evaluate(AstState<T> astState)
  {
    throw new NotImplementedException();
  }

  public override void Rename(string oldVar, string newVar)
  {
    ; // no-op
  }
}