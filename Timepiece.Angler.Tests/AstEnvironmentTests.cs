using System.Numerics;
using Timepiece.Angler.Ast;
using ZenLib;

namespace Timepiece.Angler.Tests;

public static class AstEnvironmentTests
{
  private const string DefaultPolicy = "defaultPolicy";
  private const string WillFallThrough = "willFallThrough";
  private const string ExitReject = "exitReject";
  private const string AddCommunity = "addCommunity";
  private const string CommunityTag = "10000:99999";

  /// <summary>
  ///   AstFunction to add a community to the route.
  /// </summary>
  private static readonly AstFunction<RouteEnvironment> AddCommunityFunction = new("env", new[]
  {
    new Assign("env",
      new WithField(new Var("env"), "Communities",
        new SetAdd(new StringExpr(CommunityTag),
          new GetField(typeof(RouteEnvironment), typeof(CSet<string>), new Var("env"), "Communities")))),
    UpdateResult("env", "Fallthrough", new BoolExpr(true))
  });

  // AstFunction that exits and rejects the route.
  private static readonly AstFunction<RouteEnvironment> ExitRejectFunction = UpdateResultFunction(new RouteResult
  {
    Exit = true,
    Value = false
  });

  // AstFunction that falls through
  private static readonly AstFunction<RouteEnvironment> FallThroughFunction = UpdateResultFunction(new RouteResult
  {
    Returned = false,
    Fallthrough = true
  });

  // AstFunction that sets returned and value to true.
  private static readonly AstFunction<RouteEnvironment> ReturnTrueFunction = UpdateResultFunction(new RouteResult
  {
    Returned = true,
    Value = true
  });

  // DefaultPolicy is a policy that just accepts a route.
  private static readonly AstEnvironment Env = new(new Dictionary<string, AstFunction<RouteEnvironment>>
  {
    {DefaultPolicy, ReturnTrueFunction},
    {WillFallThrough, FallThroughFunction},
    {ExitReject, ExitRejectFunction},
    {AddCommunity, AddCommunityFunction}
  });

  private static readonly Environment<RouteEnvironment> R = new(Zen.Symbolic<RouteEnvironment>());

  private static AstFunction<RouteEnvironment> UpdateResultFunction(RouteResult result)
  {
    return new AstFunction<RouteEnvironment>("env", new Statement[]
    {
      new Assign("env",
        new WithField(new Var("env"), "Result",
          AstEnvironment.ResultToRecord(result)))
    });
  }

  /// <summary>
  ///   An expression that evaluates to true if the given argument is unreachable (has exited or returned).
  /// </summary>
  /// <param name="routeArg"></param>
  /// <returns></returns>
  private static Expr Unreachable(string routeArg)
  {
    return new Or(
      new GetField("TResult", "TBool",
        new GetField("TEnvironment", "TResult", new Var(routeArg), "Result"),
        "Returned"),
      new GetField("TResult", "TBool",
        new GetField("TEnvironment", "TResult", new Var(routeArg), "Result"),
        "Exit"));
  }

  /// <summary>
  ///   Return a statement to assign a new value to the given field of the result to the given argument.
  /// </summary>
  /// <param name="arg">The route environment.</param>
  /// <param name="resultField">The name of the field of the result type.</param>
  /// <param name="resultFieldValue">The updated value of the result's field.</param>
  /// <returns></returns>
  private static Statement UpdateResult(string arg, string resultField, Expr resultFieldValue)
  {
    return new Assign(arg,
      new WithField(new Var(arg), "Result", new WithField(
        new GetField(typeof(RouteEnvironment), typeof(RouteResult), new Var(arg), "Result"),
        resultField, resultFieldValue)));
  }

  private static dynamic EvaluateExprIgnoreRoute(Expr e)
  {
    return Env.EvaluateExpr(R, e).returnValue;
  }

  /// <summary>
  ///   Helper method for checking that two values are always equal.
  /// </summary>
  /// <param name="expr1"></param>
  /// <param name="expr2"></param>
  /// <typeparam name="T"></typeparam>
  private static void AssertEqValid<T>(Zen<T> expr1, Zen<T> expr2)
  {
    var b = Zen.Not(Zen.Eq(expr1, expr2)).Solve();
    Assert.False(b.IsSatisfiable());
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public static void EvaluateBoolExprs(bool e)
  {
    var evaluated = (Zen<bool>) EvaluateExprIgnoreRoute(new BoolExpr(e));
    var zen = Zen.Constant(e);
    AssertEqValid(evaluated, zen);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  [InlineData(1000000)]
  public static void EvaluateIntExprs(int e)
  {
    var evaluated = (Zen<int>) EvaluateExprIgnoreRoute(new IntExpr(e));
    var zen = Zen.Constant(e);
    AssertEqValid(evaluated, zen);
  }

  [Theory]
  [InlineData("foo")]
  [InlineData("bar")]
  public static void EvaluateStringExprs(string e)
  {
    var evaluated = (Zen<string>) EvaluateExprIgnoreRoute(new StringExpr(e));
    var zen = Zen.Constant(e);
    AssertEqValid(evaluated, zen);
  }

  [Fact]
  public static void EvaluateNoneExpr()
  {
    var zen = Option.Null<int>();
    var evaluated = (Zen<Option<int>>) EvaluateExprIgnoreRoute(new None(typeof(int)));
    AssertEqValid(evaluated, zen);
  }

  [Fact]
  public static void EvaluateDefaultRoute()
  {
    var zen = new RouteEnvironment();
    var evaluated = (Zen<RouteEnvironment>) EvaluateExprIgnoreRoute(AstEnvironment.DefaultRoute());
    AssertEqValid(evaluated, zen);
  }

  [Fact]
  public static void EvaluateAssignStmt()
  {
    const string name = "x";
    var env1 = Env.EvaluateStatement(name, R, new Assign(name, new IntExpr(0)));
    var evaluated = (Zen<int>) env1.EvaluateExpr(R, new Var(name)).returnValue;
    AssertEqValid(evaluated, Zen.Constant(0));
  }

  [Fact]
  public static void EvaluateVariableSwap()
  {
    const string dummy = "r";
    const string var1 = "x";
    const string var2 = "y";
    const string tempVar = "z";
    var statements = new List<Statement>
    {
      new Assign(var1, new IntExpr(0)),
      new Assign(var2, new IntExpr(1)),
      new Assign(tempVar, new Var(var1)),
      new Assign(var1, new Var(var2)),
      new Assign(var2, new Var(tempVar))
    };
    var env1 = Env.Update(dummy, R.route).EvaluateStatements(dummy, R, statements);
    var getVar1 = (Zen<int>) env1.EvaluateExpr(R, new Var(var1)).returnValue;
    var getVar2 = (Zen<int>) env1.EvaluateExpr(R, new Var(var2)).returnValue;
    AssertEqValid(getVar1, Zen.Constant(1));
    AssertEqValid(getVar2, Zen.Constant(0));
  }

  [Fact]
  public static void EvaluateIfStatementHavoc()
  {
    const string resultVar = "result";
    const int trueResult = 0;
    const int falseResult = 1;
    var statement = new IfThenElse(new Havoc(), new List<Statement>
    {
      new Assign(resultVar, new IntExpr(trueResult))
    }, new List<Statement>
    {
      new Assign(resultVar, new IntExpr(falseResult))
    });
    var env1 = Env.EvaluateStatement(resultVar, R, statement);
    var result = (Zen<int>) env1[resultVar];
    var b = Zen.Not(Zen.Or(Zen.Eq(result, Zen.Constant(trueResult)), Zen.Eq(result, Zen.Constant(falseResult))))
      .Solve();
    Assert.False(b.IsSatisfiable());
  }

  [Fact]
  public static void EvaluateGetField()
  {
    const string pathLen = "AsPathLength";
    var route = AstEnvironment.DefaultRoute();
    const string arg = "route";
    var statements = new Statement[]
    {
      new Assign(arg, route),
      new Assign(arg,
        new WithField(new Var(arg), "Result",
          new CreateRecord("TResult", new Dictionary<string, Expr>
          {
            // set the value by asking if the field we got is equal to 0
            {
              "Value",
              new Equals(
                new BigIntExpr(0),
                new GetField(typeof(RouteEnvironment), typeof(BigInteger), new Var(arg), pathLen))
            },
            {"Returned", new BoolExpr(false)},
            {"Exit", new BoolExpr(false)},
            {"Fallthrough", new BoolExpr(false)}
          })))
    };
    var env1 = Env.Update(arg, R.route).EvaluateStatements(arg, R, statements);
    var result = (Zen<RouteEnvironment>) env1[arg];
    var b = Zen.Not(result.GetResult().GetValue()).Solve();
    Assert.False(b.IsSatisfiable());
  }

  [Fact]
  public static void EvaluateIncrementFieldConstant()
  {
    const string pathLen = "AsPathLength";
    const string route = "route";
    var statements = new Statement[]
    {
      new Assign(route, AstEnvironment.DefaultRoute()),
      new Assign(route, new WithField(new Var(route), pathLen,
        new Plus(new GetField(typeof(RouteEnvironment), typeof(BigInteger), new Var(route), pathLen),
          new BigIntExpr(BigInteger.One))))
    };
    var env1 = Env.Update(route, R.route).EvaluateStatements(route, R, statements);
    var result = (Zen<RouteEnvironment>) env1[route];
    var incrementedRoute = Zen.Constant(new RouteEnvironment()).IncrementAsPathLength(BigInteger.One);
    AssertEqValid(result, incrementedRoute);
  }

  [Fact]
  public static void EvaluateIncrementFieldSymbolic()
  {
    const string pathLen = "AsPathLength";
    var route = Zen.Symbolic<RouteEnvironment>();
    const string rVar = "route";
    var env1 = Env.Update(rVar, route);
    const string lenVar = "len";
    var statements = new Statement[]
    {
      new Assign(lenVar,
        new GetField(typeof(RouteEnvironment), typeof(BigInteger), new Var(rVar), pathLen)),
      new Assign(rVar,
        new WithField(new Var(rVar), pathLen,
          new Plus(new Var(lenVar), new BigIntExpr(BigInteger.One))))
    };
    var env2 = env1.EvaluateStatements(rVar, R, statements);
    var result = (Zen<RouteEnvironment>) env2[rVar];
    var incrementedRoute = route.IncrementAsPathLength(BigInteger.One);
    AssertEqValid(result, incrementedRoute);
  }

  [Fact]
  public static void EvaluateReturnTrueFunction()
  {
    const string arg = "arg";
    var function = new AstFunction<RouteEnvironment>(arg, new Statement[]
    {
      new Assign(arg,
        new WithField(new Var(arg), "Result",
          AstEnvironment.ResultToRecord(new RouteResult
          {
            Value = true,
            Returned = true
          }))),
      new IfThenElse(Unreachable(arg), new[]
      {
        // update returned to false
        UpdateResult(arg, "Returned", new BoolExpr(false))
      }, new Statement[]
      {
        // update value to the default and fallthrough to true
        new Assign(arg, new WithField(new Var(arg), "Result",
          new WithField(
            new WithField(
              new GetField(typeof(RouteEnvironment), typeof(RouteResult), new Var(arg), "Result"),
              "Value",
              new GetField(typeof(RouteEnvironment), typeof(bool), new Var(arg), "LocalDefaultAction")),
            "Fallthrough",
            new BoolExpr(true))))
      })
    });

    // the function above resets the result fields after returning true, so Returned should also be false
    Zen<RouteEnvironment> ReturnTrue(Zen<RouteEnvironment> t)
    {
      return t.WithResult(new RouteResult
      {
        Value = true
      });
    }

    var inputRoute = Zen.Symbolic<RouteEnvironment>();
    var evaluatedFunction = Env.EvaluateFunction(function);
    AssertEqValid(ReturnTrue(inputRoute), evaluatedFunction(inputRoute));
  }

  [Theory]
  [InlineData(true, true, true)]
  [InlineData(true, false, false)]
  [InlineData(false, true, false)]
  [InlineData(false, false, false)]
  public static void EvaluateAndExprTruthTable(bool arg1, bool arg2, bool result)
  {
    var e = new And(new BoolExpr(arg1), new BoolExpr(arg2));
    var evaluated = EvaluateExprIgnoreRoute(e);
    AssertEqValid(evaluated, Zen.Constant(result));
  }

  [Theory]
  [InlineData(true, true, true)]
  [InlineData(true, false, true)]
  [InlineData(false, true, true)]
  [InlineData(false, false, false)]
  public static void EvaluateOrExprTruthTable(bool arg1, bool arg2, bool result)
  {
    var e = new Or(new BoolExpr(arg1), new BoolExpr(arg2));
    var evaluated = EvaluateExprIgnoreRoute(e);
    AssertEqValid(evaluated, Zen.Constant(result));
  }

  [Theory]
  [InlineData(true, false)]
  [InlineData(false, true)]
  public static void EvaluateNotExprTruthTable(bool arg, bool result)
  {
    var e = new Not(new BoolExpr(arg));
    var evaluated = EvaluateExprIgnoreRoute(e);
    AssertEqValid(evaluated, Zen.Constant(result));
  }

  [Theory]
  [InlineData(DefaultPolicy, true, false, false)]
  [InlineData(ExitReject, false, true, false)]
  [InlineData(WillFallThrough, false, false, true)]
  public static void EvaluateCallExpr(string functionName, bool value, bool exit, bool fallthrough)
  {
    var e = new Call(functionName);
    var evaluated = Env.EvaluateExpr(R, e);
    AssertEqValid(evaluated.returnValue, value);
    // returned will be reset to whatever it had been before the call, so we just check the value, exit and fallthrough
    AssertEqValid(evaluated.route,
      R.route.WithResult(R.route.GetResult().WithValue(value).WithExit(exit).WithFallthrough(fallthrough)));
  }

  [Fact]
  public static void EvaluateFirstMatchChainDefaultPolicySet()
  {
    const string arg = "env";
    var statements = new Statement[]
    {
      new SetDefaultPolicy(DefaultPolicy),
      // set the value according to the result of FirstMatchChain
      new IfThenElse(new FirstMatchChain(), new[]
        {
          UpdateResult(arg, "Value", new BoolExpr(true))
        },
        new[]
        {
          UpdateResult(arg, "Value", new BoolExpr(false))
        }
      )
    };
    var inputRoute = Zen.Symbolic<RouteEnvironment>().WithResult(new RouteResult());
    var evaluated = Env.Update(arg, inputRoute).EvaluateStatements(arg, R.WithRoute(inputRoute), statements);
    AssertEqValid(evaluated[arg], inputRoute.WithResultValue(true));
  }

  [Fact]
  public static void EvaluateFirstMatchChainFallThrough()
  {
    const string arg = "env";
    var statements = new Statement[]
    {
      new SetDefaultPolicy(DefaultPolicy),
      // set the value according to the result of FirstMatchChain
      new IfThenElse(new FirstMatchChain(new Call(WillFallThrough)), new[]
        {
          UpdateResult(arg, "Value", new BoolExpr(true))
        },
        new[]
        {
          UpdateResult(arg, "Value", new BoolExpr(false))
        }
      )
    };
    var inputRoute = Zen.Symbolic<RouteEnvironment>().WithResult(new RouteResult());
    var evaluated = Env.Update(arg, inputRoute).EvaluateStatements(arg, R.WithRoute(inputRoute), statements);
    AssertEqValid(evaluated[arg], inputRoute.WithResultValue(true));
  }

  [Fact]
  public static void EvaluateFirstMatchChainNoDefaultPolicy()
  {
    const string arg = "env";
    var statements = new Statement[]
    {
      new IfThenElse(new FirstMatchChain(), new Statement[]
        {
          new Assign(arg, new WithField(new Var(arg), "Value", new BoolExpr(true)))
        },
        new Statement[]
        {
          new Assign(arg, new WithField(new Var(arg), "Value", new BoolExpr(false)))
        }
      )
    };
    Assert.Throws<Exception>(() =>
      Env.Update(arg, R.route).EvaluateStatements(arg, R, statements));
  }

  /// <summary>
  ///   Test that a function that falls through modifies the resulting route when the FMC returns.
  /// </summary>
  [Fact]
  public static void EvaluateFirstMatchChainAddCommunity()
  {
    const string arg = "env";
    var fun = new AstFunction<RouteEnvironment>(arg,
      new Statement[]
      {
        new SetDefaultPolicy(DefaultPolicy),
        new IfThenElse(new FirstMatchChain(new Call(AddCommunity)), new[]
        {
          UpdateResult(arg, "Value", new BoolExpr(true))
        }, new[]
        {
          UpdateResult(arg, "Value", new BoolExpr(false))
        })
      });
    var inputRoute = Zen.Symbolic<RouteEnvironment>().WithResult(new RouteResult());
    var evaluated = Env.EvaluateFunction(fun)(inputRoute);
    AssertEqValid(evaluated,
      inputRoute.WithResultValue(true).WithCommunities(CSet.Add(inputRoute.GetCommunities(), CommunityTag)));
  }
}