namespace Timekeeper.Json.UntypedAst;

public class ConstantExpr : Expr
{
  public readonly dynamic value;

  public ConstantExpr(dynamic value)
  {
    this.value = value;
  }

  public override void Rename(string oldVar, string newVar)
  {
    ;
  }
}
