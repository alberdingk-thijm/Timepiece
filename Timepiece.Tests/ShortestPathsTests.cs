using System;
using System.Collections.Generic;
using System.Numerics;
using Timepiece.Networks;
using Xunit;
using ZenLib;

namespace Timepiece.Tests;

public static class ShortestPathsTests
{
  private static readonly SymbolicValue<BigInteger> DRoute = new("d", r => r >= BigInteger.Zero);

  private static readonly ShortestPath<string, Unit> Concrete = new(Topologies.Path(3), "A",
    System.Array.Empty<SymbolicValue<Unit>>());

  private static readonly ShortestPath<string, BigInteger> SymbolicRoute = new(Topologies.Complete(3),
    new Dictionary<string, Zen<Option<BigInteger>>>
    {
      {"A", Option.Create(DRoute.Value)},
      {"B", Option.None<BigInteger>()},
      {"C", Option.None<BigInteger>()}
    }, new[] {DRoute});

  private static ShortestPath<string, string> SymbolicDestination(Digraph<string> digraph)
  {
    var dest = new SymbolicValue<string>("dest", s => digraph.FoldNodes(Zen.False(), (b, n) => Zen.Or(b, s == n)));
    return new ShortestPath<string, string>(digraph,
      digraph.MapNodes(n => Zen.If(dest.EqualsValue(n),
        Option.Create<BigInteger>(BigInteger.Zero), Option.Null<BigInteger>())), new[] {dest});
  }

  private static readonly Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<bool>>> ConcreteWeakSafetyProperties =
    Concrete.Digraph.MapNodes(_ => Lang.IsSome<BigInteger>());

  private static readonly Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<bool>>> ConcreteStrongSafetyProperties =
    new()
    {
      {"A", Lang.IfSome<BigInteger>(r => r == BigInteger.Zero)},
      {"B", Lang.IfSome<BigInteger>(r => r == BigInteger.One)},
      {"C", Lang.IfSome<BigInteger>(r => r == new BigInteger(2))}
    };

  private static AnnotatedNetwork<Option<BigInteger>, string, Unit> AnnotatedConcrete(
    Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<BigInteger>, Zen<bool>>> annotations,
    IReadOnlyDictionary<string, Func<Zen<Option<BigInteger>>, Zen<bool>>> stableProperties)
  {
    return new AnnotatedNetwork<Option<BigInteger>, string, Unit>(Concrete, annotations,
      stableProperties, Concrete.Digraph.MapNodes(_ => Lang.True<Option<BigInteger>>()), 4);
  }

  private static AnnotatedNetwork<Option<BigInteger>, string, BigInteger> AnnotatedSymbolicRoute(
    Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<BigInteger>, Zen<bool>>> annotations,
    IReadOnlyDictionary<string, Func<Zen<Option<BigInteger>>, Zen<bool>>> stableProperties)
  {
    return new AnnotatedNetwork<Option<BigInteger>, string, BigInteger>(SymbolicRoute, annotations, stableProperties,
      SymbolicRoute.Digraph.MapNodes(_ => Lang.True<Option<BigInteger>>()), 2);
  }

  [Fact]
  public static void SoundAnnotationsPassChecks()
  {
    var annotations = new Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<BigInteger>, Zen<bool>>>
    {
      {"A", Lang.Equals<Option<BigInteger>>(Option.Some(new BigInteger(0)))},
      {
        "B",
        Lang.Until(new BigInteger(1), Lang.IsNone<BigInteger>(), Lang.IfSome<BigInteger>(r => r == new BigInteger(1)))
      },
      {
        "C",
        Lang.Until(new BigInteger(2), Lang.IsNone<BigInteger>(), Lang.IfSome<BigInteger>(r => r == new BigInteger(2)))
      }
    };
    var net = AnnotatedConcrete(annotations, ConcreteWeakSafetyProperties);

    NetworkAssert.CheckSound(net);
  }

  [Fact]
  public static void SoundPathLengthAnnotationsPassChecks()
  {
    var annotations = new Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<BigInteger>, Zen<bool>>>
    {
      {"A", Lang.Equals<Option<BigInteger>>(Option.Some(new BigInteger(0)))},
      {
        "B",
        Lang.Until(new BigInteger(1), Lang.IsNone<BigInteger>(), Lang.IfSome<BigInteger>(r => r == new BigInteger(1)))
      },
      {
        "C",
        Lang.Until(new BigInteger(2), Lang.IsNone<BigInteger>(), Lang.IfSome<BigInteger>(r => r == new BigInteger(2)))
      }
    };
    var net = AnnotatedConcrete(annotations, ConcreteStrongSafetyProperties);
    NetworkAssert.CheckSound(net);
  }

  [Fact]
  public static void UnsoundAnnotationsFailChecks()
  {
    var annotations = new Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<BigInteger>, Zen<bool>>>
    {
      {"A", Lang.Equals<Option<BigInteger>>(Option.Some(new BigInteger(0)))},
      {"B", Lang.Never(Lang.IsSome<BigInteger>())},
      {"C", Lang.Never(Lang.IsSome<BigInteger>())}
    };
    var net = AnnotatedConcrete(annotations, ConcreteStrongSafetyProperties);

    NetworkAssert.CheckUnsound(net);
  }

  [Fact]
  public static void SoundSymbolicAnnotationsPassChecks()
  {
    var annotations = new Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<BigInteger>, Zen<bool>>>
    {
      {"A", Lang.Equals(Option.Create(DRoute.Value))},
      {"B", Lang.Until(new BigInteger(1), Lang.IsNone<BigInteger>(), Lang.IfSome<BigInteger>(r => r >= DRoute.Value))},
      {"C", Lang.Until(new BigInteger(1), Lang.IsNone<BigInteger>(), Lang.IfSome<BigInteger>(r => r >= DRoute.Value))}
    };
    var net = AnnotatedSymbolicRoute(annotations, SymbolicRoute.Digraph.MapNodes(_ => Lang.IsSome<BigInteger>()));

    NetworkAssert.CheckSound(net);
  }

  [Fact]
  public static void UnsoundSymbolicAnnotationsFailChecks()
  {
    var annotations = new Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<BigInteger>, Zen<bool>>>
    {
      {"A", Lang.Globally(Lang.IfSome<BigInteger>(r => r <= DRoute.Value))},
      {"B", Lang.Finally(new BigInteger(1), Lang.IfSome<BigInteger>(r => r <= DRoute.Value))},
      {"C", Lang.Finally(new BigInteger(1), Lang.IfSome<BigInteger>(r => r <= DRoute.Value))}
    };
    var net = AnnotatedSymbolicRoute(annotations, SymbolicRoute.Digraph.MapNodes(_ => Lang.IsSome<BigInteger>()));

    NetworkAssert.CheckUnsound(net);
  }

  [Fact]
  public static void SoundSymbolicDestAnnotationsPassChecks()
  {
    var topology = Topologies.Path(3);
    var net = SymbolicDestination(topology);
    var dest = net.Symbolics[0];
    var convergeTime = new BigInteger(3);
    var annotations =
      new Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<BigInteger>, Zen<bool>>>
      {
        {
          "A",
          Lang.Until<Option<BigInteger>>(
            Zen.If(dest.EqualsValue("A"), new BigInteger(0),
              Zen.If<BigInteger>(dest.EqualsValue("B"), new BigInteger(1), new BigInteger(2))),
            Option.IsNone, Option.IsSome)
        },
        {
          "B",
          Lang.Until<Option<BigInteger>>(
            Zen.If<BigInteger>(dest.DoesNotEqualValue("B"), new BigInteger(1), new BigInteger(0)),
            Option.IsNone, Option.IsSome)
        },
        {
          "C",
          Lang.Until<Option<BigInteger>>(
            Zen.If(dest.EqualsValue("A"), new BigInteger(2),
              Zen.If<BigInteger>(dest.EqualsValue("B"), new BigInteger(1), new BigInteger(0))),
            Option.IsNone, Option.IsSome)
        }
      };
    var annotated = new AnnotatedNetwork<Option<BigInteger>, string, string>(net, annotations,
      topology.MapNodes(_ => Lang.Finally(convergeTime, Lang.IsSome<BigInteger>())),
      topology.MapNodes(_ => Lang.IsSome<BigInteger>()));

    NetworkAssert.CheckSound(annotated);
  }

  [Fact]
  public static void UnsoundSymbolicDestAnnotationsFailChecks()
  {
    var topology = Topologies.Path(3);
    var net = SymbolicDestination(topology);
    var annotations =
      new Dictionary<string, Func<Zen<Option<BigInteger>>, Zen<BigInteger>, Zen<bool>>>
      {
        {"A", Lang.Finally<Option<BigInteger>>(new BigInteger(1), Option.IsSome)},
        {"B", Lang.Finally<Option<BigInteger>>(new BigInteger(1), Option.IsSome)},
        {"C", Lang.Finally<Option<BigInteger>>(new BigInteger(1), Option.IsSome)}
      };
    var annotated = new AnnotatedNetwork<Option<BigInteger>, string, string>(net, annotations,
      topology.MapNodes(_ => Lang.Finally(new BigInteger(3), Lang.IsSome<BigInteger>())),
      topology.MapNodes(_ => Lang.IsSome<BigInteger>()));

    NetworkAssert.CheckUnsoundCheck(annotated, SmtCheck.Inductive);
  }
}
