using ZenLib;

namespace Gardener.AstExpr;

public class WithField<T1, T2, TState> : Expr<T1, TState>
{
  public Expr<T1, TState> Record { get; set; }
  public string FieldName { get; set; }

  public Expr<T2,TState> FieldValue { get; set; }

  public WithField(Expr<T1, TState> record, string fieldName, Expr<T2, TState> fieldValue)
  {
    Record = record;
    FieldName = fieldName;
    FieldValue = fieldValue;
  }

  public override Func<Zen<TState>, Zen<T1>> Evaluate(State<TState> state)
  {
    return r => Record.Evaluate(state)(r).WithField<T1, T2>(FieldName, FieldValue.Evaluate(state)(r));
  }

  public override void Rename(string oldVar, string newVar)
  {
    Record.Rename(oldVar, newVar);
    FieldValue.Rename(oldVar, newVar);
  }
}