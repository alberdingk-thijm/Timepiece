﻿using System.CommandLine;
using System.Diagnostics;
using Newtonsoft.Json;
using Timepiece;
using Timepiece.Angler.Ast;
using Timepiece.Angler.DataTypes;
using Timepiece.Angler.Queries;
using ZenLib;

ZenSettings.UseLargeStack = true;
ZenSettings.LargeStackSize = 30_000_000;

var rootCommand = new RootCommand("Timepiece benchmark runner");
var monoOption = new System.CommandLine.Option<bool>(
  new[] {"--mono", "--ms", "-m"},
  "If given, run the benchmark monolithically (simulating Minesweeper)");
var queryOption = new System.CommandLine.Option<bool>(
  new[] {"--query", "-q"},
  "If given, print the query formulas to stdout");
var trackTermsOption = new System.CommandLine.Option<bool>(
  new[] {"--track-terms", "-t"},
  "If given, turn on tracking of the visited terms of a route.");
var fileArgument = new Argument<string>(
  "file",
  "The .angler.json file to use");
var queryArgument =
  new Argument<NetworkQueryType>("query",
    description: "The type of query to check",
    parse: result => NetworkQueryTypeExtensions.Parse(result.Tokens.Single().Value));
rootCommand.Add(fileArgument);
rootCommand.Add(queryArgument);
rootCommand.Add(monoOption);
rootCommand.Add(queryOption);
rootCommand.Add(trackTermsOption);
rootCommand.SetHandler(
  (file, queryType, mono, printQuery, trackTerms) =>
  {
    var json = new JsonTextReader(new StreamReader(file));

    var ast = AstSerializationBinder.JsonSerializer().Deserialize<AnglerNetwork>(json);

    Console.WriteLine($"Successfully deserialized JSON file {file}");
    Debug.WriteLine("Running in debug mode...");
    Debug.WriteLine("Warning: additional assertions in debug mode may substantially slow running time!");
    json.Close();
    if (ast != null)
    {
      var (topology, transfer) = ast.TopologyAndTransfer(trackTerms: trackTerms);
      var externalNodes = ast.Externals.Select(i => i.Name);
      var query = queryType switch
      {
        NetworkQueryType.Internet2BlockToExternal => Internet2.BlockToExternal(topology, externalNodes),
        NetworkQueryType.Internet2NoMartians => Internet2.NoMartians(topology, externalNodes),
        NetworkQueryType.Internet2NoPrivateAs => Internet2.NoPrivateAs(topology, externalNodes),
        NetworkQueryType.Internet2GaoRexford => Internet2.GaoRexford(topology, externalNodes),
        NetworkQueryType.Internet2Reachable => Internet2.Reachable(topology, externalNodes),
        NetworkQueryType.Internet2ReachableInternal => Internet2.ReachableInternal(topology),
        NetworkQueryType.FatReachable => FatTreeQuery.Reachable(FatTreeQuery.LabelFatTree(topology)),
        NetworkQueryType.FatPathLength => FatTreeQuery.MaxPathLength(FatTreeQuery.LabelFatTree(topology)),
        NetworkQueryType.FatValleyFreedom => FatTreeQuery.ValleyFreedom(FatTreeQuery.LabelFatTree(topology)),
        NetworkQueryType.FatHijackFiltering => FatTreeQuery.FatTreeHijackFiltering(FatTreeQuery.LabelFatTree(topology)),
        _ => throw new ArgumentOutOfRangeException(nameof(queryType), queryType, "Query type not supported!")
      };
      var net = query.ToNetwork(topology, transfer, RouteEnvironmentExtensions.MinOptional);
      // turn on query printing if true
      net.PrintFormulas = printQuery;
      if (mono)
        Profile.RunMonoWithStats(net);
      else
        Profile.RunAnnotatedWithStats(net);
    }
    else
    {
      Console.WriteLine("Failed to deserialize contents of {file} (received null).");
    }
  }, fileArgument, queryArgument, monoOption, queryOption, trackTermsOption);

await rootCommand.InvokeAsync(args);
